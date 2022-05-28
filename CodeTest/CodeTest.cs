using System.Collections.Immutable;
using System.Text;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Misbat.CodeAnalysis.Test.Extensions;

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

    public async Task<CodeTest> Run(CancellationToken cancellationToken)
    {
        var syntaxTrees = new SyntaxTree[Code.Count];

        Console.WriteLine("test code:");
        for (int i = 0; i < Code.Count; i++)
        {
            CodeTestCode testCode = Code[i];

            var codeBuilder = new StringBuilder();
            foreach (string nameSpaceImport in NameSpaceImports)
            {
                codeBuilder.AppendLine($"using {nameSpaceImport};");
            }

            codeBuilder.Append('\n');
            codeBuilder.Append(testCode.Code);

            string extendedCode = codeBuilder.ToString();

            Console.WriteLine
            (
                testCode.Path != null
                    ? $"--- {testCode.Path}"
                    : "---"
            );
            Console.Write(extendedCode);
            Console.Write('\n');
            Console.WriteLine("---");

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

        Assert.That.Compiles(compilation);

        ImmutableArray<Diagnostic> allDiagnostics = analyzerDiagnostics.AddRange(generatorDiagnostics);

        return WithResult
        (
            new CodeTestResult
            {
                AnalyzerDiagnostics = analyzerDiagnostics,
                GeneratorDiagnostics = generatorDiagnostics,
                AllDiagnostics = allDiagnostics,
                GeneratorResults = generatorResults
            }
        );
    }

    public CodeTest WithCode(string code) => WithCode(new CodeTestCode(code));

    public CodeTest WithCode(CodeTestCode code) => new(this) { Code = Code.Add(code) };

    public CodeTest WithGenerator(ISourceGenerator generator) =>
        new(this) { Configuration = Configuration.WithAdditionalGenerators(ImmutableArray.Create(generator)) };

    public CodeTest WithGenerator(IIncrementalGenerator generator) =>
        new(this) { Configuration = Configuration.WithAdditionalIncrementalGenerators(ImmutableArray.Create(generator)) };

    public CodeTest WithConfiguration(CodeTestConfiguration configuration) => new(this) { Configuration = configuration };

    public CodeTest WithAddedNamespaceImports
        (params string[] namespaceImports) =>
        new(this) { NameSpaceImports = NameSpaceImports.AddRange(namespaceImports) };

    public CodeTestResult Result
    {
        get
        {
            if (Results.Any())
            {
                return Results.Last();
            }

            throw new InvalidOperationException("latest code test result was requested but no results are available");
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