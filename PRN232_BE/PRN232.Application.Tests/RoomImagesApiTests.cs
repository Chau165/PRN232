using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using PRN232.Application.DTOs;
using PRN232.Api.Controllers;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Application.Settings;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class RoomImagesApiTests
{
    private const string Key = "room-images-integration-test-signing-key-only";
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/l1sAAAAASUVORK5CYII=");

    [Fact]
    public async Task Upload_requires_authentication_and_scopes_managers_to_assigned_properties()
    {
        using var factory = new RoomImagesFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Upload(client)).StatusCode);
        Authenticate(factory, client, 104, "Customer");
        Assert.Equal(HttpStatusCode.Forbidden, (await Upload(client)).StatusCode);
        Authenticate(factory, client, 103, "Manager");
        Assert.Equal(HttpStatusCode.Forbidden, (await Upload(client)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsync("/api/rooms/1/images/1/primary", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync("/api/rooms/1/images/1")).StatusCode);
        Assert.Equal(0, factory.Storage.UploadCount);
        Authenticate(factory, client, 102, "Manager");
        Assert.Equal(HttpStatusCode.OK, (await Upload(client)).StatusCode);
        Assert.Equal(1, factory.Storage.UploadCount);
    }

    [Fact]
    public async Task Upload_primary_switch_delete_and_public_gallery_work_together()
    {
        using var factory = new RoomImagesFactory();
        using var client = factory.CreateClient();
        Authenticate(factory, client, 101, "Admin");
        var firstResponse = await Upload(client);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var first = (await firstResponse.Content.ReadFromJsonAsync<RoomImageDto>())!;
        Assert.True(first.IsPrimary);
        var second = (await (await Upload(client)).Content.ReadFromJsonAsync<RoomImageDto>())!;
        Assert.False(second.IsPrimary);

        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PutAsync($"/api/rooms/1/images/{second.RoomImageId}/primary", null)).StatusCode);
        var images = (await client.GetFromJsonAsync<List<RoomImageDto>>("/api/rooms/1/images"))!;
        Assert.Equal(second.RoomImageId, Assert.Single(images, image => image.IsPrimary).RoomImageId);

        // Public responses show uploaded public images, but never legacy signed URLs.
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.RoomImages.Add(new RoomImages { RoomId = 1, ImageUrl = "https://project.supabase.co/storage/v1/object/sign/private/secret.png?token=secret" });
            await context.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = null;
        using var detail = JsonDocument.Parse(await client.GetStringAsync("/api/public/rooms/1"));
        Assert.Equal(new[] { second.ImageUrl, first.ImageUrl }, detail.RootElement.GetProperty("images").EnumerateArray().Select(image => image.GetString()));
        Assert.DoesNotContain("token=", detail.RootElement.GetRawText());
        using var listing = JsonDocument.Parse(await client.GetStringAsync("/api/public/properties/1/rooms"));
        Assert.Equal(second.ImageUrl, listing.RootElement.GetProperty("items")[0].GetProperty("primaryImageUrl").GetString());

        Authenticate(factory, client, 102, "Manager");
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/rooms/1/images/{second.RoomImageId}")).StatusCode);
        Assert.Contains(second.ImageUrl, factory.Storage.Deleted);
        images = (await client.GetFromJsonAsync<List<RoomImageDto>>("/api/rooms/1/images"))!;
        Assert.Equal(first.RoomImageId, Assert.Single(images, image => image.IsPrimary).RoomImageId);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsync("/api/rooms/1/images/999/primary", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync("/api/rooms/2/images/1")).StatusCode);
    }

    [Theory]
    [InlineData("fake.png", "image/png", false)]
    [InlineData("room.svg", "image/svg+xml", false)]
    [InlineData("room.jpg", "image/jpeg", true)]
    public async Task Upload_rejects_invalid_content_or_mismatched_type(string name, string type, bool usePng)
    {
        using var factory = new RoomImagesFactory();
        using var client = factory.CreateClient();
        Authenticate(factory, client, 101, "Admin");
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(client, usePng ? Png : [1, 2, 3], name, type)).StatusCode);
        Assert.Equal(0, factory.Storage.UploadCount);
    }

    [Fact]
    public async Task Upload_rejects_empty_oversized_missing_files_and_unknown_rooms()
    {
        using var factory = new RoomImagesFactory();
        using var client = factory.CreateClient();
        Authenticate(factory, client, 101, "Admin");
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(client, [])).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(client, new byte[5 * 1024 * 1024 + 1])).StatusCode);
        using var emptyForm = new MultipartFormDataContent();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/rooms/1/images/upload", emptyForm)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Upload(client, roomId: 999)).StatusCode);
        Assert.Equal(0, factory.Storage.UploadCount);
    }

    [Fact]
    public async Task Removing_a_reused_url_keeps_the_object_needed_by_the_original_image()
    {
        using var factory = new RoomImagesFactory();
        using var client = factory.CreateClient();
        Authenticate(factory, client, 101, "Admin");
        var original = (await (await Upload(client)).Content.ReadFromJsonAsync<RoomImageDto>())!;
        var linkedResponse = await client.PostAsJsonAsync("/api/rooms/1/images", new AddRoomImageDto(original.ImageUrl, false));
        var linked = (await linkedResponse.Content.ReadFromJsonAsync<RoomImageDto>())!;
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/rooms/1/images/{linked.RoomImageId}")).StatusCode);
        Assert.Empty(factory.Storage.Deleted);
        Assert.Single((await client.GetFromJsonAsync<List<RoomImageDto>>("/api/rooms/1/images"))!);
    }

    [Fact]
    public async Task Storage_failure_returns_502_without_creating_a_row_and_delete_can_be_retried()
    {
        using var factory = new RoomImagesFactory();
        using var client = factory.CreateClient();
        Authenticate(factory, client, 101, "Admin");
        factory.Storage.Fail = true;
        Assert.Equal(HttpStatusCode.BadGateway, (await Upload(client)).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<RoomImageDto>>("/api/rooms/1/images"))!);
        factory.Storage.Fail = false;
        var image = (await (await Upload(client)).Content.ReadFromJsonAsync<RoomImageDto>())!;
        factory.Storage.Fail = true;
        Assert.Equal(HttpStatusCode.BadGateway, (await client.DeleteAsync($"/api/rooms/1/images/{image.RoomImageId}")).StatusCode);
        Assert.Single((await client.GetFromJsonAsync<List<RoomImageDto>>("/api/rooms/1/images"))!);
    }

    private static async Task<HttpResponseMessage> Upload(HttpClient client, byte[]? content = null,
        string name = "room.png", string type = "image/png", int roomId = 1)
    {
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(content ?? Png);
        file.Headers.ContentType = new MediaTypeHeaderValue(type);
        form.Add(file, "file", name);
        form.Add(new StringContent("false"), "isPrimary");
        return await client.PostAsync($"/api/rooms/{roomId}/images/upload", form);
    }

    private static void Authenticate(RoomImagesFactory factory, HttpClient client, int userId, string role)
    {
        var settings = factory.Services.GetRequiredService<JwtSettings>();
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience,
            [new Claim("sub", userId.ToString()), new Claim(ClaimTypes.Role, role)],
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }

    private sealed class FakeStorage : IRoomImageStorage
    {
        public bool Fail { get; set; }
        public int UploadCount { get; private set; }
        public List<string> Deleted { get; } = [];
        public Task<string> UploadAsync(RoomImageInput image, CancellationToken cancellationToken = default)
        {
            if (Fail) throw new RoomImageStorageException();
            UploadCount++;
            return Task.FromResult($"https://project.supabase.co/storage/v1/object/public/room-images/rooms/{Guid.NewGuid():N}.png");
        }
        public Task DeleteAsync(string imageUrl, CancellationToken cancellationToken = default)
        {
            if (Fail) throw new RoomImageStorageException();
            Deleted.Add(imageUrl);
            return Task.CompletedTask;
        }
    }

    private sealed class RoomImagesFactory : WebApplicationFactory<RoomsController>
    {
        public FakeStorage Storage { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = Key, ["Jwt:Issuer"] = "test-issuer", ["Jwt:Audience"] = "test-audience",
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused",
                ["Supabase:Url"] = "https://project.supabase.co"
            }));
            var databaseName = $"room-images-{Guid.NewGuid()}";
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
                services.RemoveAll<IRoomImageStorage>();
                services.AddSingleton<IRoomImageStorage>(Storage);
            });
        }

        protected override void ConfigureClient(HttpClient client)
        {
            base.ConfigureClient(client);
            using var scope = Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Roles.AddRange(new Roles { RoleId = 1, RoleName = "Admin" }, new Roles { RoleId = 2, RoleName = "Manager" }, new Roles { RoleId = 3, RoleName = "Customer" });
            context.Users.AddRange(Enumerable.Range(101, 4).Select(id => new Users
            {
                UserId = id, RoleId = id == 101 ? 1 : id == 104 ? 3 : 2,
                FullName = $"User {id}", Email = $"user{id}@example.test", IsActive = true
            }));
            context.Properties.Add(new Property { PropertyId = 1, OwnerId = 101, PropertyName = "Test House", AddressText = "Test address", City = "HCM" });
            context.RoomTypes.Add(new RoomTypes { RoomTypeId = 1, TypeName = "Studio", Price = 3000000 });
            context.Rooms.Add(new Rooms { RoomId = 1, PropertyId = 1, RoomTypeId = 1, RoomCode = "A101", AreaSqm = 20, Status = "Available" });
            context.PropertyManagerAssignments.Add(new PropertyManagerAssignment { PropertyId = 1, ManagerId = 102 });
            context.SaveChanges();
        }
    }
}
