using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Misbat.CodeAnalysis.Test.Extensions;

namespace Misbat.CodeAnalysis.Test.CodeTest;

[PublicAPI]
public readonly struct CodeTest
{
    public ImmutableArray<string> NamespaceImports { get; init; }
    public string? Namespace { get; init; }
    public ImmutableList<CodeTestCode> Code { get; init; } = ImmutableList<CodeTestCode>.Empty;
    public ImmutableArray<CodeTestResult> Results { get; private init; } = ImmutableArray<CodeTestResult>.Empty;
    private CodeTestConfiguration Configuration { get; init; }

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
    (
        CancellationToken cancellationToken,
        ILoggerFactory? loggerFactory = null,
        LoggingOptions loggingOptions = LoggingOptions.All
    )
    {
        ImmutableArray<Predicate<Diagnostic>> configurationDiagnosticFilters = Configuration.DiagnosticFilters;

        bool Filter(Diagnostic diagnostic)
        {
            return Enumerable.All(configurationDiagnosticFilters, filter => filter(diagnostic));
        }

        ILogger<CodeTest> logger = loggerFactory != null ? loggerFactory.CreateLogger<CodeTest>() : NullLogger<CodeTest>.Instance;

        var syntaxTrees = new SyntaxTree[Code.Count];

        for (int i = 0; i < Code.Count; i++)
        {
            string? path = Code[i].Path;
            SourceText sourceText = SourceText.From(DecorateTestCode(Code[i]), Encoding.UTF8);

            SyntaxTree tree = CSharpSyntaxTree.ParseText(sourceText, cancellationToken: cancellationToken, path: path ?? string.Empty);

            //log tested code
            if (loggingOptions.HasFlag(LoggingOptions.TestedCode) && logger.IsEnabled(LogLevel.Information))
            {
                await logger.LogTreeAsync(tree, cancellationToken);
            }

            syntaxTrees[i] = tree;
        }

        Compilation compilation = CSharpCompilation.Create
        (
            Configuration.AssemblyName,
            syntaxTrees,
            Configuration.MetaDataReferences,
            Configuration.CompilationOptions
        );

        ImmutableArray<Diagnostic> analyzerDiagnostics = Configuration.Analyzers.Length > 0
            ? (await compilation.WithAnalyzers(Configuration.Analyzers, cancellationToken: cancellationToken)
                .GetAllDiagnosticsAsync(cancellationToken)).Where(Filter)
            .ToImmutableArray()
            : ImmutableArray<Diagnostic>.Empty;

        ImmutableDictionary<Type, GeneratorDriver> generatorResults = RunGenerators
            (compilation, out compilation, out ImmutableArray<Diagnostic> generatorDiagnostics, Filter, cancellationToken);

        ImmutableArray<Diagnostic> finalDiagnostics = compilation.GetDiagnostics().Where(Filter).ToImmutableArray();

        //log all diagnostics
        if ((loggingOptions & LoggingOptions.Diagnostics) != 0 && logger.IsEnabled(LogLevel.Information))
        {
            LogDiagnostics(logger, analyzerDiagnostics, generatorDiagnostics, finalDiagnostics, loggingOptions);
        }

        //log all generated code
        if (loggingOptions.HasFlag(LoggingOptions.GeneratedCode) && logger.IsEnabled(LogLevel.Information))
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
    (
        ILogger<CodeTest> logger,
        ImmutableDictionary<Type, GeneratorDriver> generatorResults,
        CancellationToken cancellationToken
    )
    {
        if (generatorResults.Count > 0)
        {
            foreach (KeyValuePair<Type, GeneratorDriver> generatorResult in generatorResults)
            {
                ImmutableArray<SyntaxTree> generatedTrees = generatorResult.Value.GetRunResult().GeneratedTrees;
                int treesCount = generatedTrees.Length;
                if (treesCount > 0)
                {
                    bool hasTreesString = generatedTrees.TryGetString(out string? treesString, 1);
                    Debug.Assert(hasTreesString);

                    logger.LogInformation
                    (
                        "Generator {GeneratorType} generated {GeneratedTreesCount} trees:\n{TreePaths}",
                        generatorResult.Key.FullName,
                        treesCount,
                        treesString
                    );

                    foreach (SyntaxTree tree in generatedTrees)
                    {
                        await logger.LogTreeAsync(tree, cancellationToken);
                    }
                }
            }
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
        LoggingOptions loggingOptions,
        Func<Diagnostic, bool>? filter = null
    )
    {
        if (loggingOptions.HasFlag(LoggingOptions.AnalyzerDiagnostics))
        {
            logger.LogDiagnostics(analyzerDiagnostics, "Analyzer", filter);
        }

        if (loggingOptions.HasFlag(LoggingOptions.GeneratorDiagnostics))
        {
            logger.LogDiagnostics(generatorDiagnostics, "Generator", filter);
        }

        if (loggingOptions.HasFlag(LoggingOptions.FinalDiagnostics))
        {
            logger.LogDiagnostics(finalDiagnostics, "Final compilation analysis", filter);
        }
    }

    public CodeTest WithCode(string code) => WithCode(new CodeTestCode(code));

    public CodeTest WithCode(CodeTestCode code) => new(this) { Code = Code.Add(code) };

    public CodeTest WithConfiguration(CodeTestConfiguration configuration) => new(this) { Configuration = configuration };

    public CodeTest Configure(Func<CodeTestConfiguration, CodeTestConfiguration> configure) => WithConfiguration(configure(Configuration));

    public CodeTest WithAddedNamespaceImports(params string[] namespaceImports) => new(this) { NamespaceImports = NamespaceImports.AddRange(namespaceImports) };

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
        Func<Diagnostic, bool> diagnosticsFilter,
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

        reportedDiagnostics = reportedDiagnostics.Where(diagnosticsFilter).ToImmutableArray();

        return results;
    }
}