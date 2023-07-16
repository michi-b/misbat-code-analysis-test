#region

using System.Collections.Immutable;
using System.Text;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Misbat.CodeAnalysis.Test.CodeTest;
using Misbat.CodeAnalysis.Test.Extensions;
using Misbat.CodeAnalysis.Test.Utility;

#endregion

namespace Misbat.CodeAnalysis.Test.TestBases;

public abstract class SingleGenerationTest<TTest, TGenerator> : Test
    where TTest : new()
    where TGenerator : IIncrementalGenerator, new()
{
    [PublicAPI] protected readonly CodeTest.CodeTest CodeTest;

    [PublicAPI] protected readonly ILogger<TTest> Logger;

    [PublicAPI] protected readonly ILoggerFactory LoggerFactory;

    protected SingleGenerationTest
    (
        ILoggerFactory loggerFactory,
        Predicate<Diagnostic>? diagnosticFilter = null,
        Func<CodeTest.CodeTest, CodeTest.CodeTest>? configure = null,
        params Type[] referencedTypes
    )
    {
        CodeTest = CodeTestUtility.GetSingleGeneratorCodeTest<TTest, TGenerator>(diagnosticFilter, referencedTypes);
        CodeTest = configure?.Invoke(CodeTest) ?? CodeTest;
        LoggerFactory = loggerFactory;
        Logger = loggerFactory.CreateLogger<TTest>();
    }

    [PublicAPI]
    protected async Task<CodeTestResult> RunCodeTest(LoggingOptions loggingOptions, Func<CodeTest.CodeTest, CodeTest.CodeTest>? configureCodeTest = null)
    {
        CodeTest.CodeTest codeTest = configureCodeTest?.Invoke(CodeTest) ?? CodeTest;
        return (await codeTest.Run(CancellationToken, LoggerFactory, loggingOptions)).Result;
    }

    [PublicAPI]
    protected async Task LogDiagnosticSourceTrees(CodeTestResult result, IEnumerable<Diagnostic> diagnostics)
    {
        IEnumerable<string> diagnosticTargetFilePaths = from diagnostic in diagnostics
            where diagnostic.Location!.SourceTree != null
            select diagnostic.Location!.SourceTree!.FilePath!;
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

    [PublicAPI]
    protected async Task<CodeTestResult> TestGeneratorRuns(LoggingOptions loggingOptions = LoggingOptions.All)
    {
        CodeTestResult result = await RunCodeTest(loggingOptions);
        Assert.IsTrue(result.GeneratorResults.ContainsKey(typeof(TGenerator)));
        return result;
    }

    [PublicAPI]
    protected async Task<CodeTestResult> TestGeneratesAnyTrees(LoggingOptions loggingOptions = LoggingOptions.All)
    {
        CodeTestResult result = await RunCodeTest(loggingOptions);
        Assert.That.HasGeneratedAnyTree(result.GeneratorResults[typeof(TGenerator)].GetRunResult());
        return result;
    }

    private static bool IsNotHidden(Diagnostic diagnostic) => diagnostic.Severity != DiagnosticSeverity.Hidden;

    [PublicAPI]
    protected async Task<CodeTestResult> TestFinalCompilationReportsNoDiagnostics() => await TestFinalCompilationReportsNoDiagnostics(IsNotHidden);

    [PublicAPI]
    protected async Task<CodeTestResult> TestFinalCompilationReportsNoDiagnostics(Predicate<Diagnostic>? filter)
    {
        CodeTestResult result = await RunCodeTest(LoggingOptions.FinalDiagnostics);

        ImmutableArray<Diagnostic> diagnostics = result.Compilation.GetDiagnostics();
        Diagnostic[] filteredDiagnostics = (filter != null ? diagnostics.Where(diagnostic => filter(diagnostic)) : diagnostics).ToArray();

        await LogDiagnosticSourceTrees(result, filteredDiagnostics);

        Assert.AreEqual(0, filteredDiagnostics.Length);

        return result;
    }

    [PublicAPI]
    protected async Task<CodeTestResult> TestGeneratesFile(string shortFileName)
    {
        bool IsTargetDiagnostic(Diagnostic diagnostic) => diagnostic.Location.SourceTree == null || diagnostic.Location.SourceTree.GetShortFilename() == shortFileName;

        Predicate<Diagnostic> diagnosticFilter = IsTargetDiagnostic;

        CodeTest.CodeTest ConfigureCodeTest(CodeTest.CodeTest codeTest)
        {
            return codeTest.Configure(testConfiguration => testConfiguration.WithAdditionalDiagnosticFilters(diagnosticFilter));
        }

        CodeTestResult result = await RunCodeTest(LoggingOptions.Diagnostics, ConfigureCodeTest);
        GeneratorDriverRunResult generatorDriverRunResult = GetGeneratorDriverRunResult(result);

        ImmutableArray<SyntaxTree> generatedTrees = generatorDriverRunResult.GeneratedTrees;
        SyntaxTree? tree = generatedTrees.FirstOrDefault(tree => tree.GetShortFilename() == shortFileName);
        Assert.IsNotNull(tree, $"expected file '{shortFileName}' was not generated");
        await Logger.LogTreeAsync(tree, CancellationToken);
        Logger.LogInformation("Full file path is '{TreeFilePath}'", tree.FilePath);

        return result;
    }

    [PublicAPI]
    protected async Task<CodeTestResult> TestGeneratorHasResult(bool logGeneratedTrees = true, LoggingOptions loggingOptions = LoggingOptions.None)
    {
        CodeTestResult result = await RunCodeTest(loggingOptions);
        Assert.AreEqual(1, result.GeneratorResults.Count);

        if (logGeneratedTrees)
        {
            var stringBuilder = new StringBuilder(10000);

            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            // LINQ would be less readable here
            foreach (SyntaxTree tree in result.GetGeneratorDriverRunResult<TGenerator>().GeneratedTrees)
            {
                foreach (TextLine line in (await tree.GetTextAsync()).Lines)
                {
                    stringBuilder.AppendLine(line.ToString());
                }
            }

            Logger.LogInformation("Generated trees:\n{Trees}", stringBuilder.ToString());
        }

        return result;
    }

    [PublicAPI]
    protected async Task<CodeTestResult> TestGeneratorThrowsNoException(LoggingOptions loggingOptions = LoggingOptions.None)
    {
        CodeTestResult result = await RunCodeTest(loggingOptions);
        Assert.AreEqual(null, GetGeneratorRunResult(result).Exception);
        return result;
    }

    [PublicAPI]
    protected async Task<CodeTestResult> TestGeneratorReportsNoDiagnostics()
    {
        CodeTestResult result = await RunCodeTest(LoggingOptions.GeneratorDiagnostics);
        ImmutableArray<Diagnostic> diagnostics = GetGeneratorRunResult(result).Diagnostics;
        Assert.AreEqual(0, diagnostics.Length);
        return result;
    }

    [PublicAPI]
    protected async Task<CodeTestResult> TestGeneratorReportsDiagnostics(params string[] diagnosticIds)
    {
        CodeTestResult result = await RunCodeTest(LoggingOptions.GeneratorDiagnostics);
        ImmutableArray<Diagnostic> diagnostics = GetGeneratorRunResult(result).Diagnostics;
        foreach (string diagnosticId in diagnosticIds)
        {
            Assert.IsTrue(diagnostics.Any(d => d.Id == diagnosticId), $"Expected diagnostic with ID '{diagnosticId}' was not reported");
        }

        return result;
    }

    [PublicAPI]
    protected static GeneratorRunResult GetGeneratorRunResult(CodeTestResult result) => GetGeneratorRunResult(GetGeneratorDriverRunResult(result));

    [PublicAPI]
    protected static GeneratorRunResult GetGeneratorRunResult(GeneratorDriverRunResult generatorResults) => generatorResults.Results[0];

    [PublicAPI]
    protected static GeneratorDriverRunResult GetGeneratorDriverRunResult(CodeTestResult result) => result.GeneratorResults[typeof(TGenerator)].GetRunResult();
}