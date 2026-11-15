using System.Text.RegularExpressions;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
namespace PRN232.Application.Services;
public sealed class PropertyLocationPreviewService(IPropertyGeocodingService geocoding, IAdministrativeDivisionCatalog catalog)
    : IPropertyLocationPreviewService
{
    public async Task<PropertyLocationPreviewDto> PreviewAsync(PropertyLocationPreviewRequest request, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(request.AddressText) || request.AddressText.Length > 250 ||
            string.IsNullOrWhiteSpace(request.ProvinceCode) || string.IsNullOrWhiteSpace(request.WardCode))
            throw new PropertyValidationException("Hãy nhập địa chỉ tối đa 250 ký tự và chọn tỉnh/phường.");
        var province = catalog.GetProvince(request.ProvinceCode);
        var ward = catalog.GetCommune(request.WardCode);
        if (province is null || ward is null || ward.ParentProvinceCode != province.Code)
            throw new PropertyValidationException("Mã tỉnh/phường không hợp lệ hoặc không cùng địa giới.");
        var original = request.AddressText.Trim();
        var normalized = Regex.Replace(original, @"\s+", " ");
        normalized = Regex.Replace(normalized, @"^(\d+[\p{L}\d/\-]*)\s*,\s*", "$1 ");
        var parsed = Regex.Match(normalized, @"^(?<number>\d+[\p{L}\d/\-]*)\s+(?<street>.+)$");
        var number = parsed.Success ? parsed.Groups["number"].Value : null;
        var street = parsed.Success ? parsed.Groups["street"].Value : normalized;
        var stripped = Regex.IsMatch(street, @"^đường\s+(?!số(?:\s|$)|\d)", RegexOptions.IgnoreCase)
            ? Regex.Replace(street, @"^đường\s+", "", RegexOptions.IgnoreCase) : street;
        string Full(string address) => $"{address}, {ward.Name}, {province.Name}, Việt Nam";
        var variants = new List<PropertyGeocodingQuery> {
            new(Full(original), street, number, ward.Name, province.Name),
            new(Full(normalized), street, number, ward.Name, province.Name),
            new(Full(number is null ? stripped : number + " " + stripped), stripped, number, ward.Name, province.Name),
            // Broaden only to the same street/administrative area. Never manufacture nearby house numbers.
            new(Full(stripped), stripped, null, ward.Name, province.Name) };
        variants = variants.DistinctBy(x => x.FullAddress).ToList();
        var expected = new PropertyGeocodingQuery(Full(normalized), street, number, ward.Name, province.Name);
        var results = new List<PropertyLocationPreviewCandidate>();
        var providers = new List<PropertyLocationProviderStatus>();
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(token);
        budget.CancelAfter(TimeSpan.FromSeconds(25));
        var anyResponse = false;
        foreach (var provider in new[] { "Nominatim", "Photon" })
        {
            // Photon is structured: comma-only changes in FullAddress do not change its request.
            var queries = provider == "Photon" ? variants.DistinctBy(x => (x.StreetName, x.HouseNumber)) : variants;
            foreach (var query in queries)
            {
                token.ThrowIfCancellationRequested();
                if (budget.IsCancellationRequested) { providers.Add(new(provider,"BudgetExceeded")); break; }
                try
                {
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(budget.Token);
                    deadline.CancelAfter(TimeSpan.FromSeconds(4));
                    var found = provider == "Nominatim"
                        ? await geocoding.FindNominatimCandidatesAsync(query, deadline.Token)
                        : await geocoding.FindPhotonCandidatesAsync(query, deadline.Token);
                    anyResponse = true; providers.Add(new(provider, found.Count == 0 ? "Empty" : "Success"));
                    foreach (var candidate in found)
                    {
                        var item = Classify(candidate, expected);
                        if (item is not null) results.Add(item);
                    }
                    if (results.Any(x => x.MatchLevel == "Match")) break;
                }
                catch (PropertyGeocodingRateLimitedException) { providers.Add(new(provider,"RateLimited")); break; }
                catch (Exception e) when (!token.IsCancellationRequested && e is PropertyGeocodingException or HttpRequestException or OperationCanceledException)
                {
                    providers.Add(new(provider,e is OperationCanceledException ? "Timeout" : "Unavailable"));
                    break; // Do not repeatedly retry address variants against a failing provider.
                }
            }
            if (results.Any(x => x.MatchLevel == "Match")) break;
        }
        var candidates = results.OrderByDescending(x => x.MatchLevel == "Match")
            .DistinctBy(x => (x.Location.Coordinates, x.Location.HouseNumber, x.Location.StreetName)).Take(10).ToList();
        // Equal evidence at distant coordinates must remain suggestions, never a selected match.
        var matches = candidates.Where(x => x.MatchLevel == "Match").ToList();
        if (matches.Any(a => matches.Any(b => Distance(a.Location.Coordinates,b.Location.Coordinates) > 100)))
            candidates = candidates.Select(x => x.MatchLevel == "Match" ? x with { MatchLevel="Suggestion", Reasons=["spatially_ambiguous"] } : x).ToList();
        var status = candidates.Any(x => x.MatchLevel == "Match") ? "Matched" : candidates.Count > 0 ? "Suggestions" : anyResponse ? "NoResults" : "Unavailable";
        return new(status, original, true, candidates, providers.Distinct().ToList());
    }
    private static PropertyLocationPreviewCandidate? Classify(AddressGeocodingCandidate candidate, PropertyGeocodingQuery expected)
    {
        var reasons = new List<string>();
        var checkedCandidate = candidate;
        var reason = PropertyLocationCandidatePolicy.GetCandidateRejectionReason(checkedCandidate, expected);
        if (reason == "ward_unverified")
        {
            reasons.Add(reason);
            checkedCandidate = checkedCandidate with { WardName = expected.WardName };
            reason = PropertyLocationCandidatePolicy.GetCandidateRejectionReason(checkedCandidate, expected);
        }
        if (reason is "house_number_mismatch" or "house_number_unverified")
        {
            reasons.Add(reason);
            checkedCandidate = checkedCandidate with { HouseNumber = expected.HouseNumber };
            reason = PropertyLocationCandidatePolicy.GetCandidateRejectionReason(checkedCandidate, expected);
        }
        if (reason is not null) return null; // Explicit wrong ward/province/street/country is never a match or suggestion.
        if (candidate.Precision is not ("house" or "building") || expected.HouseNumber is null)
            reasons.Add("approximate_location");
        return new(candidate, reasons.Count == 0 ? "Match" : "Suggestion", reasons);
    }
    private static double Distance(AddressCoordinates a, AddressCoordinates b)
    {
        var lat = (double)(b.Latitude-a.Latitude)*Math.PI/180;
        var lon = (double)(b.Longitude-a.Longitude)*Math.PI/180;
        var h = Math.Pow(Math.Sin(lat/2),2)+Math.Cos((double)a.Latitude*Math.PI/180)*Math.Cos((double)b.Latitude*Math.PI/180)*Math.Pow(Math.Sin(lon/2),2);
        return 6371000*2*Math.Asin(Math.Sqrt(Math.Clamp(h,0,1)));
    }
}
