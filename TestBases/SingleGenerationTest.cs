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
    [PublicAPI] protected readonly CodeTest.CodeTest CodeTest;

    [PublicAPI] protected readonly ILogger<TTest> Logger;

    [PublicAPI] protected readonly ILoggerFactory LoggerFactory;

    protected SingleGenerationTest(string code, string nameSpace, ILoggerFactory loggerFactory, Predicate<Diagnostic>? diagnosticFilter, params Type[] referencedTypes)
    {
        CodeTest = CodeTestUtility.GetSingleGeneratorCodeTest<TTest, TGenerator>(code, nameSpace, diagnosticFilter, referencedTypes);
        LoggerFactory = loggerFactory;
        Logger = loggerFactory.CreateLogger<TTest>();
    }

    [PublicAPI]
    protected async Task<CodeTestResult> RunCodeTest
    (
        Func<CodeTest.CodeTest, CodeTest.CodeTest>? configureCodeTest = null,
        LoggingOptions loggingOptions = LoggingOptions.None
    )
    {
        CodeTest.CodeTest codeTest = configureCodeTest?.Invoke(CodeTest) ?? CodeTest;
        return (await codeTest.Run(CancellationToken, LoggerFactory, loggingOptions)).Result;
    }

    [PublicAPI]
    protected async Task LogDiagnosticSourceTrees(CodeTestResult result, IEnumerable<Diagnostic> diagnostics)
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
        => Assert.That.HasGeneratedAnyTree((await RunCodeTest(loggingOptions: LoggingOptions.All)).GeneratorResults[typeof(TGenerator)].GetRunResult());

    private static bool IsNotHidden(Diagnostic diagnostic) => diagnostic.Severity != DiagnosticSeverity.Hidden;

    protected async Task TestFinalCompilationReportsNoDiagnostics()
    {
        await TestFinalCompilationReportsNoDiagnostics(IsNotHidden);
    }

    [PublicAPI]
    protected async Task TestFinalCompilationReportsNoDiagnostics(Predicate<Diagnostic>? filter)
    {
        CodeTestResult result = await RunCodeTest(loggingOptions: LoggingOptions.FinalDiagnostics);

        ImmutableArray<Diagnostic> diagnostics = result.Compilation.GetDiagnostics();
        Diagnostic[] filteredDiagnostics = (filter != null ? diagnostics.Where(diagnostic => filter(diagnostic)) : diagnostics).ToArray();

        await LogDiagnosticSourceTrees(result, filteredDiagnostics);

        Assert.AreEqual(0, filteredDiagnostics.Length);
    }

    protected async Task TestGeneratesFile(string shortFileName)
    {
        bool IsTargetDiagnostic(Diagnostic diagnostic) => diagnostic.Location.SourceTree == null || diagnostic.Location.SourceTree.GetShortFilename() == shortFileName;

        Predicate<Diagnostic> diagnosticFilter = IsTargetDiagnostic;

        CodeTest.CodeTest ConfigureCodeTest(CodeTest.CodeTest codeTest)
        {
            return codeTest.Configure(testConfiguration => testConfiguration.WithAdditionalDiagnosticFilters(diagnosticFilter));
        }

        GeneratorDriverRunResult result = GetGeneratorDriverRunResult(await RunCodeTest(ConfigureCodeTest, LoggingOptions.Diagnostics));

        SyntaxTree? tree = result.GeneratedTrees.FirstOrDefault(tree => tree.GetShortFilename() == shortFileName);
        Assert.IsNotNull(tree);
        await Logger.LogTreeAsync(tree, CancellationToken);
        Logger.LogInformation("Full file path is '{TreeFilePath}'", tree.FilePath);
    }

    protected async Task TestGeneratorHasOneResult() => Assert.AreEqual(1, (await RunCodeTest(loggingOptions: LoggingOptions.All)).GeneratorResults.Count);
    protected async Task TestGeneratorThrowsNoException() => Assert.AreEqual(null, GetGeneratorRunResult(await RunCodeTest()).Exception);

    protected async Task TestGeneratorReportsNoDiagnostics()
    {
        ImmutableArray<Diagnostic> diagnostics = GetGeneratorRunResult(await RunCodeTest(loggingOptions: LoggingOptions.GeneratorDiagnostics)).Diagnostics;
        Assert.AreEqual(0, diagnostics.Length);
    }

    protected async Task TestGeneratorReportsDiagnostics(params string[] diagnosticIds)
    {
        ImmutableArray<Diagnostic> diagnostics = GetGeneratorRunResult(await RunCodeTest(loggingOptions: LoggingOptions.GeneratorDiagnostics)).Diagnostics;
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