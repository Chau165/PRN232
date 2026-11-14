using PRN232.Application.Normalizers;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class PublicAreaNameNormalizerTests
{
    [Fact]
    public void Normalize_matches_ward_name_with_or_without_administrative_prefix()
    {
        Assert.Equal(
            PublicAreaNameNormalizer.Normalize("Phường Cầu Ông Lãnh"),
            PublicAreaNameNormalizer.Normalize("Cầu Ông Lãnh"));
    }

    [Theory]
    [InlineData("Thành phố Hồ Chí Minh")]
    [InlineData("TP. Hồ Chí Minh")]
    [InlineData("Hồ Chí Minh")]
    [InlineData("Ho Chi Minh City")]
    [InlineData("Ho Chi Minh")]
    public void Normalize_resolves_verified_Ho_Chi_Minh_name_variants(string provinceName)
    {
        Assert.Equal(
            PublicAreaNameNormalizer.Normalize("Thành phố Hồ Chí Minh"),
            PublicAreaNameNormalizer.Normalize(provinceName));
    }

    [Fact]
    public void Normalize_keeps_different_ward_names_distinct()
    {
        Assert.NotEqual(
            PublicAreaNameNormalizer.Normalize("Phường Bến Thành"),
            PublicAreaNameNormalizer.Normalize("Phường Bến Nghé"));
    }
}
