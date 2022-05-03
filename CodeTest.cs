using System.Collections.Immutable;
using System.Text;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Misbat.CodeAnalysisTest.Extensions;
using static Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree;

namespace Misbat.CodeAnalysisTest;

[PublicAPI]
public class CodeTest
{
    public int? ExpectedGeneratedTreesCount { get; init; }

    public ImmutableArray<string> ExpectedDiagnosticIds { get; init; } = ImmutableArray<string>.Empty;

    public ImmutableArray<string> NameSpaceImports { get; init; }

    public string Code { get; init; } = "";

    private GeneratorDriver GeneratorDriver { get; }

    private CodeTestConfiguration Configuration { get; }

    public CodeTest(CodeTestConfiguration configuration, ImmutableArray<string> nameSpaceImports = new())
    {
        Configuration = configuration;
        GeneratorDriver = CSharpGeneratorDriver.Create(Configuration.Generators);
        NameSpaceImports = nameSpaceImports;
    }

    private CodeTest(CodeTest other)
    {
        Configuration = other.Configuration;
        GeneratorDriver = other.GeneratorDriver;
        ExpectedGeneratedTreesCount = other.ExpectedGeneratedTreesCount;
        ExpectedDiagnosticIds = other.ExpectedDiagnosticIds;
        NameSpaceImports = other.NameSpaceImports;
        Code = other.Code;
    }

    public async Task Run(CancellationToken cancellationToken)
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

        GeneratorDriver driver = CSharpGeneratorDriver.Create(Configuration.Generators)
            .RunGeneratorsAndUpdateCompilation
            (
                compilation,
                out _,
                out ImmutableArray<Diagnostic> generatorDiagnostics,
                cancellationToken
            );

        GeneratorDriverRunResult generatorResult = driver.GetRunResult();

        VerifyGeneratedTreesCount(generatorResult);

        ImmutableArray<Diagnostic> allDiagnostics = analyzerDiagnostics.AddRange(generatorDiagnostics);

        Assert.That.DiagnosticsContain(allDiagnostics, ExpectedDiagnosticIds);
    }

    public CodeTest WithCode(string code)
    {
        return new CodeTest(this) { Code = code };
    }

    public CodeTest WithExpectedGeneratedTreesCount(int count)
    {
        return new CodeTest(this) { ExpectedGeneratedTreesCount = count };
    }

    public CodeTest WithExpectedDiagnosticIds(params string[] diagnosticIds)
    {
        return new CodeTest(this) { ExpectedDiagnosticIds = ExpectedDiagnosticIds.AddRange(diagnosticIds) };
    }

    public CodeTest WithAddedNamespaceImports(params string[] namespaceImports)
    {
        return new CodeTest(this) { NameSpaceImports = NameSpaceImports.AddRange(namespaceImports) };
    }

    private void VerifyGeneratedTreesCount(GeneratorDriverRunResult generatorResult)
    {
        if (ExpectedGeneratedTreesCount != null)
        {
            Assert.IsTrue(generatorResult.GeneratedTrees.Length == ExpectedGeneratedTreesCount);
        }
    }
}