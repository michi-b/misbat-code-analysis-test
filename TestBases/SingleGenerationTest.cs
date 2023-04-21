using System.Collections.Immutable;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Misbat.CodeAnalysis.Test.CodeTest;
using Misbat.CodeAnalysis.Test.Extensions;
using Misbat.CodeAnalysis.Test.Utility;

namespace Misbat.CodeAnalysis.Test.TestBases;

public abstract class SingleGenerationTest<TTest, TGenerator> : Test
    where TGenerator : IIncrementalGenerator, new()
{
    [PublicAPI]
    protected readonly CodeTest.CodeTest CodeTest;

    [PublicAPI]
    protected readonly ILogger<TTest> Logger;

    [PublicAPI]
    protected readonly ILoggerFactory LoggerFactory;

    protected SingleGenerationTest(string code, string nameSpace, ILoggerFactory loggerFactory, params Type[] referencedTypes)
    {
        CodeTest = CodeTestUtility.GetSingleGeneratorCodeTest<TTest, TGenerator>(code, nameSpace, referencedTypes);
        LoggerFactory = loggerFactory;
        Logger = loggerFactory.CreateLogger<TTest>();
    }

    [PublicAPI]
    protected async Task<CodeTestResult> RunCodeTest
    (
        LoggingOptions loggingOptions = LoggingOptions.None,
        Predicate<Diagnostic>? diagnosticFilter = null
    )
        => (await CodeTest.Run(CancellationToken, LoggerFactory, loggingOptions)).Result;

    [PublicAPI]
    protected async Task LogDiagnosticSourceTrees(CodeTestResult result, ImmutableArray<Diagnostic> diagnostics)
    {
        IEnumerable<string> diagnosticTargetFilePaths = from diagnostic in diagnostics
            where diagnostic.Location.SourceTree != null
            select diagnostic.Location.SourceTree.FilePath;
        foreach (string filePath in diagnosticTargetFilePaths.Distinct())
        {
            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            // I find this more readable
            foreach (KeyValuePair<Type, GeneratorDriver> generatorResult in result.GeneratorResults)
            {
                ImmutableArray<SyntaxTree> syntaxTrees = generatorResult.Value.GetRunResult().GeneratedTrees;
                foreach (SyntaxTree tree in syntaxTrees.Where(tree => tree.FilePath == filePath))
                {
                    await Logger.LogTreeAsync(tree, CancellationToken);
                }
            }
        }
    }

    protected async Task TestGeneratorRuns()
    {
        CodeTestResult result = await RunCodeTest();
        Assert.IsTrue(result.GeneratorResults.ContainsKey(typeof(TGenerator)));
    }

    protected async Task TestGeneratesAnyTrees()
        => Assert.That.HasGeneratedAnyTree((await RunCodeTest(LoggingOptions.All)).GeneratorResults[typeof(TGenerator)].GetRunResult());

    protected async Task TestFinalCompilationReportsNoDiagnostics()
    {
        CodeTestResult result = await RunCodeTest(LoggingOptions.FinalDiagnostics);
        ImmutableArray<Diagnostic> diagnostics = result.Compilation.GetDiagnostics();

        await LogDiagnosticSourceTrees(result, diagnostics);

        Assert.AreEqual(0, diagnostics.Length);
    }

    protected async Task TestGeneratesFile(string shortFileName)
    {
        bool IsTargetDiagnostic(Diagnostic diagnostic) => diagnostic.Location.SourceTree == null || diagnostic.Location.SourceTree.GetShortFilename() == shortFileName;

        Predicate<Diagnostic> diagnosticFilter = IsTargetDiagnostic;
        GeneratorDriverRunResult result = GetGeneratorDriverRunResult(await RunCodeTest(LoggingOptions.Diagnostics, diagnosticFilter));
        SyntaxTree? tree = result.GeneratedTrees.FirstOrDefault(tree => tree.GetShortFilename() == shortFileName);
        Assert.IsNotNull(tree);
        await Logger.LogTreeAsync(tree, CancellationToken);
        Logger.LogInformation("Full file path is '{TreeFilePath}'", tree.FilePath);
    }

    protected async Task TestGeneratorHasOneResult() => Assert.AreEqual(1, (await RunCodeTest(LoggingOptions.All)).GeneratorResults.Count);
    protected async Task TestGeneratorThrowsNoException() => Assert.AreEqual(null, GetGeneratorRunResult(await RunCodeTest()).Exception);

    protected async Task TestGeneratorReportsNoDiagnostics()
    {
        ImmutableArray<Diagnostic> diagnostics = GetGeneratorRunResult(await RunCodeTest(LoggingOptions.GeneratorDiagnostics)).Diagnostics;
        Assert.AreEqual(0, diagnostics.Length);
    }

    protected async Task TestGeneratorReportsDiagnostics(params string[] diagnosticIds)
    {
        ImmutableArray<Diagnostic> diagnostics = GetGeneratorRunResult(await RunCodeTest(LoggingOptions.GeneratorDiagnostics)).Diagnostics;
        foreach (string diagnosticId in diagnosticIds)
        {
            Assert.IsTrue(diagnostics.Any(d => d.Id == diagnosticId), $"Expected diagnostic with ID '{diagnosticId}' was not reported");
        }        
    }

    [PublicAPI]
    protected static GeneratorRunResult GetGeneratorRunResult(CodeTestResult result) => GetGeneratorRunResult(GetGeneratorDriverRunResult(result));

    [PublicAPI]
    protected static GeneratorRunResult GetGeneratorRunResult(GeneratorDriverRunResult generatorResults) => generatorResults.Results[0];

    [PublicAPI]
    protected static GeneratorDriverRunResult GetGeneratorDriverRunResult(CodeTestResult result) => result.GeneratorResults[typeof(TGenerator)].GetRunResult();
}