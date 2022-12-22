using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Misbat.CodeAnalysis.Test.Utility;

public static class DiagnosticSeverityUtility
{
    public static readonly ImmutableArray<DiagnosticSeverity> All = ImmutableArray.Create(DiagnosticSeverity.Hidden, DiagnosticSeverity.Info, DiagnosticSeverity.Warning, DiagnosticSeverity.Error);
}