using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Misbat.CodeAnalysis.Test.Extensions;

public static class DiagnosticSeverityExtensions
{
    public static string GetName(this DiagnosticSeverity target)
    {
        return target switch
        {
            DiagnosticSeverity.Hidden => "Hidden",
            DiagnosticSeverity.Info => "Info",
            DiagnosticSeverity.Warning => "Warning",
            DiagnosticSeverity.Error => "Error",
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
        };
    }

    public static LogLevel GetLogLevel(this DiagnosticSeverity target)
    {
        return target switch
        {
            DiagnosticSeverity.Hidden => LogLevel.Trace,
            DiagnosticSeverity.Info => LogLevel.Information,
            DiagnosticSeverity.Warning => LogLevel.Warning,
            DiagnosticSeverity.Error => LogLevel.Error,
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}