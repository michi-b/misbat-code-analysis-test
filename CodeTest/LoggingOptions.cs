using JetBrains.Annotations;

namespace Misbat.CodeAnalysis.Test.CodeTest;

[PublicAPI]
[Flags]
public enum LoggingOptions
{
    None = 0,
    TestedCode = 1 << 0,
    GeneratedCode = 1 << 1,
    AnalyzerDiagnostics = 1 << 2,
    GeneratorDiagnostics = 1 << 3,
    FinalDiagnostics = 1 << 4,
    Diagnostics = AnalyzerDiagnostics | GeneratorDiagnostics | FinalDiagnostics,
    Code = TestedCode | GeneratedCode,
    All = Code | Diagnostics
}