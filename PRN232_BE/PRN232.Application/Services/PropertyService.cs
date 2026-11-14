using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Normalizers;
using PRN232.Application.ReadModels;

namespace PRN232.Application.Services;

public class PropertyService : IPropertyService
{
    private const int MaxImageBytes = 5 * 1024 * 1024;
    // Split OSM ways on the same street are usually clustered; farther matches need manual correction.
    private const double MaxStreetCandidateSpreadMeters = 600;
    private const double MaxBuildingCandidateSpreadMeters = 100;
    private static readonly Regex HouseNumberAndStreetPattern = new(
        @"^\s*(?<number>\d+[A-Za-z]?(?:\s*[/\-]\s*\d+[A-Za-z]?)*)\s*(?:,\s*|\s+)(?<street>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IPropertyRepository _repository;
    private readonly IAdministrativeDivisionCatalog? _administrativeDivisionCatalog;
    private readonly IPropertyGeocodingService? _geocodingService;
    private readonly IPropertyImageStorage? _imageStorage;
    private readonly ILogger<PropertyService> _logger;

    public PropertyService(
        IPropertyRepository repository,
        IAdministrativeDivisionCatalog? administrativeDivisionCatalog = null,
        IPropertyGeocodingService? geocodingService = null,
        IPropertyImageStorage? imageStorage = null,
        ILogger<PropertyService>? logger = null)
    {
        _repository = repository;
        _administrativeDivisionCatalog = administrativeDivisionCatalog;
        _geocodingService = geocodingService;
        _imageStorage = imageStorage;
        _logger = logger ?? NullLogger<PropertyService>.Instance;
    }

    public async Task<List<PropertyDto>> GetAllAsync(int? ownerId, int? managerId)
    {
        var properties = await _repository.GetAllAsync(ownerId, managerId);
        return properties.Select(ConvertToDto).ToList();
    }

    public async Task<PropertyDto?> GetByIdAsync(int id, int requestingUserId, bool isManager)
    {
        var property = await _repository.GetByIdAsync(id);
        if (property is null)
        {
            return null;
        }
        if (isManager && !await _repository.IsManagerAssignedAsync(id, requestingUserId))
        {
            throw new PropertyForbiddenException();
        }
        return ConvertToDto(property);
    }

    //NGHIA
    public async Task<List<PropertyServiceRateDto>?> GetServiceRatesAsync(
        int id, DateOnly? month, int requestingUserId, bool isAdmin, CancellationToken cancellationToken)
    {
        if (month.HasValue && month.Value.Day != 1)
        {
            throw new PropertyValidationException("Month must be the first day of a month (yyyy-MM-01).");
        }

        var property = await _repository.GetByIdAsync(id);
        if (property is null)
        {
            return null;
        }
        if (!isAdmin && !await _repository.IsManagerAssignedAsync(id, requestingUserId))
        {
            throw new PropertyForbiddenException();
        }

        var rates = await _repository.GetServiceRatesAsync(id, month, cancellationToken);
        return rates.Select(rate => new PropertyServiceRateDto(
            rate.RateId, rate.PropertyId, rate.ServiceId, rate.ServiceName, rate.Price,
            rate.UnitCode, rate.BillingBasis, rate.EffectiveFrom, rate.EffectiveTo)).ToList();
    }

    public async Task<PropertyServiceRateDto> CreateServiceRateAsync(
        int id, CreatePropertyServiceRateDto dto, CancellationToken cancellationToken)
    {
        if (dto.ServiceId <= 0 || dto.Price < 0 || dto.Price > 9999999999999999.99m ||
            decimal.Round(dto.Price, 2) != dto.Price)
        {
            throw new PropertyValidationException("ServiceId must be positive; price must be nonnegative with at most two decimal places and fit numeric(18,2).");
        }
        if (dto.EffectiveFrom == default || dto.EffectiveFrom.Day != 1 ||
            (dto.EffectiveTo.HasValue && (dto.EffectiveTo.Value.Day != 1 || dto.EffectiveTo <= dto.EffectiveFrom)))
        {
            throw new PropertyValidationException("Effective dates must be month starts; EffectiveTo must be after EffectiveFrom.");
        }
        var validBasis = (dto.UnitCode, dto.BillingBasis) is
            ("kwh", "usage") or ("m3", "usage") or ("room_month", "fixed_room") or
            ("person_month", "occupant") or ("vehicle_month", "vehicle");
        if (!validBasis)
        {
            throw new PropertyValidationException("Invalid unitCode/billingBasis pair.");
        }
        if (await _repository.GetByIdAsync(id) is null)
        {
            throw new PropertyRateNotFoundException("Property does not exist.");
        }
        if (!await _repository.ServiceExistsAsync(dto.ServiceId, cancellationToken))
        {
            throw new PropertyRateNotFoundException("Service does not exist.");
        }

        var rate = new PRN232.Domain.Entities.PropertyServiceRates
        {
            PropertyId = id, ServiceId = dto.ServiceId, Price = dto.Price,
            UnitCode = dto.UnitCode, BillingBasis = dto.BillingBasis,
            EffectiveFrom = dto.EffectiveFrom, EffectiveTo = dto.EffectiveTo
        };
        if (!await _repository.AddServiceRateIfNoOverlapAsync(rate, cancellationToken))
        {
            throw new PropertyRateConflictException("This period overlaps an existing rate for this property's service. Existing rates were not changed.");
        }
        var rates = await _repository.GetServiceRatesAsync(id, null, cancellationToken);
        var created = rates.Single(item => item.RateId == rate.RateId);
        return new PropertyServiceRateDto(created.RateId, created.PropertyId, created.ServiceId,
            created.ServiceName, created.Price, created.UnitCode, created.BillingBasis,
            created.EffectiveFrom, created.EffectiveTo);
    }

    //Nghia
    public async Task<PropertyDto> CreateAsync(
        CreatePropertyDto dto,
        int ownerId,
        bool isAdmin,
        PropertyImageInput? image = null,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin(isAdmin);
        ValidatePropertyText(dto.PropertyName, dto.AddressText);
        var (province, commune) = ResolveAdministrativeLocation(dto.ProvinceCode, dto.WardCode);
        ValidateStreetAddressDoesNotRepeatArea(dto.AddressText, commune.Name, province.Name);
        var coordinates = await GeocodeAddressCoordinatesAsync(
            dto.AddressText,
            commune.Name,
            province.Name,
            cancellationToken);
        string? uploadedImageUrl = null;
        try
        {
            uploadedImageUrl = await UploadImageAsync(image);
            var property = ConvertToEntity(
                dto,
                ownerId,
                commune.Name,
                province.Name,
                coordinates,
                uploadedImageUrl);
            var created = await _repository.AddAsync(property);
            await _repository.SaveChangesAsync();

            return ConvertToDto(await GetPropertyResponseAsync(created.PropertyId));
        }
        catch
        {
            await TryDeleteImageAsync(uploadedImageUrl);
            throw;
        }
    }

    public async Task<PropertyDto?> UpdateAsync(
        int id,
        UpdatePropertyDto dto,
        int requestingUserId,
        bool isAdmin,
        PropertyImageInput? image = null,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin(isAdmin);
        var entity = await _repository.GetTrackedByIdAsync(id);
        if (entity is null)
        {
            return null;
        }
        ValidatePropertyName(dto.PropertyName);
        var hasProvinceCode = !string.IsNullOrWhiteSpace(dto.ProvinceCode);
        var hasWardCode = !string.IsNullOrWhiteSpace(dto.WardCode);
        if (hasProvinceCode != hasWardCode)
        {
            throw new PropertyValidationException("Hãy chọn cả tỉnh/thành phố và phường/xã từ danh mục.");
        }

        var provinceName = dto.City.Trim();
        var wardName = dto.Ward.Trim();
        var districtName = dto.District.Trim();
        if (hasProvinceCode && hasWardCode)
        {
            var administrativeLocation = ResolveAdministrativeLocation(dto.ProvinceCode, dto.WardCode);
            provinceName = administrativeLocation.Province.Name;
            wardName = administrativeLocation.Commune.Name;
            districtName = string.Empty;
        }

        var addressUnchanged = IsSameAddress(
            entity,
            dto.AddressText,
            wardName,
            districtName,
            provinceName);
        var updateLocation = !addressUnchanged;
        AddressCoordinates coordinates;
        if (updateLocation)
        {
            if (!hasProvinceCode || !hasWardCode)
            {
                throw new PropertyValidationException("Địa chỉ mới cần chọn tỉnh/thành phố và phường/xã hiện hành.");
            }

            ValidateStreetAddress(dto.AddressText);
            ValidateStreetAddressDoesNotRepeatArea(dto.AddressText, wardName, provinceName);
            coordinates = await GeocodeAddressCoordinatesAsync(
                dto.AddressText,
                wardName,
                provinceName,
                cancellationToken);
        }
        else
        {
            coordinates = new AddressCoordinates(entity.Latitude, entity.Longitude);
        }

        var previousImageUrl = entity.PrimaryImageUrl;
        string? uploadedImageUrl = null;
        try
        {
            uploadedImageUrl = await UploadImageAsync(image);
            ApplyUpdateToEntity(
                dto,
                entity,
                wardName,
                districtName,
                provinceName,
                coordinates,
                updateLocation,
                uploadedImageUrl ?? previousImageUrl);

            await _repository.SaveChangesAsync();
            var updated = await _repository.GetByIdAsync(id);
            if (updated is null)
            {
                throw new InvalidOperationException("Updated property was not found.");
            }

            if (uploadedImageUrl is not null && previousImageUrl is not null)
            {
                await TryDeleteImageAsync(previousImageUrl);
            }
            return ConvertToDto(updated);
        }
        catch
        {
            await TryDeleteImageAsync(uploadedImageUrl);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(int id, int requestingUserId, bool isAdmin)
    {
        EnsureAdmin(isAdmin);
        var entity = await _repository.GetTrackedByIdAsync(id);
        if (entity is null)
        {
            return false;
        }
        if (await _repository.HasDependentsAsync(id))
        {
            throw new PropertyHasRoomsException();
        }

        _repository.Remove(entity);
        await _repository.SaveChangesAsync();
        return true;
    }

    private static bool IsSameAddress(
        PRN232.Domain.Entities.Property entity,
        string addressText,
        string ward,
        string district,
        string city) =>
        string.Equals(entity.AddressText.Trim(), addressText.Trim(), StringComparison.Ordinal) &&
        string.Equals(entity.Ward.Trim(), ward.Trim(), StringComparison.Ordinal) &&
        string.Equals(entity.District.Trim(), district.Trim(), StringComparison.Ordinal) &&
        string.Equals(entity.City.Trim(), city.Trim(), StringComparison.Ordinal);

    private static void EnsureAdmin(bool isAdmin)
    {
        if (!isAdmin)
        {
            throw new PropertyForbiddenException();
        }
    }

    private static void ValidatePropertyText(string propertyName, string addressText)
    {
        ValidatePropertyName(propertyName);
        ValidateStreetAddress(addressText);
    }

    private static void ValidatePropertyName(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            throw new PropertyValidationException("Tên khu trọ không được để trống.");
        }
    }

    private static void ValidateStreetAddress(string addressText)
    {
        if (string.IsNullOrWhiteSpace(addressText))
        {
            throw new PropertyValidationException("Số nhà và tên đường không được để trống.");
        }
    }

    private (AdministrativeDivisionReadModel Province, AdministrativeDivisionReadModel Commune)
        ResolveAdministrativeLocation(string? provinceCode, string? wardCode)
    {
        if (_administrativeDivisionCatalog is null)
        {
            throw new PropertyValidationException("Danh mục hành chính hiện không khả dụng.");
        }

        if (string.IsNullOrWhiteSpace(provinceCode) || string.IsNullOrWhiteSpace(wardCode))
        {
            throw new PropertyValidationException("Hãy chọn tỉnh/thành phố và phường/xã từ danh mục.");
        }

        var province = _administrativeDivisionCatalog.GetProvince(provinceCode);
        var commune = _administrativeDivisionCatalog.GetCommune(wardCode);
        if (province is null)
        {
            throw new PropertyValidationException("Mã tỉnh/thành phố không có trong danh mục hiện hành.");
        }

        if (commune is null)
        {
            throw new PropertyValidationException("Mã phường/xã không có trong danh mục hiện hành.");
        }

        if (!string.Equals(commune.ParentProvinceCode, province.Code, StringComparison.Ordinal))
        {
            throw new PropertyValidationException("Phường/xã không thuộc tỉnh/thành phố đã chọn.");
        }

        return (province, commune);
    }

    private static void ValidateStreetAddressDoesNotRepeatArea(
        string addressText,
        string wardName,
        string provinceName)
    {
        var administrativeNames = new HashSet<string>(StringComparer.Ordinal)
        {
            PublicAreaNameNormalizer.Normalize(wardName),
            PublicAreaNameNormalizer.Normalize(provinceName)
        };
        var repeatedName = addressText
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(PublicAreaNameNormalizer.Normalize)
            .Any(administrativeNames.Contains);
        if (repeatedName)
        {
            throw new PropertyValidationException(
                "Số nhà và tên đường chỉ nhập địa chỉ chi tiết; không lặp lại phường/xã hoặc tỉnh/thành phố.");
        }
    }

    private static string BuildFullAddress(string addressText, string wardName, string provinceName)
    {
        var parts = new[] { addressText.Trim(), wardName, provinceName, "Việt Nam" };
        var uniqueParts = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in parts)
        {
            var normalized = PublicAreaNameNormalizer.Normalize(part);
            if (normalized.Length > 0 && seen.Add(normalized))
            {
                uniqueParts.Add(part.Trim());
            }
        }

        return string.Join(", ", uniqueParts);
    }

    private async Task<AddressCoordinates> GeocodeAddressCoordinatesAsync(
        string addressText,
        string expectedWardName,
        string expectedProvinceName,
        CancellationToken cancellationToken)
    {
        if (_geocodingService is null)
        {
            throw new PropertyGeocodingException();
        }

        var parsedAddress = ParseStreetAddress(addressText);
        if (string.IsNullOrWhiteSpace(parsedAddress.StreetName))
        {
            throw new PropertyValidationException("Hãy nhập tên đường để xác định tọa độ khu trọ.");
        }

        var query = new PropertyGeocodingQuery(
            BuildFullAddress(addressText, expectedWardName, expectedProvinceName),
            parsedAddress.StreetName,
            parsedAddress.HouseNumber,
            expectedWardName,
            expectedProvinceName);

        var nominatimResult = await TryFindCandidatesAsync(
            _geocodingService.FindNominatimCandidatesAsync,
            query,
            cancellationToken);
        var nominatimSelection = SelectBestCandidate(nominatimResult.Candidates, query);
        if (nominatimSelection.Candidate is not null)
        {
            LogSelectedCandidate(nominatimSelection.Candidate);
            return nominatimSelection.Candidate.Coordinates;
        }

        _logger.LogInformation(
            "Nominatim did not produce a trusted property coordinate; trying Photon. TraceId={TraceId} CandidateCount={CandidateCount} Ambiguous={Ambiguous}",
            Activity.Current?.TraceId.ToString(),
            nominatimResult.Candidates.Count,
            nominatimSelection.IsAmbiguous);

        var photonResult = await TryFindCandidatesAsync(
            _geocodingService.FindPhotonCandidatesAsync,
            query,
            cancellationToken);
        var photonSelection = SelectBestCandidate(photonResult.Candidates, query);
        if (photonSelection.Candidate is not null)
        {
            LogSelectedCandidate(photonSelection.Candidate);
            return photonSelection.Candidate.Coordinates;
        }

        if (nominatimResult.IsRateLimited || photonResult.IsRateLimited)
        {
            throw new PropertyGeocodingRateLimitedException();
        }

        if (!nominatimResult.IsAvailable || !photonResult.IsAvailable)
        {
            throw new PropertyGeocodingException();
        }

        if (nominatimSelection.IsAmbiguous || photonSelection.IsAmbiguous)
        {
            throw new PropertyValidationException(
                "Tìm thấy nhiều vị trí phù hợp nhưng cách nhau quá xa để xác định an toàn. Hãy kiểm tra lại số nhà và tên đường.");
        }

        if (nominatimResult.Candidates.Count == 0 && photonResult.Candidates.Count == 0)
        {
            throw new PropertyValidationException(
                "Không tìm thấy kết quả định vị cho địa chỉ đã nhập. Hãy kiểm tra số nhà, tên đường và địa giới hành chính.");
        }

        throw new PropertyValidationException(
            "Kết quả định vị không khớp đủ tin cậy với số nhà, tên đường hoặc phường/xã đã chọn.");
    }

    private async Task<ProviderCandidates> TryFindCandidatesAsync(
        Func<PropertyGeocodingQuery, CancellationToken, Task<IReadOnlyList<AddressGeocodingCandidate>>> findCandidatesAsync,
        PropertyGeocodingQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var candidates = await findCandidatesAsync(query, cancellationToken);
            _logger.LogInformation(
                "Geocoding provider returned candidates. TraceId={TraceId} Provider={Provider} CandidateCount={CandidateCount}",
                Activity.Current?.TraceId.ToString(),
                candidates.FirstOrDefault()?.Provider,
                candidates.Count);
            return new ProviderCandidates(true, false, candidates);
        }
        catch (PropertyGeocodingRateLimitedException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ProviderCandidates(false, true, Array.Empty<AddressGeocodingCandidate>());
        }
        catch (PropertyGeocodingException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ProviderCandidates(false, false, Array.Empty<AddressGeocodingCandidate>());
        }
        catch (HttpRequestException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ProviderCandidates(false, false, Array.Empty<AddressGeocodingCandidate>());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ProviderCandidates(false, false, Array.Empty<AddressGeocodingCandidate>());
        }
    }

    private CandidateSelection SelectBestCandidate(
        IReadOnlyList<AddressGeocodingCandidate> candidates,
        PropertyGeocodingQuery query)
    {
        var acceptedCandidates = new List<CandidateEvaluation>();
        foreach (var candidate in candidates)
        {
            var rejectionReason = GetCandidateRejectionReason(candidate, query);
            _logger.LogInformation(
                "Geocoding candidate evaluated. TraceId={TraceId} Provider={Provider} OsmKey={OsmKey} OsmValue={OsmValue} Precision={Precision} Decision={Decision} RejectionReason={RejectionReason}",
                Activity.Current?.TraceId.ToString(),
                candidate.Provider,
                candidate.OsmKey,
                candidate.OsmValue,
                candidate.Precision,
                rejectionReason is null ? "accepted" : "rejected",
                rejectionReason);

            if (rejectionReason is null)
            {
                acceptedCandidates.Add(new CandidateEvaluation(
                    candidate,
                    GetCandidateQualityScore(candidate, query)));
            }
        }

        if (acceptedCandidates.Count == 0)
        {
            return new CandidateSelection(null, false);
        }

        var bestQualityScore = acceptedCandidates.Max(item => item.QualityScore);
        var bestCandidates = acceptedCandidates
            .Where(item => item.QualityScore == bestQualityScore)
            .OrderBy(item => item.Candidate.ProviderRank)
            .ToList();
        var maximumSpread = PrecisionRank(bestCandidates[0].Candidate.Precision) == PrecisionRank("street")
            ? MaxStreetCandidateSpreadMeters
            : MaxBuildingCandidateSpreadMeters;

        for (var firstIndex = 0; firstIndex < bestCandidates.Count; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < bestCandidates.Count; secondIndex++)
            {
                var distanceMeters = DistanceInMeters(
                    bestCandidates[firstIndex].Candidate.Coordinates,
                    bestCandidates[secondIndex].Candidate.Coordinates);
                if (distanceMeters > maximumSpread)
                {
                    _logger.LogWarning(
                        "Geocoding candidates with equal address evidence are spatially ambiguous. TraceId={TraceId} Provider={Provider} DistanceMeters={DistanceMeters} CandidateCount={CandidateCount}",
                        Activity.Current?.TraceId.ToString(),
                        bestCandidates[firstIndex].Candidate.Provider,
                        distanceMeters,
                        bestCandidates.Count);
                    return new CandidateSelection(null, true);
                }
            }
        }

        return new CandidateSelection(bestCandidates[0].Candidate, false);
    }

    private static string? GetCandidateRejectionReason(
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

    private static int GetCandidateQualityScore(
        AddressGeocodingCandidate candidate,
        PropertyGeocodingQuery query)
    {
        var precisionScore = PrecisionRank(candidate.Precision) * 100;
        var wardEvidenceScore = candidate.WardName is not null ? 20 : 10;
        var provinceEvidenceScore = candidate.ProvinceName is not null ? 10 : 0;
        var houseNumberScore = candidate.HouseNumber is not null && query.HouseNumber is not null ? 100 : 0;
        return precisionScore + wardEvidenceScore + provinceEvidenceScore + houseNumberScore;
    }

    private void LogSelectedCandidate(AddressGeocodingCandidate candidate)
    {
        _logger.LogInformation(
            "Property geocoding candidate selected. TraceId={TraceId} Provider={Provider} OsmKey={OsmKey} OsmValue={OsmValue} Precision={Precision}",
            Activity.Current?.TraceId.ToString(),
            candidate.Provider,
            candidate.OsmKey,
            candidate.OsmValue,
            candidate.Precision);
    }

    private static (string? HouseNumber, string StreetName) ParseStreetAddress(string addressText)
    {
        var match = HouseNumberAndStreetPattern.Match(addressText.Trim());
        if (!match.Success)
        {
            return (null, addressText.Trim());
        }

        return (
            match.Groups["number"].Value.Trim(),
            match.Groups["street"].Value.Trim());
    }

    private static bool NamesMatch(string? candidateName, string expectedName)
    {
        return candidateName is not null &&
            PublicAreaNameNormalizer.Normalize(candidateName) ==
            PublicAreaNameNormalizer.Normalize(expectedName);
    }

    private static string NormalizeStreetName(string value)
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

    private static string NormalizeHouseNumber(string value)
    {
        return string.Concat(value
            .Trim()
            .ToUpperInvariant()
            .Where(character => char.IsLetterOrDigit(character) || character is '/' or '-'));
    }

    private static double DistanceInMeters(AddressCoordinates first, AddressCoordinates second)
    {
        const double earthRadiusMeters = 6_371_000;
        var firstLatitude = (double)first.Latitude * Math.PI / 180;
        var secondLatitude = (double)second.Latitude * Math.PI / 180;
        var latitudeDifference = secondLatitude - firstLatitude;
        var longitudeDifference = ((double)second.Longitude - (double)first.Longitude) * Math.PI / 180;
        var haversine = Math.Pow(Math.Sin(latitudeDifference / 2), 2) +
            Math.Cos(firstLatitude) * Math.Cos(secondLatitude) *
            Math.Pow(Math.Sin(longitudeDifference / 2), 2);
        return earthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(haversine), Math.Sqrt(1 - haversine));
    }

    private static bool IsWithinVietnam(AddressGeocodingCandidate candidate)
    {
        var coordinates = candidate.Coordinates;
        // This broad envelope includes mainland Vietnam and the Hoang Sa/Truong Sa archipelagos.
        // It rejects implausible country-level coordinates; it does not verify ward membership.
        var coordinatesWithinBroadEnvelope = coordinates.Latitude is >= 6.5m and <= 24m &&
            coordinates.Longitude is >= 102m and <= 117.5m;
        return coordinatesWithinBroadEnvelope &&
            string.Equals(candidate.CountryCode, "vn", StringComparison.OrdinalIgnoreCase);
    }

    private static int PrecisionRank(string precision)
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

    private async Task<string?> UploadImageAsync(PropertyImageInput? image)
    {
        if (image is null)
        {
            return null;
        }
        ValidateImage(image);
        if (_imageStorage is null)
        {
            throw new PropertyImageStorageException();
        }
        try
        {
            return await _imageStorage.UploadAsync(image);
        }
        catch (PropertyImageStorageException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new PropertyImageStorageException();
        }
    }

    private async Task TryDeleteImageAsync(string? imageUrl)
    {
        if (imageUrl is null || _imageStorage is null)
        {
            return;
        }
        try
        {
            await _imageStorage.DeleteAsync(imageUrl);
        }
        catch
        {
            // Cleanup is best-effort so the original operation failure remains visible.
        }
    }

    private static PRN232.Domain.Entities.Property ConvertToEntity(
        CreatePropertyDto dto,
        int ownerId,
        string wardName,
        string provinceName,
        AddressCoordinates coordinates,
        string? imageUrl)
    {
        return new PRN232.Domain.Entities.Property
        {
            OwnerId = ownerId,
            PropertyName = dto.PropertyName.Trim(),
            AddressText = dto.AddressText.Trim(),
            Ward = wardName,
            District = string.Empty,
            City = provinceName,
            Latitude = coordinates.Latitude,
            Longitude = coordinates.Longitude,
            PrimaryImageUrl = imageUrl,
            Description = NormalizeOptional(dto.Description)
        };
    }

    private static void ApplyUpdateToEntity(
        UpdatePropertyDto dto,
        PRN232.Domain.Entities.Property entity,
        string wardName,
        string districtName,
        string provinceName,
        AddressCoordinates coordinates,
        bool updateLocation,
        string? imageUrl)
    {
        entity.PropertyName = dto.PropertyName.Trim();
        if (updateLocation)
        {
            entity.AddressText = dto.AddressText.Trim();
            entity.Ward = wardName;
            entity.District = districtName;
            entity.City = provinceName;
            entity.Latitude = coordinates.Latitude;
            entity.Longitude = coordinates.Longitude;
        }
        entity.PrimaryImageUrl = imageUrl;
        entity.Description = NormalizeOptional(dto.Description);
    }

    private static PropertyDto ConvertToDto(PropertySummaryReadModel readModel)
    {
        return new PropertyDto(
            readModel.PropertyId,
            readModel.OwnerId,
            readModel.OwnerName,
            readModel.PropertyName,
            readModel.AddressText,
            readModel.Ward,
            readModel.District,
            readModel.City,
            readModel.Latitude,
            readModel.Longitude,
            readModel.RoomCount,
            readModel.PrimaryImageUrl,
            readModel.Description);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void ValidateImage(PropertyImageInput image)
    {
        if (image.Content.Length is 0 or > MaxImageBytes)
        {
            throw new PropertyValidationException("Ảnh phải có dung lượng từ 1 byte đến 5 MiB.");
        }

        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        var content = image.Content.AsSpan();
        var isJpeg = image.ContentType == "image/jpeg" &&
                     (extension is ".jpg" or ".jpeg") &&
                     content.Length >= 4 &&
                     content.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }) &&
                     content[^2..].SequenceEqual(new byte[] { 0xFF, 0xD9 });
        var isPng = image.ContentType == "image/png" &&
                    extension == ".png" &&
                    content.Length >= 20 &&
                    content.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }) &&
                    content[^8..].SequenceEqual(new byte[] { 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 });
        var isWebp = image.ContentType == "image/webp" &&
                     extension == ".webp" &&
                     content.Length >= 20 &&
                     content[..4].SequenceEqual("RIFF"u8) &&
                     content[8..12].SequenceEqual("WEBP"u8) &&
                     System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(content[4..8]) == content.Length - 8;
        if (!isJpeg && !isPng && !isWebp)
        {
            throw new PropertyValidationException("Ảnh phải là tệp JPEG, PNG hoặc WebP hợp lệ.");
        }
    }

    private async Task<PropertySummaryReadModel> GetPropertyResponseAsync(int propertyId)
    {
        var property = await _repository.GetByIdAsync(propertyId);
        if (property is null)
        {
            throw new InvalidOperationException("Created property was not found.");
        }
        return property;
    }

    private sealed record ProviderCandidates(
        bool IsAvailable,
        bool IsRateLimited,
        IReadOnlyList<AddressGeocodingCandidate> Candidates);

    private sealed record CandidateEvaluation(
        AddressGeocodingCandidate Candidate,
        int QualityScore);

    private sealed record CandidateSelection(
        AddressGeocodingCandidate? Candidate,
        bool IsAmbiguous);
}

public class PropertyBusinessRuleException(string message) : Exception(message);

public sealed class PropertyRateNotFoundException(string message) : PropertyBusinessRuleException(message);
public sealed class PropertyRateConflictException(string message) : PropertyBusinessRuleException(message);

public sealed class PropertyValidationException(string message) : PropertyBusinessRuleException(message);

public class PropertyGeocodingException(
    string message = "Dịch vụ xác định tọa độ hiện không khả dụng.")
    : Exception(message);

public sealed class PropertyGeocodingRateLimitedException()
    : PropertyGeocodingException("Dịch vụ định vị đang giới hạn số lượt truy cập. Vui lòng thử lại sau.");

public sealed class PropertyImageStorageException()
    : Exception("Dịch vụ lưu trữ ảnh hiện không khả dụng.");

public sealed class PropertyForbiddenException()
    : PropertyBusinessRuleException("Bạn không phải chủ sở hữu của khu trọ này.");

public sealed class PropertyHasRoomsException()
    : PropertyBusinessRuleException("Không thể xoá khu trọ còn dữ liệu liên quan cần bảo toàn.");
