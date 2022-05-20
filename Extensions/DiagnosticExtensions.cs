using Microsoft.CodeAnalysis;

namespace Misbat.CodeAnalysis.Test.Extensions;

public static class DiagnosticExtensions
{
    public static void Log(this Diagnostic target, int indentLevel = 0)
    {
        Console.WriteLine($"{target.Id} ({target.Severity}): {target.GetMessage()}".Indent(indentLevel));
    }
}