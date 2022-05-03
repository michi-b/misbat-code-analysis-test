using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;

namespace Misbat.CodeAnalysisTest.Extensions;

public static class CompilationExtensions
{
    public static bool Test(
        this Compilation compilation,
        string logTag = "compilation",
        DiagnosticSeverity minimumSeverity = DiagnosticSeverity.Info,
        int indentLevel = 0)
    {
        bool result = compilation.Test(out ImmutableArray<Diagnostic> diagnostics);
        diagnostics.WithMinimumSeverity(minimumSeverity).Log(logTag, indentLevel);
        return result;
    }

    public static bool Test(
        this Compilation compilation,
        DiagnosticSeverity minimumSeverity = DiagnosticSeverity.Info,
        string logTag = "compilation",
        int indentLevel = 0)
    {
        bool result = compilation.Test(out ImmutableArray<Diagnostic> diagnostics);
        diagnostics.WithMinimumSeverity(minimumSeverity).Log(logTag, indentLevel);
        return result;
    }

    public static bool Test(this Compilation compilation, out ImmutableArray<Diagnostic> diagnostics)
    {
        using var memoryStream = new MemoryStream();

        EmitResult emitResult = compilation.Emit(memoryStream);

        diagnostics = emitResult.Diagnostics;

        return emitResult.Success;
    }
}