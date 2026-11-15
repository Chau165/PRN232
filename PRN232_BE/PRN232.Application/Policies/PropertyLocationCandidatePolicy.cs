using PRN232.Application.DTOs;
using PRN232.Application.Normalizers;
namespace PRN232.Application.Policies;
public static class PropertyLocationCandidatePolicy
{
    public static string? GetCandidateRejectionReason(
        AddressGeocodingCandidate candidate,
        PropertyGeocodingQuery query)
    {
        if (!IsWithinVietnam(candidate))
        {
            return "country_or_coordinate_mismatch";
        }

        if (candidate.ProvinceName is not null)
        {
            if (!NamesMatch(candidate.ProvinceName, query.ProvinceName))
            {
                return "province_mismatch";
            }
        }
        else if (candidate.ProvinceAreas?.Any(area => NamesMatch(area.Name, query.ProvinceName)) != true)
        {
            return "province_unverified";
        }

        if (candidate.WardName is not null)
        {
            if (!NamesMatch(candidate.WardName, query.WardName))
            {
                return "ward_mismatch";
            }
        }
        else if (candidate.AdministrativeAreas?.Any(area => NamesMatch(area.Name, query.WardName)) != true)
        {
            return "ward_unverified";
        }

        if (string.IsNullOrWhiteSpace(candidate.StreetName))
        {
            return "street_missing";
        }

        if (!string.Equals(
                NormalizeStreetName(candidate.StreetName),
                NormalizeStreetName(query.StreetName),
                StringComparison.Ordinal))
        {
            return "street_mismatch";
        }

        if (candidate.HouseNumber is not null)
        {
            if (query.HouseNumber is null ||
                !string.Equals(
                    NormalizeHouseNumber(candidate.HouseNumber),
                    NormalizeHouseNumber(query.HouseNumber),
                    StringComparison.Ordinal))
            {
                return "house_number_mismatch";
            }
        }

        if (query.HouseNumber is not null &&
            (candidate.Precision is "building" or "house") &&
            candidate.HouseNumber is null)
        {
            return "house_number_unverified";
        }

        if (PrecisionRank(candidate.Precision) < PrecisionRank("street"))
        {
            return "object_precision_too_low";
        }

        return null;
    }
    public static bool NamesMatch(string? candidateName, string expectedName)
    {
        return candidateName is not null &&
            PublicAreaNameNormalizer.Normalize(candidateName) ==
            PublicAreaNameNormalizer.Normalize(expectedName);
    }
    public static string NormalizeStreetName(string value)
    {
        var tokens = PublicAreaNameNormalizer.Normalize(value)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        while (tokens.Count > 1 && tokens[0] is "duong" or "street" or "road" or "st" or "rd")
        {
            tokens.RemoveAt(0);
        }

        return string.Join(' ', tokens);
    }
    public static string NormalizeHouseNumber(string value)
    {
        return string.Concat(value
            .Trim()
            .ToUpperInvariant()
            .Where(character => char.IsLetterOrDigit(character) || character is '/' or '-'));
    }
    public static bool IsWithinVietnam(AddressGeocodingCandidate candidate)
    {
        var coordinates = candidate.Coordinates;
        // This broad envelope includes mainland Vietnam and the Hoang Sa/Truong Sa archipelagos.
        // It rejects implausible country-level coordinates; it does not verify ward membership.
        var coordinatesWithinBroadEnvelope = coordinates.Latitude is >= 6.5m and <= 24m &&
            coordinates.Longitude is >= 102m and <= 117.5m;
        return coordinatesWithinBroadEnvelope &&
            string.Equals(candidate.CountryCode, "vn", StringComparison.OrdinalIgnoreCase);
    }
    public static int PrecisionRank(string precision)
    {
        return precision switch
        {
            "house" => 5,
            "building" => 4,
            "street" => 3,
            "commune" => 2,
            "province" => 1,
            _ => 0
        };
    }
}
