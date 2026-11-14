using System.Reflection;
using System.Text.Json;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;

namespace PRN232.Infrastructure.AdministrativeDivisions;

public sealed class AdministrativeDivisionCatalog : IAdministrativeDivisionCatalog
{
    private const string CatalogResourceName =
        "PRN232.Infrastructure.AdministrativeDivisions.vietnam-administrative-divisions-2026-11-14.json";

    private readonly IReadOnlyList<AdministrativeDivisionReadModel> _provinces;
    private readonly IReadOnlyDictionary<string, AdministrativeDivisionReadModel> _provinceByCode;
    private readonly IReadOnlyDictionary<string, AdministrativeDivisionReadModel> _communeByCode;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<AdministrativeDivisionReadModel>> _communesByProvince;

    public AdministrativeDivisionCatalog()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(CatalogResourceName)
            ?? throw new InvalidDataException("Administrative division catalog resource was not found.");

        var document = JsonSerializer.Deserialize<CatalogDocument>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidDataException("Administrative division catalog is empty.");

        var divisions = document.Divisions ?? [];
        var provinces = divisions
            .Where(division => division.AdministrativeLevel == "province")
            .Select(ToReadModel)
            .OrderBy(division => division.Code, StringComparer.Ordinal)
            .ToArray();
        var communes = divisions
            .Where(division => division.AdministrativeLevel == "commune")
            .Select(ToReadModel)
            .ToArray();

        if (provinces.Length != document.ProvinceCount ||
            communes.Length != document.CommuneCount ||
            provinces.Length != 34 ||
            communes.Length != 3321)
        {
            throw new InvalidDataException("Administrative division catalog counts do not match its source metadata.");
        }

        _provinceByCode = CreateUniqueCodeLookup(provinces);
        _communeByCode = CreateUniqueCodeLookup(communes);
        if (communes.Any(commune =>
                commune.ParentProvinceCode is null || !_provinceByCode.ContainsKey(commune.ParentProvinceCode)))
        {
            throw new InvalidDataException("Administrative division catalog contains an invalid province parent code.");
        }

        _communesByProvince = communes
            .GroupBy(commune => commune.ParentProvinceCode!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<AdministrativeDivisionReadModel>)group
                    .OrderBy(commune => commune.Code, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);
        _provinces = provinces;
    }

    public IReadOnlyList<AdministrativeDivisionReadModel> GetProvinces() => _provinces;

    public IReadOnlyList<AdministrativeDivisionReadModel> GetCommunes(string provinceCode)
    {
        return _communesByProvince.TryGetValue(provinceCode, out var communes)
            ? communes
            : Array.Empty<AdministrativeDivisionReadModel>();
    }

    public AdministrativeDivisionReadModel? GetProvince(string provinceCode)
    {
        return _provinceByCode.GetValueOrDefault(provinceCode);
    }

    public AdministrativeDivisionReadModel? GetCommune(string communeCode)
    {
        return _communeByCode.GetValueOrDefault(communeCode);
    }

    private static Dictionary<string, AdministrativeDivisionReadModel> CreateUniqueCodeLookup(
        IEnumerable<AdministrativeDivisionReadModel> divisions)
    {
        var lookup = new Dictionary<string, AdministrativeDivisionReadModel>(StringComparer.Ordinal);
        foreach (var division in divisions)
        {
            if (string.IsNullOrWhiteSpace(division.Code) ||
                string.IsNullOrWhiteSpace(division.Name) ||
                !lookup.TryAdd(division.Code, division))
            {
                throw new InvalidDataException("Administrative division catalog contains a duplicate or empty code.");
            }
        }

        return lookup;
    }

    private static AdministrativeDivisionReadModel ToReadModel(CatalogDivision division)
    {
        return new AdministrativeDivisionReadModel(
            division.Code,
            division.Name,
            division.AdministrativeLevel,
            division.ParentProvinceCode);
    }

    private sealed class CatalogDocument
    {
        public int ProvinceCount { get; set; }

        public int CommuneCount { get; set; }

        public List<CatalogDivision>? Divisions { get; set; }
    }

    private sealed class CatalogDivision
    {
        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string AdministrativeLevel { get; set; } = string.Empty;

        public string? ParentProvinceCode { get; set; }
    }
}
