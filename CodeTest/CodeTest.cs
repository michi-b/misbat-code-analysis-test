using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
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
    private static readonly Regex ShortGeneratedCodeFilePathRegex = new(".*([^\\.]+\\.g\\.cs)$", RegexOptions.RightToLeft);

    public ImmutableArray<string> NamespaceImports { get; init; }
    public string? Namespace { get; init; }
    public ImmutableList<CodeTestCode> Code { get; init; } = ImmutableList<CodeTestCode>.Empty;
    public ImmutableArray<CodeTestResult> Results { get; private init; } = ImmutableArray<CodeTestResult>.Empty;
    private CodeTestConfiguration Configuration { get; init; }

    [PublicAPI]
    public enum LoggingOptions
    {
        None = 0,
        TestedCode = 1 << 0,
        GeneratedCode = 1 << 1,
        AnalyzerDiagnostics = 1 << 2,
        GeneratorDiagnostics = 1 << 3,
        All = TestedCode | GeneratedCode | AnalyzerDiagnostics | GeneratorDiagnostics
    }

    public CodeTest(CodeTestConfiguration configuration, ImmutableArray<string> namespaceImports = default, string? inNamespace = null)
    {
        namespaceImports = namespaceImports.IsDefault ? ImmutableArray<string>.Empty : namespaceImports;
        Configuration = configuration;
        NamespaceImports = namespaceImports;
        Namespace = inNamespace;
    }

    private CodeTest(CodeTest other)
    {
        Configuration = other.Configuration;
        NamespaceImports = other.NamespaceImports;
        Namespace = other.Namespace;
        Code = other.Code;
    }

    public async Task<CodeTest> Run
        (CancellationToken cancellationToken, ILoggerFactory? loggerFactory = null, LoggingOptions loggingOptions = LoggingOptions.All)
    {
        ILogger<CodeTest> logger = loggerFactory != null ? loggerFactory.CreateLogger<CodeTest>() : NullLogger<CodeTest>.Instance;

        var syntaxTrees = new SyntaxTree[Code.Count];

        for (int i = 0; i < Code.Count; i++)
        {
            string? path = Code[i].Path;
            string code = DecorateTestCode(Code[i]);

            if (loggingOptions.HasFlag(LoggingOptions.TestedCode) && logger.IsEnabled(LogLevel.Information))
            {
                await LogCode(logger, "Tested", i, code, path);
            }

            syntaxTrees[i] = CSharpSyntaxTree.ParseText(code, cancellationToken: cancellationToken, path: path ?? "");
        }

        Compilation compilation = CSharpCompilation.Create
        (
            Configuration.AssemblyName,
            syntaxTrees,
            Configuration.MetaDataReferences,
            Configuration.CompilationOptions
        );

        ImmutableArray<Diagnostic> analyzerDiagnostics = Configuration.Analyzers.Length > 0
            ? await compilation.WithAnalyzers(Configuration.Analyzers, cancellationToken: cancellationToken)
                .GetAllDiagnosticsAsync(cancellationToken)
            : ImmutableArray<Diagnostic>.Empty;

        ImmutableDictionary<Type, GeneratorDriver> generatorResults = RunGenerators
            (compilation, out compilation, out ImmutableArray<Diagnostic> generatorDiagnostics, cancellationToken);

        ImmutableArray<Diagnostic> finalDiagnostics = compilation.GetDiagnostics();

        LogDiagnostics(logger, analyzerDiagnostics, generatorDiagnostics, finalDiagnostics, loggingOptions);

        if (loggingOptions.HasFlag(LoggingOptions.GeneratedCode))
        {
            await LogGeneratedCode(logger, generatorResults, cancellationToken);
        }

        return WithResult
        (
            new CodeTestResult
            {
                AnalyzerDiagnostics = analyzerDiagnostics,
                GeneratorDiagnostics = generatorDiagnostics,
                FinalDiagnostics = finalDiagnostics,
                GeneratorResults = generatorResults,
                Compilation = compilation
            }
        );
    }

    private string DecorateTestCode(CodeTestCode testCode)
    {
        var codeBuilder = new StringBuilder();

        if (NamespaceImports.Length > 0)
        {
            foreach (string nameSpaceImport in NamespaceImports)
            {
                codeBuilder.AppendLine($"using {nameSpaceImport};");
            }

            codeBuilder.AppendLine();
        }

        if (Namespace != null)
        {
            codeBuilder.AppendLine($"namespace {Namespace};");
            codeBuilder.AppendLine();
        }

        codeBuilder.Append(testCode.Code);

        string code = codeBuilder.ToString();
        return code;
    }

    private static async Task LogGeneratedCode
        (ILogger<CodeTest> logger, ImmutableDictionary<Type, GeneratorDriver> generatorResults, CancellationToken cancellationToken)
    {
        if (generatorResults.Count > 0)
        {
            foreach (KeyValuePair<Type, GeneratorDriver> generatorResult in generatorResults)
            {
                ImmutableArray<SyntaxTree> generatedTrees = generatorResult.Value.GetRunResult().GeneratedTrees;
                if (generatedTrees.Any())
                {
                    string[] treePaths = (from tree in generatedTrees select Path.GetFileName(tree.FilePath)).ToArray();
                    int treeCount = treePaths.Length;
                    var treePathsBuilder = new StringBuilder(treePaths.Length * treePaths[0].Length * 2);
                    for (int i = 0; i < treeCount; i++)
                    {
                        treePathsBuilder.Append($"\n[{i}] {treePaths[i]}");
                    }

                    logger.LogInformation
                    (
                        "Generator {GeneratorType} generated {GeneratedTreesCount} trees:{TreePaths}",
                        generatorResult.Key.FullName,
                        treeCount,
                        treePathsBuilder.ToString()
                    );

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
    }

    private static async Task LogCode(ILogger<CodeTest> logger, object? category, int index, string code, string? path)
    {
        var textCodeReader = new StringReader(code);
        var testCodeWriter = new StringWriter();

        var lines = new List<string>(100);
        while (await textCodeReader.ReadLineAsync() is { } line)
        {
            lines.Add(line);
        }

        string format = GetContainingDigitsFormat(lines.Count);

        for (int i = 0; i < lines.Count; i++)
        {
            string lineNumber = string.Format(format, i);
            string formattedLine = $"{lineNumber} | {lines[i]}";

            if (i < lines.Count - 1)
            {
                await testCodeWriter.WriteLineAsync(formattedLine);
            }
            else
            {
                await testCodeWriter.WriteAsync(formattedLine);
            }
        }

        if (path != null)
        {
            logger.LogInformation
            (
                "{Category} syntax tree {SyntaxTreeIndex} code:\n{ShortPath}:\n{Code}",
                category,
                index,
                GetShortPath(path),
                testCodeWriter
            );
        }
        else
        {
            logger.LogInformation("{Category} syntax tree {SyntaxTreeIndex} code:\n{Code}", category, index, testCodeWriter);
        }
    }

    private static string GetContainingDigitsFormat(int count)
    {
        int lastIndex = count - 1;
        int decimalPlaces = (int)Math.Floor(Math.Log10(lastIndex) + 1);
        char[] zeros = Enumerable.Repeat('0', decimalPlaces).ToArray();
        return $"{{0:{new string(zeros)}}}";
    }

    private static void LogDiagnostics
    (
        ILogger<CodeTest> logger,
        ImmutableArray<Diagnostic> analyzerDiagnostics,
        ImmutableArray<Diagnostic> generatorDiagnostics,
        ImmutableArray<Diagnostic> finalDiagnostics,
        LoggingOptions loggingOptions
    )
    {
        if (loggingOptions.HasFlag(LoggingOptions.AnalyzerDiagnostics))
        {
            LogDiagnostics(logger, analyzerDiagnostics, "Analyzer");
        }

        if (loggingOptions.HasFlag(LoggingOptions.GeneratorDiagnostics))
        {
            LogDiagnostics(logger, generatorDiagnostics, "Generator");
        }

        LogDiagnostics(logger, finalDiagnostics, "Final compilation analysis");
    }

    private static void LogDiagnostics(ILogger<CodeTest> logger, ImmutableArray<Diagnostic> diagnostics, string diagnosticsSource)
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
            if (logger.IsEnabled(logLevel))
            {
                ImmutableArray<Diagnostic> currentDiagnostics = diagnostics.Where(d => d.Severity == severity).ToImmutableArray();

                if (currentDiagnostics.Any())
                {
                    var diagnosticsStringBuilder = new StringBuilder(diagnostics.Length * 100);
                    foreach (Diagnostic diagnostic in currentDiagnostics)
                    {
                        FileLinePositionSpan location = diagnostic.Location.GetLineSpan();
                        string shortPath = GetShortPath(location.Path);
                        diagnosticsStringBuilder.AppendLine($"\t{shortPath}: {location.Span.ToString()}:");
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

                    string severityName = severity.ToString();
                    string diagnosticsString = diagnosticsStringBuilder.ToString();

                    logger.Log
                    (
                        logLevel,
                        "{DiagnosticsSource} has {DiagnosticsCount} '{DiagnosticSeverity}' diagnostics:\n{Diagnostics}",
                        diagnosticsSource,
                        currentDiagnostics.Length,
                        severityName,
                        diagnosticsString
                    );
                }
            }
        }
    }

    private static string GetShortPath(string path) => ShortGeneratedCodeFilePathRegex.Match(path).Groups[1].Captures[0].Value;

    public CodeTest WithCode(string code) => WithCode(new CodeTestCode(code));

    public CodeTest WithCode(CodeTestCode code) => new(this) { Code = Code.Add(code) };

    public CodeTest WithConfiguration(CodeTestConfiguration configuration) => new(this) { Configuration = configuration };

    public CodeTest Configure(Func<CodeTestConfiguration, CodeTestConfiguration> configure) => WithConfiguration(configure(Configuration));

    public CodeTest WithAddedNamespaceImports
        (params string[] namespaceImports)
        => new(this) { NamespaceImports = NamespaceImports.AddRange(namespaceImports) };

    public CodeTestResult Result
    {
        get
        {
            Assert.AreEqual(Results.Length, 1, $"single code test result was requested, but it has {Results.Length} results");
            return Results[0];
        }
    }

    private CodeTest WithResult(CodeTestResult result) => new(this) { Results = Results.Add(result) };

    public CodeTest InNamespace(string namespaceIn) => new(this) { Namespace = namespaceIn };

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