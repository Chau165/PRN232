using PRN232.Infrastructure.AdministrativeDivisions;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class AdministrativeDivisionCatalogTests
{
    [Fact]
    public void Catalog_loads_the_official_snapshot_and_preserves_string_codes()
    {
        var catalog = new AdministrativeDivisionCatalog();

        Assert.Equal(34, catalog.GetProvinces().Count);
        Assert.Equal(3321, catalog.GetProvinces()
            .SelectMany(province => catalog.GetCommunes(province.Code))
            .Count());

        var hanoi = catalog.GetProvince("01");
        Assert.NotNull(hanoi);
        Assert.Equal("Thành phố Hà Nội", hanoi.Name);

        var baDinh = catalog.GetCommune("00004");
        Assert.NotNull(baDinh);
        Assert.Equal("Phường Ba Đình", baDinh.Name);
        Assert.Equal("01", baDinh.ParentProvinceCode);
        Assert.Contains(catalog.GetCommunes("01"), item => item.Code == "00004");
        Assert.DoesNotContain(catalog.GetCommunes("79"), item => item.Code == "00004");
    }
}
