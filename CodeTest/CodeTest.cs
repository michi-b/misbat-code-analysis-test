using System.Collections.Immutable;
using System.Text;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.Logging;
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

    public async Task<CodeTest> Run(ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        ILogger<CodeTest> logger = loggerFactory.CreateLogger<CodeTest>();

        var syntaxTrees = new SyntaxTree[Code.Count];

        for (int i = 0; i < Code.Count; i++)
        {
            CodeTestCode testCode = Code[i];

            var codeBuilder = new StringBuilder();
            foreach (string nameSpaceImport in NameSpaceImports)
            {
                codeBuilder.AppendLine($"using {nameSpaceImport};");
            }

            codeBuilder.Append(testCode.Code);

            string extendedCode = codeBuilder.ToString();

            if (logger.IsEnabled(LogLevel.Information))
            {
                await LogTestCode(logger, i, extendedCode, testCode.Path);
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

        ImmutableDictionary<Type, GeneratorDriver> generatorResults = RunGenerators
            (compilation, out compilation, out ImmutableArray<Diagnostic> generatorDiagnostics, cancellationToken);

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

    private static async Task LogTestCode(ILogger<CodeTest> logger, int treeIndex, string extendedCode, string? path)
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

        await testCodeWriter.WriteAsync(extendedCode);
        await testCodeWriter.WriteAsync('\n');
        await testCodeWriter.WriteLineAsync(codeEndMarker);

        logger.LogInformation("Syntax Tree {SyntaxTreeIndex} has code:\n{Code}", treeIndex, testCodeWriter);
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