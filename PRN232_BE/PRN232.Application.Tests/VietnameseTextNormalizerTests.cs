using PRN232.Application.Normalizers;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class VietnameseTextNormalizerTests
{
    [Fact]
    public void Composes_decomposed_vietnamese_and_collapses_spaces()
    {
        // "Nguyễn  Xiển" typed with Unicode to hop: ê + combining tilde / hook above, two spaces.
        var typed = "  Nguyễn  Xiển ";

        var cleaned = VietnameseTextNormalizer.Clean(typed);

        Assert.Equal("Nguyễn Xiển", cleaned);
        Assert.True(cleaned.IsNormalized(System.Text.NormalizationForm.FormC));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Blank_input_becomes_empty(string? value)
    {
        Assert.Equal(string.Empty, VietnameseTextNormalizer.Clean(value));
    }
}
