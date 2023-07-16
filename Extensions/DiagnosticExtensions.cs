#region

using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Misbat.CodeAnalysis.Test.Utility;

#endregion

namespace Misbat.CodeAnalysis.Test.Extensions;

public static class DiagnosticExtensions
{
    public static void Log(this Diagnostic target, int indentLevel = 0)
        => Console.WriteLine($"{target.Id} ({target.Severity}): {target.GetMessage()}".Indent(indentLevel));

    public static ImmutableArray<Diagnostic> WithSeverity(this ImmutableArray<Diagnostic> target, DiagnosticSeverity severity)
        => target.Where(d => d.Severity == severity).ToImmutableArray();

    public static string GetString(this ImmutableArray<Diagnostic> target)
    {
        var diagnosticsStringBuilder = new StringBuilder(target.Length * 100);
        foreach (Diagnostic diagnostic in target)
        {
            FileLinePositionSpan location = diagnostic.Location.GetLineSpan();

            diagnosticsStringBuilder.Append('\t');
            if (FormatUtility.TryGetShortFileName(location.Path, out string? fileName))
            {
                diagnosticsStringBuilder.Append($"\t{fileName}: ");
            }

            diagnosticsStringBuilder.AppendLine($"{location.Span.ToString()}: {diagnostic.Id}");
            diagnosticsStringBuilder.AppendLine($"\t\t{diagnostic.GetMessage()}");
        }

        bool endsInNewLine = diagnosticsStringBuilder[^1] == '\n';
        if (endsInNewLine)
        {
            if (diagnosticsStringBuilder[^2] == '\r')
            {
                diagnosticsStringBuilder.Length -= 2;
            }
            else
            {
                diagnosticsStringBuilder.Length -= 1;
            }
        }

        string diagnosticsString = diagnosticsStringBuilder.ToString();
        return diagnosticsString;
    }
}