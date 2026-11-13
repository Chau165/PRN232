using System.Net;
using Microsoft.Extensions.Configuration;
using PRN232.Application.DTOs;
using PRN232.Application.Services;
using PRN232.Infrastructure.Storage;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class SupabaseRoomImageStorageTests
{
    [Fact]
    public async Task Upload_uses_a_random_object_name_and_deletes_only_from_the_room_bucket()
    {
        var handler = new StorageHandler();
        using var client = new HttpClient(handler);
        var storage = new SupabaseRoomImageStorage(client, Configuration());
        var url = await storage.UploadAsync(new RoomImageInput("../../client.png", "image/png", [1, 2, 3]));
        Assert.StartsWith("https://project.supabase.co/storage/v1/object/public/room-images/rooms/", url);
        Assert.EndsWith(".png", url);
        Assert.DoesNotContain("client", url);
        await storage.DeleteAsync(url);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("test-only-key", handler.Requests[0].ApiKey);
        Assert.Equal("image/png", handler.Requests[0].ContentType);
        Assert.Equal(HttpMethod.Delete, handler.Requests[1].Method);
        Assert.Contains("rooms/", handler.Requests[1].Body);
    }

    [Theory]
    [InlineData("https://other.supabase.co/storage/v1/object/public/room-images/rooms/photo.png")]
    [InlineData("https://project.supabase.co/storage/v1/object/public/property-images/rooms/photo.png")]
    [InlineData("https://project.supabase.co/storage/v1/object/sign/room-images/rooms/photo.png?token=secret")]
    [InlineData("https://project.supabase.co/storage/v1/object/public/room-images/rooms/photo.png?alias=1")]
    [InlineData("https://project.supabase.co/storage/v1/object/public/room-images/rooms/%70hoto.png")]
    public async Task Delete_does_not_touch_foreign_private_or_noncanonical_urls(string url)
    {
        var handler = new StorageHandler();
        using var client = new HttpClient(handler);
        await new SupabaseRoomImageStorage(client, Configuration()).DeleteAsync(url);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Storage_errors_become_a_domain_storage_error()
    {
        using var client = new HttpClient(new StorageHandler { Status = HttpStatusCode.Forbidden });
        var storage = new SupabaseRoomImageStorage(client, Configuration());
        await Assert.ThrowsAsync<RoomImageStorageException>(() => storage.UploadAsync(new RoomImageInput("room.png", "image/png", [1])));
    }

    [Theory]
    [InlineData(true, HttpStatusCode.OK)]
    [InlineData(false, HttpStatusCode.ServiceUnavailable)]
    [InlineData(false, HttpStatusCode.TooManyRequests)]
    public async Task Upload_retries_transient_failures_using_the_same_object_and_content(bool networkFailure, HttpStatusCode firstStatus)
    {
        var handler = new RetryStorageHandler(networkFailure, firstStatus);
        using var client = new HttpClient(handler);
        var storage = new SupabaseRoomImageStorage(client, Configuration());
        var url = await storage.UploadAsync(new RoomImageInput("images (2).jpg", "image/jpeg", [0xFF, 0xD8, 0xFF, 0xD9]));
        Assert.EndsWith(".jpg", url);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(handler.Requests[0].Url, handler.Requests[1].Url);
        Assert.Equal(handler.Requests[0].Content, handler.Requests[1].Content);
        Assert.Equal("true", handler.Requests[1].Upsert);
    }

    [Fact]
    public async Task Upload_limits_retries_and_does_not_retry_forbidden_responses()
    {
        var unavailable = new RetryStorageHandler(false, HttpStatusCode.ServiceUnavailable, alwaysFail: true);
        using var client = new HttpClient(unavailable);
        await Assert.ThrowsAsync<RoomImageStorageException>(() => new SupabaseRoomImageStorage(client, Configuration())
            .UploadAsync(new RoomImageInput("room.png", "image/png", [1])));
        Assert.Equal(3, unavailable.Requests.Count);

        var forbidden = new RetryStorageHandler(false, HttpStatusCode.Forbidden, alwaysFail: true);
        using var forbiddenClient = new HttpClient(forbidden);
        await Assert.ThrowsAsync<RoomImageStorageException>(() => new SupabaseRoomImageStorage(forbiddenClient, Configuration())
            .UploadAsync(new RoomImageInput("room.png", "image/png", [1])));
        Assert.Single(forbidden.Requests);
    }

    [Fact]
    public async Task Upload_respects_caller_cancellation_without_retrying()
    {
        var handler = new RetryStorageHandler(true, HttpStatusCode.OK);
        using var client = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new SupabaseRoomImageStorage(client, Configuration())
            .UploadAsync(new RoomImageInput("room.png", "image/png", [1]), cancellation.Token));
        Assert.Empty(handler.Requests);
    }

    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Supabase:Url"] = "https://project.supabase.co",
        ["Supabase:ServiceRoleKey"] = "test-only-key"
    }).Build();

    private sealed class RetryStorageHandler(bool networkFailure, HttpStatusCode firstStatus, bool alwaysFail = false) : HttpMessageHandler
    {
        public List<(string Url, byte[] Content, string Upsert)> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add((request.RequestUri!.AbsoluteUri, await request.Content!.ReadAsByteArrayAsync(cancellationToken),
                request.Headers.GetValues("x-upsert").Single()));
            if (Requests.Count == 1 || alwaysFail)
            {
                if (networkFailure) throw new HttpRequestException(HttpRequestError.ConnectionError);
                return new HttpResponseMessage(firstStatus);
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class StorageHandler : HttpMessageHandler
    {
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        public List<(HttpMethod Method, string ApiKey, string? ContentType, string Body)> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method, request.Headers.GetValues("apikey").Single(), request.Content?.Headers.ContentType?.MediaType,
                request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(Status);
        }
    }
}
