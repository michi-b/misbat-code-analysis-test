using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Misbat.CodeAnalysisTest.Utility;

namespace Misbat.CodeAnalysisTest.Extensions;

public static class DiagnosticsExtensions
{
    public static void Log(
        this ImmutableArray<Diagnostic> target,
        string? tag = null,
        int indentLevel = 0)
    {
        string prefix = string.IsNullOrEmpty(tag)
            ? StringUtility.Indent(indentLevel)
            : (tag + ": ").Indent(indentLevel);

        if (!target.Any())
        {
            Console.WriteLine(prefix + "no diagnostics reported");
            return;
        }

        Console.WriteLine(prefix + "diagnostics:");
        foreach (Diagnostic diagnostic in target)
        {
            diagnostic.Log(indentLevel + 1);
        }
    }

    public static ImmutableArray<Diagnostic> WithMinimumSeverity(
        this ImmutableArray<Diagnostic> target,
        DiagnosticSeverity minimumSeverity)
    {
        return target.Where(diagnostic => diagnostic.Severity >= minimumSeverity)
            .ToImmutableArray();
    }

    public static string GetIdsString(this ImmutableArray<Diagnostic> diagnostics)
    {
        IEnumerable<string> ids = diagnostics.Select(diagnostic => diagnostic.Id);
        return string.Join(',', ids);
    }
}