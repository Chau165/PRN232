using System.Net;
using System.Net.Http.Json;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.Services;
using Xunit;
using static PRN232.Application.Tests.BookingsApiTests;
namespace PRN232.Application.Tests;
public class PropertyLocationPreviewTests
{
    private sealed class Catalog : IAdministrativeDivisionCatalog
    {
        public AdministrativeDivisionReadModel? GetProvince(string code) => code=="79" ? new("79","Thành phố Hồ Chí Minh","province",null) : null;
        public AdministrativeDivisionReadModel? GetCommune(string code) => code=="01" ? new("01","Phường Thủ Đức","ward","79") : null;
        public IReadOnlyList<AdministrativeDivisionReadModel> GetProvinces()=>[GetProvince("79")!];
        public IReadOnlyList<AdministrativeDivisionReadModel> GetCommunes(string code)=>[GetCommune("01")!];
    }
    private sealed class Gateway : IPropertyGeocodingService
    {
        public List<PropertyGeocodingQuery> Queries=[];
        public Exception? NominatimError;
        public Exception? PhotonError;
        public IReadOnlyList<AddressGeocodingCandidate> Candidates=[];
        public Task<IReadOnlyList<AddressGeocodingCandidate>> FindNominatimCandidatesAsync(PropertyGeocodingQuery q,CancellationToken token=default)
        { Queries.Add(q);token.ThrowIfCancellationRequested();if(NominatimError is not null)throw NominatimError;return Task.FromResult<IReadOnlyList<AddressGeocodingCandidate>>([]); }
        public Task<IReadOnlyList<AddressGeocodingCandidate>> FindPhotonCandidatesAsync(PropertyGeocodingQuery q,CancellationToken token=default)
        { Queries.Add(q);token.ThrowIfCancellationRequested();if(PhotonError is not null)throw PhotonError;return Task.FromResult(Candidates); }
    }
    private static AddressGeocodingCandidate Candidate(string? ward=null,string? house=null,string precision="street") => new(new(10.85m,106.77m),"Lê Văn Việt, Hồ Chí Minh","Photon",precision,house,"Lê Văn Việt",ward,"Thành phố Hồ Chí Minh","vn");
    private static PropertyLocationPreviewRequest Request(string address="121, đường Lê Văn Việt")=>new(address,"79","01");
    [Fact]
    public async Task Timeout_does_not_hide_photon_suggestions_and_street_is_not_a_house_match()
    {
        var gateway=new Gateway { NominatimError=new TaskCanceledException(),Candidates=[Candidate()] };
        var result=await new PropertyLocationPreviewService(gateway,new Catalog()).PreviewAsync(Request(),default);
        Assert.Equal("Suggestions",result.Status);var item=Assert.Single(result.Candidates);
        Assert.Contains("ward_unverified",item.Reasons);Assert.Contains("approximate_location",item.Reasons);
        Assert.Equal("Suggestion",item.MatchLevel);Assert.Contains(result.Providers,x=>x.Provider=="Nominatim" && x.Status=="Timeout");
        Assert.Equal("121, đường Lê Văn Việt",result.OriginalAddress);
    }
    [Theory]
    [InlineData("ward")][InlineData("street")][InlineData("country")][InlineData("province")]
    public async Task Explicit_geographic_mismatch_is_not_suggested(string field)
    {
        var candidate=Candidate("Phường Thủ Đức","121","house");
        candidate=field switch {"ward"=>candidate with { WardName="Phường khác" },"street"=>candidate with { StreetName="Đường khác" },
            "country"=>candidate with { CountryCode="us" },_=>candidate with { ProvinceName="Hà Nội" }};
        var result=await new PropertyLocationPreviewService(new Gateway{Candidates=[candidate]},new Catalog()).PreviewAsync(Request(),default);
        Assert.Equal("NoResults",result.Status);Assert.Empty(result.Candidates);
    }
    [Fact]
    public async Task Uses_bounded_variants_and_preserves_numbered_street_names()
    {
        var gateway=new Gateway();var service=new PropertyLocationPreviewService(gateway,new Catalog());
        await service.PreviewAsync(Request(),default);
        Assert.Contains(gateway.Queries,q=>q.FullAddress.StartsWith("121 đường Lê Văn Việt,"));
        Assert.Contains(gateway.Queries,q=>q.FullAddress.StartsWith("121 Lê Văn Việt,"));
        Assert.Contains(gateway.Queries,q=>q.HouseNumber==null && q.StreetName=="Lê Văn Việt");
        Assert.InRange(gateway.Queries.Count,1,8);gateway.Queries.Clear();
        await service.PreviewAsync(Request("12 Đường số 8"),default);
        Assert.All(gateway.Queries,q=>Assert.Equal("Đường số 8",q.StreetName));
    }
    [Fact]
    public async Task Real_nearby_house_is_a_suggestion_and_exact_house_is_a_match()
    {
        var gateway=new Gateway{Candidates=[Candidate("Phường Thủ Đức","123","house")]};
        var service=new PropertyLocationPreviewService(gateway,new Catalog());
        var result=await service.PreviewAsync(Request(),default);
        Assert.Equal("Suggestions",result.Status);Assert.Equal("123",Assert.Single(result.Candidates).Location.HouseNumber);
        gateway.Candidates=[Candidate("Phường Thủ Đức","121","house")];
        Assert.Equal("Matched",(await service.PreviewAsync(Request(),default)).Status);
    }
    [Fact]
    public async Task Distant_equal_house_matches_are_ambiguous()
    {
        var candidate=Candidate("Phường Thủ Đức","121","house");
        var gateway=new Gateway{Candidates=[candidate,candidate with {Coordinates=new(10.95m,106.77m)}]};
        var result=await new PropertyLocationPreviewService(gateway,new Catalog()).PreviewAsync(Request(),default);
        Assert.Equal("Suggestions",result.Status);Assert.All(result.Candidates,x=>Assert.Contains("spatially_ambiguous",x.Reasons));
    }
    [Fact]
    public async Task Unavailable_is_distinct_from_empty_and_caller_cancellation_propagates()
    {
        var gateway=new Gateway{NominatimError=new HttpRequestException(),PhotonError=new HttpRequestException()};
        var service=new PropertyLocationPreviewService(gateway,new Catalog());
        Assert.Equal("Unavailable",(await service.PreviewAsync(Request(),default)).Status);
        gateway.NominatimError=null;gateway.PhotonError=null;
        Assert.Equal("NoResults",(await service.PreviewAsync(Request(),default)).Status);
        await Assert.ThrowsAsync<OperationCanceledException>(()=>service.PreviewAsync(Request(),new CancellationToken(true)));
        await Assert.ThrowsAsync<PropertyValidationException>(()=>service.PreviewAsync(Request() with {WardCode="invalid"},default));
    }
    [Theory]
    [InlineData(null,401)] [InlineData("customer@example.test",403)] [InlineData("manager@example.test",403)] [InlineData("admin@example.test",400)]
    public async Task Preview_endpoint_requires_admin_and_valid_input(string? email,int status)
    {
        using var factory=new BookingsApiFactory();using var client=factory.CreateClient();
        if(email is not null)await LoginAsync(client,email);
        var response=await client.PostAsJsonAsync("/api/properties/location-preview",new {addressText="",provinceCode="",wardCode=""});
        Assert.Equal((HttpStatusCode)status,response.StatusCode);
    }
}
