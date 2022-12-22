using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Misbat.CodeAnalysis.Test.Extensions;

namespace Misbat.CodeAnalysis.Test.Utility;

public static class FormatUtility
{
    private static readonly Regex ShortFileNameRegex = new(".*([^\\.\\\\\\\\]+\\.g\\.cs)$", RegexOptions.RightToLeft);

    public static string GetContainingDigitsFormat(int count)
    {
        int lastIndex = count - 1;
        int decimalPlaces = (int)Math.Floor(Math.Log10(lastIndex) + 1);
        char[] zeros = Enumerable.Repeat('0', decimalPlaces).ToArray();
        return $"{{0:{new string(zeros)}}}";
    }

    public static bool TryGetShortFileName(string path, out string? shortFileName)
    {
        if (string.IsNullOrEmpty(path))
        {
            shortFileName = default;
            return false;
        }

        Match match = ShortFileNameRegex.Match(path);
        if (match.Success)
        {
            shortFileName = match.Groups[1].Captures[0].Value;
            return true;
        }

        shortFileName = default;
        return false;
    }
}