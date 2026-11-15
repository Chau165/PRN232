using PRN232.Application.DTOs;
using PRN232.Application.Policies;
using Xunit;
namespace PRN232.Application.Tests;
// Provider evidence still matters to the optional preview endpoint, not to writes using an Admin pin.
public class PropertyGeocodingSelectionTests
{
    private static AddressGeocodingCandidate Candidate()=>new(new(10.85m,106.77m),"Lê Văn Việt","Photon","house","121","Lê Văn Việt","Phường Thủ Đức","Hồ Chí Minh","vn");
    private static PropertyGeocodingQuery Query()=>new("121 Lê Văn Việt","Lê Văn Việt","121","Phường Thủ Đức","Hồ Chí Minh");
    [Fact] public void Matching_evidence_is_accepted()=>Assert.Null(PropertyLocationCandidatePolicy.GetCandidateRejectionReason(Candidate(),Query()));
    [Fact] public void Missing_ward_is_distinct_from_wrong_ward()
    {
        Assert.Equal("ward_unverified",PropertyLocationCandidatePolicy.GetCandidateRejectionReason(Candidate() with {WardName=null},Query()));
        Assert.Equal("ward_mismatch",PropertyLocationCandidatePolicy.GetCandidateRejectionReason(Candidate() with {WardName="Phường khác"},Query()));
    }
    [Fact] public void Wrong_house_and_street_are_distinct()
    {
        Assert.Equal("house_number_mismatch",PropertyLocationCandidatePolicy.GetCandidateRejectionReason(Candidate() with {HouseNumber="122"},Query()));
        Assert.Equal("street_mismatch",PropertyLocationCandidatePolicy.GetCandidateRejectionReason(Candidate() with {StreetName="Lê Lợi"},Query()));
    }
}
