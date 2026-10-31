using System.Globalization;
using System.Text;

namespace PRN232.Application.Normalizers;

public static class PublicAreaNameNormalizer
{
    private static readonly string[][] Prefixes =
    [
        ["thanh", "pho"],
        ["thi", "xa"],
        ["province", "of"],
        ["city", "of"],
        ["quan"],
        ["district"],
        ["huyen"],
        ["phuong"],
        ["ward"],
        ["commune"],
        ["xa"],
        ["tinh"],
        ["province"],
        ["city"],
        ["tp"],
        ["q"],
        ["p"]
    ];

    private static readonly HashSet<string> Suffixes = new HashSet<string>(StringComparer.Ordinal)
    {
        "city", "province", "district", "ward"
    };

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Trim().Normalize(NormalizationForm.FormD).ToLowerInvariant();
        var characters = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var normalizedCharacter = character;
            if (character == 'đ')
            {
                normalizedCharacter = 'd';
            }

            var outputCharacter = ' ';
            if (char.IsLetterOrDigit(normalizedCharacter))
            {
                outputCharacter = normalizedCharacter;
            }
            characters.Append(outputCharacter);
        }

        var tokens = characters.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        RemovePrefixes(tokens);
        while (tokens.Count > 1 && Suffixes.Contains(tokens[^1]))
        {
            tokens.RemoveAt(tokens.Count - 1);
        }

        var normalized = string.Join(' ', tokens);
        return normalized switch
        {
            "hcm" or "tphcm" or "saigon" or "sai gon" => "ho chi minh",
            "hanoi" => "ha noi",
            "danang" => "da nang",
            _ => normalized
        };
    }

    private static void RemovePrefixes(List<string> tokens)
    {
        var removedPrefix = true;
        while (removedPrefix && tokens.Count > 1)
        {
            removedPrefix = false;
            foreach (var prefix in Prefixes)
            {
                if (tokens.Count <= prefix.Length ||
                    !tokens.Take(prefix.Length).SequenceEqual(prefix, StringComparer.Ordinal))
                {
                    continue;
                }

                tokens.RemoveRange(0, prefix.Length);
                removedPrefix = true;
                break;
            }
        }
    }
}
