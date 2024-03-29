﻿#region

using System.Collections.Immutable;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Misbat.CodeAnalysis.Test.Utility;

#endregion

namespace Misbat.CodeAnalysis.Test.Extensions;

public static class LoggerExtensions
{
    public static async ValueTask LogTreeAsync
    (
        this ILogger logger,
        SyntaxTree tree,
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
                logger.LogInformation("({FileName})\n{Code}", fileName, code);
            }
            else
            {
                logger.LogInformation("\n{Code}", code);
            }
        }
    }

    [PublicAPI]
    public static void LogDiagnostics(this ILogger logger, ImmutableArray<Diagnostic> diagnostics)
    {
        LogDiagnosticsPrivate(logger, diagnostics, "There are");
    }

    public static void LogDiagnostics(this ILogger logger, ImmutableArray<Diagnostic> diagnostics, string source)
    {
        LogDiagnosticsPrivate(logger, diagnostics, $"{source} has");
    }

    private static void LogDiagnosticsPrivate(ILogger logger, ImmutableArray<Diagnostic> diagnostics, string messagePrefix)
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
                        "{MessagePrefix} {DiagnosticsCount} '{DiagnosticSeverity}' diagnostics:\n{Diagnostics}",
                        messagePrefix,
                        currentDiagnostics.Length,
                        severity.GetName(),
                        currentDiagnostics.GetString()
                    );
                }
            }
        }
    }
}