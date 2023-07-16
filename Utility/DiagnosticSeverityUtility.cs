#region

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

#endregion

namespace Misbat.CodeAnalysis.Test.Utility;

public static class DiagnosticSeverityUtility
{
    public static readonly ImmutableArray<DiagnosticSeverity> All = ImmutableArray.Create
        (DiagnosticSeverity.Error, DiagnosticSeverity.Warning, DiagnosticSeverity.Info, DiagnosticSeverity.Hidden);
}