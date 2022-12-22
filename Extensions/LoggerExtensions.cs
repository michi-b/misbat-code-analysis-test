using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Misbat.CodeAnalysis.Test.Utility;

namespace Misbat.CodeAnalysis.Test.Extensions;

public static class LoggerExtensions
{
    public static async ValueTask LogTreeAsync
    (
        this ILogger logger,
        SyntaxTree tree,
        string message,
        CancellationToken cancellationToken,
        LogLevel logLevel = LogLevel.Information
    )
    {
        if (logger.IsEnabled(logLevel))
        {
            SourceText sourceText = await tree.GetTextAsync(cancellationToken);
            string code = await sourceText.GetFormattedCodeAsync(cancellationToken);

            if (FormatUtility.TryGetShortFileName(tree.FilePath, out string? fileName))
            {
                logger.LogInformation("{Message} ({FileName})\n{Code}", message, fileName, code);
            }
            else
            {
                logger.LogInformation("{Message}\n{Code}", message, code);
            }
        }
    }

    public static void LogDiagnostics(this ILogger logger, ImmutableArray<Diagnostic> diagnostics, string source)
    {
        foreach (DiagnosticSeverity severity in DiagnosticSeverityUtility.All)
        {
            LogLevel logLevel = severity.GetLogLevel();
            
            if (logger.IsEnabled(logLevel))
            {
                ImmutableArray<Diagnostic> currentDiagnostics = diagnostics.WithSeverity(severity);

                if (currentDiagnostics.Any())
                {
                    logger.Log
                    (
                        logLevel,
                        "{DiagnosticsSource} has {DiagnosticsCount} '{DiagnosticSeverity}' diagnostics:\n{Diagnostics}",
                        source,
                        currentDiagnostics.Length,
                        severity.GetName(),
                        currentDiagnostics.GetString()
                    );
                }
            }
        }
    }
}