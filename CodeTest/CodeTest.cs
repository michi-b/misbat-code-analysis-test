using System.Collections.Immutable;
using System.Text;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Misbat.CodeAnalysis.Test.Extensions;
using static Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree;

namespace Misbat.CodeAnalysis.Test.CodeTest;

[PublicAPI]
public readonly struct CodeTest
{
    public ImmutableArray<string> NameSpaceImports { get; init; }

    public ImmutableArray<string> Code { get; init; } = ImmutableArray<string>.Empty;

    public ImmutableArray<CodeTestResult> Results { get; private init; } = ImmutableArray<CodeTestResult>.Empty;

    private CodeTestConfiguration Configuration { get; init; }

    public CodeTest(CodeTestConfiguration configuration, ImmutableArray<string> nameSpaceImports = new())
    {
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
        //build code
        var codeBuilder = new StringBuilder();
        foreach (string nameSpaceImport in NameSpaceImports)
        {
            codeBuilder.AppendLine($"using {nameSpaceImport};");
        }

        codeBuilder.Append('\n');
        codeBuilder.Append(Code);

        string code = codeBuilder.ToString();

        Console.WriteLine("testing code:");
        Console.WriteLine("---");
        Console.Write(code);
        Console.Write('\n');
        Console.WriteLine("---");

        Compilation compilation = CSharpCompilation.Create
        (
            Configuration.AssemblyName,
            new[] { ParseText(code, cancellationToken: cancellationToken) },
            Configuration.MetaDataReferences,
            Configuration.CompilationOptions
        );
        Assert.That.Compiles(compilation);

        ImmutableArray<Diagnostic> analyzerDiagnostics = await compilation.WithAnalyzers
                (Configuration.Analyzers, cancellationToken: cancellationToken)
            .GetAnalyzerDiagnosticsAsync(cancellationToken);

        ImmutableDictionary<Type, GeneratorDriver> generatorResults = RunGenerators
            (compilation, out _, out ImmutableArray<Diagnostic> generatorDiagnostics, cancellationToken);

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

    public CodeTest WithCode(string code) => new(this) { Code = Code.Add(code) };

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