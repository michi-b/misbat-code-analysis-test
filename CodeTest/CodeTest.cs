using System.Collections.Immutable;
using System.Text;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Misbat.CodeAnalysis.Test.CodeTest;

[PublicAPI]
public readonly struct CodeTest
{
    public ImmutableArray<string> NameSpaceImports { get; init; }

    public ImmutableList<CodeTestCode> Code { get; init; } = ImmutableList<CodeTestCode>.Empty;

    public ImmutableArray<CodeTestResult> Results { get; private init; } = ImmutableArray<CodeTestResult>.Empty;

    private CodeTestConfiguration Configuration { get; init; }

    public CodeTest(CodeTestConfiguration configuration, ImmutableArray<string> nameSpaceImports = default)
    {
        nameSpaceImports = nameSpaceImports.IsDefault ? ImmutableArray<string>.Empty : nameSpaceImports;

        Configuration = configuration;
        NameSpaceImports = nameSpaceImports;
    }

    private CodeTest(CodeTest other)
    {
        Configuration = other.Configuration;
        NameSpaceImports = other.NameSpaceImports;
        Code = other.Code;
    }

    public enum LoggingOptions
    {
        None = 0,
        TestedCode = 1 << 0,
        GeneratedCode = 1 << 1,
        AnalyzerDiagnostics = 1 << 2,
        GeneratorDiagnostics = 1 << 3,
        All = TestedCode | GeneratedCode | AnalyzerDiagnostics | GeneratorDiagnostics
    }

    public async Task<CodeTest> Run
        (CancellationToken cancellationToken, ILoggerFactory? loggerFactory = null, LoggingOptions loggingOptions = LoggingOptions.All)
    {
        ILogger<CodeTest> logger = loggerFactory != null ? loggerFactory.CreateLogger<CodeTest>() : NullLogger<CodeTest>.Instance;

        var syntaxTrees = new SyntaxTree[Code.Count];

        for (int i = 0; i < Code.Count; i++)
        {
            CodeTestCode testCode = Code[i];

            var codeBuilder = new StringBuilder();
            foreach (string nameSpaceImport in NameSpaceImports)
            {
                codeBuilder.AppendLine($"using {nameSpaceImport};");
            }

            //add a newline after namespace imports
            codeBuilder.AppendLine();

            codeBuilder.Append(testCode.Code);

            string extendedCode = codeBuilder.ToString();

            if (loggingOptions.HasFlag(LoggingOptions.TestedCode) && logger.IsEnabled(LogLevel.Information))
            {
                await LogCode(logger, "Tested", i, extendedCode, testCode.Path);
            }

            syntaxTrees[i] = CSharpSyntaxTree.ParseText(extendedCode, cancellationToken: cancellationToken, path: testCode.Path ?? "");
        }

        Compilation compilation = CSharpCompilation.Create
        (
            Configuration.AssemblyName,
            syntaxTrees,
            Configuration.MetaDataReferences,
            Configuration.CompilationOptions
        );

        ImmutableArray<Diagnostic> analyzerDiagnostics = Configuration.Analyzers.Any()
            ? await compilation.WithAnalyzers(Configuration.Analyzers, cancellationToken: cancellationToken)
                .GetAnalyzerDiagnosticsAsync(cancellationToken)
            : ImmutableArray<Diagnostic>.Empty;

        if (loggingOptions.HasFlag(LoggingOptions.AnalyzerDiagnostics))
        {
            LogDiagnostics(logger, analyzerDiagnostics, "Analyzer");
        }

        ImmutableDictionary<Type, GeneratorDriver> generatorResults = RunGenerators
            (compilation, out compilation, out ImmutableArray<Diagnostic> generatorDiagnostics, cancellationToken);

        if (loggingOptions.HasFlag(LoggingOptions.GeneratorDiagnostics))
        {
            LogDiagnostics(logger, analyzerDiagnostics, "Generator");
        }

        if (loggingOptions.HasFlag(LoggingOptions.GeneratedCode))
        {
            foreach (KeyValuePair<Type, GeneratorDriver> generatorResult in generatorResults)
            {
                ImmutableArray<SyntaxTree> generatedTrees = generatorResult.Value.GetRunResult().GeneratedTrees;
                if (generatedTrees.Any())
                {
                    for (int i = 0; i < generatedTrees.Length; i++)
                    {
                        SyntaxTree generatedTree = generatedTrees[i];
                        SourceText sourceText = await generatedTree.GetTextAsync(cancellationToken);
                        await LogCode
                        (
                            logger,
                            $"Generated (by {generatorResult.Key.FullName})",
                            i,
                            sourceText.ToString(),
                            generatedTree.FilePath
                        );
                    }
                }
            }
        }

        ImmutableArray<Diagnostic> allDiagnostics = analyzerDiagnostics.AddRange(generatorDiagnostics);

        return WithResult
        (
            new CodeTestResult
            {
                AnalyzerDiagnostics = analyzerDiagnostics,
                GeneratorDiagnostics = generatorDiagnostics,
                AllDiagnostics = allDiagnostics,
                GeneratorResults = generatorResults,
                Compilation = compilation
            }
        );
    }

    private static async Task LogCode(ILogger<CodeTest> logger, object? category, int index, string code, string? path)
    {
        const string codeBeginMarker = "---CODE-BEGIN---";
        const string codeEndMarker = "---CODE-END---";

        var testCodeWriter = new StringWriter();

        await testCodeWriter.WriteLineAsync
        (
            path != null
                ? $"{codeBeginMarker} {path}"
                : $"{codeBeginMarker}"
        );

        await testCodeWriter.WriteAsync(code);
        await testCodeWriter.WriteAsync('\n');
        await testCodeWriter.WriteLineAsync(codeEndMarker);

        logger.LogInformation("{Category} syntax tree {SyntaxTreeIndex} code:\n{Code}", category, index, testCodeWriter);
    }

    private static void LogDiagnostics(ILogger<CodeTest> logger, ImmutableArray<Diagnostic> diagnostics, string sourceName)
    {
        foreach (DiagnosticSeverity severity in new[]
                 {
                     DiagnosticSeverity.Error, DiagnosticSeverity.Warning, DiagnosticSeverity.Info, DiagnosticSeverity.Hidden
                 })
        {
            LogLevel logLevel = severity switch
            {
                DiagnosticSeverity.Hidden => LogLevel.Trace,
                DiagnosticSeverity.Info => LogLevel.Information,
                DiagnosticSeverity.Warning => LogLevel.Warning,
                DiagnosticSeverity.Error => LogLevel.Error,
                _ => throw new ArgumentOutOfRangeException()
            };
            if (logger.IsEnabled(logLevel)) { }

            ImmutableArray<Diagnostic> currentDiagnostics = diagnostics.Where(d => d.Severity == severity).ToImmutableArray();

            if (currentDiagnostics.Any())
            {
                var diagnosticsStringBuilder = new StringBuilder(diagnostics.Length * 100);
                foreach (Diagnostic diagnostic in currentDiagnostics)
                {
                    diagnosticsStringBuilder.AppendLine($"\t{diagnostic}");
                }

                diagnosticsStringBuilder.Length--;

                logger.Log
                (
                    logLevel,
                    "{DiagnosticsSourceName} {DiagnosticSeverity} diagnostics:\n{Diagnostics}",
                    sourceName,
                    severity.ToString(),
                    diagnosticsStringBuilder.ToString()
                );
            }
        }
    }

    public CodeTest WithCode(string code) => WithCode(new CodeTestCode(code));

    public CodeTest WithCode(CodeTestCode code) => new(this) { Code = Code.Add(code) };

    public CodeTest WithConfiguration(CodeTestConfiguration configuration) => new(this) { Configuration = configuration };

    public CodeTest Configure(Func<CodeTestConfiguration, CodeTestConfiguration> configure) => WithConfiguration(configure(Configuration));

    public CodeTest WithAddedNamespaceImports
        (params string[] namespaceImports)
        => new(this) { NameSpaceImports = NameSpaceImports.AddRange(namespaceImports) };

    public CodeTestResult Result
    {
        get
        {
            Assert.AreEqual(Results.Length, 1, $"single code test result was requested, but it has {Results.Length} results");
            return Results[0];
        }
    }

    private CodeTest WithResult(CodeTestResult result) => new(this) { Results = Results.Add(result) };

    private ImmutableDictionary<Type, GeneratorDriver> RunGenerators
    (
        Compilation compilation,
        out Compilation updatedCompilation,
        out ImmutableArray<Diagnostic> reportedDiagnostics,
        CancellationToken cancellationToken
    )
    {
        var results = ImmutableDictionary<Type, GeneratorDriver>.Empty;
        updatedCompilation = compilation;
        reportedDiagnostics = ImmutableArray<Diagnostic>.Empty;

        foreach (ISourceGenerator generator in Configuration.Generators)
        {
            GeneratorDriver generatorDriver = CSharpGeneratorDriver.Create(generator);
            generatorDriver = generatorDriver.RunGeneratorsAndUpdateCompilation
                (updatedCompilation, out updatedCompilation, out ImmutableArray<Diagnostic> diagnostics, cancellationToken);
            results = results.Add(generator.GetType(), generatorDriver);
            reportedDiagnostics = reportedDiagnostics.AddRange(diagnostics);
        }

        foreach (IIncrementalGenerator generator in Configuration.IncrementalGenerators)
        {
            GeneratorDriver generatorDriver = CSharpGeneratorDriver.Create(generator);
            generatorDriver = generatorDriver.RunGeneratorsAndUpdateCompilation
                (updatedCompilation, out updatedCompilation, out ImmutableArray<Diagnostic> diagnostics, cancellationToken);
            results = results.Add(generator.GetType(), generatorDriver);
            reportedDiagnostics = reportedDiagnostics.AddRange(diagnostics);
        }

        return results;
    }
}