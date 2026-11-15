using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using PRN232.Infrastructure.Storage;
using Xunit;

namespace PRN232.Application.Tests;

// Rebuild 3.5b: contract evidence lives in a private bucket; the read link handed to users must point at
// our own Supabase project, never at a host chosen by the storage response.
public sealed class SupabaseContractEvidenceStorageTests
{
    [Theory]
    [InlineData("/object/sign/contract-evidence/contracts/1/movein/a.png?token=abc")]
    [InlineData("/storage/v1/object/sign/contract-evidence/contracts/1/movein/a.png?token=abc")]
    public async Task Signed_links_are_resolved_against_the_configured_project(string signedUrl)
    {
        var storage = Storage(signedUrl, out var handler);

        var url = await storage.GetReadUrlAsync("contracts/1/movein/a.png");

        Assert.Equal("https://project.supabase.co/storage/v1/object/sign/contract-evidence/contracts/1/movein/a.png?token=abc", url);
        Assert.Equal("https://project.supabase.co/storage/v1/object/sign/contract-evidence/contracts/1/movein/a.png",
            handler.LastRequest!.RequestUri!.ToString());
    }

    [Theory]
    [InlineData("https://evil.example/object/sign/contract-evidence/a.png?token=abc")]
    [InlineData("//evil.example/object/sign/contract-evidence/a.png")]
    [InlineData("/object/sign/other-bucket/a.png?token=abc")]
    [InlineData("/object/sign/contract-evidence/../room-images/a.png")]
    [InlineData("")]
    public async Task Unexpected_signed_links_are_rejected(string signedUrl)
    {
        var storage = Storage(signedUrl, out _);

        await Assert.ThrowsAsync<InvalidOperationException>(() => storage.GetReadUrlAsync("contracts/1/movein/a.png"));
    }

    private static SupabaseContractEvidenceStorage Storage(string signedUrl, out StubHandler handler)
    {
        handler = new StubHandler($"{{\"signedURL\":\"{signedUrl}\"}}");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Supabase:Url"] = "https://project.supabase.co",
            ["Supabase:ServiceRoleKey"] = "service-role-key",
            ["Supabase:ContractEvidenceBucket"] = "contract-evidence"
        }).Build();
        return new SupabaseContractEvidenceStorage(new HttpClient(handler), configuration);
    }

    private sealed class StubHandler(string body) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
