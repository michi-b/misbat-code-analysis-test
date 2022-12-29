using System.Collections.Immutable;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Misbat.CodeAnalysis.Test.CodeTest;

[PublicAPI]
public readonly struct CodeTestConfiguration
{
    public readonly CSharpCompilationOptions CompilationOptions = new(OutputKind.DynamicallyLinkedLibrary);

    public readonly string AssemblyName;

    public ImmutableArray<DiagnosticAnalyzer> Analyzers { get; init; } = ImmutableArray<DiagnosticAnalyzer>.Empty;

    public ImmutableArray<ISourceGenerator> Generators { get; init; } = ImmutableArray<ISourceGenerator>.Empty;

    public ImmutableArray<IIncrementalGenerator> IncrementalGenerators { get; init; } = ImmutableArray<IIncrementalGenerator>.Empty;

    private ImmutableHashSet<Type> GeneratorTypes { get; init; } = ImmutableHashSet<Type>.Empty;

    public ImmutableArray<MetadataReference> MetaDataReferences { get; init; } = ImmutableArray<MetadataReference>.Empty;

    public ImmutableArray<Predicate<Diagnostic>> DiagnosticFilters { get; init; } = ImmutableArray<Predicate<Diagnostic>>.Empty;

    private CodeTestConfiguration(CodeTestConfiguration other)
    {
        MetaDataReferences = other.MetaDataReferences;
        Analyzers = other.Analyzers;
        Generators = other.Generators;
        IncrementalGenerators = other.IncrementalGenerators;
        AssemblyName = other.AssemblyName;
        DiagnosticFilters = other.DiagnosticFilters;
    }

    private ImmutableHashSet<Type> TestAndTrackDistinctGeneratorType(Type generatorType)
    {
        if (GeneratorTypes.Contains(generatorType))
        {
            throw new ArgumentException($"generator of type {{{generatorType.Name}}} has been added before", nameof(generatorType));
        }

        return GeneratorTypes.Add(generatorType);
    }

    private ImmutableHashSet<Type> TestAndTrackDistinctGeneratorTypes(IEnumerable<object> generators)
    {
        ImmutableHashSet<Type> result = GeneratorTypes;
        foreach (object generator in generators)
        {
            result = TestAndTrackDistinctGeneratorType(generator.GetType());
        }

        return result;
    }

    public CodeTestConfiguration
    (
        ImmutableArray<MetadataReference> metaDataReferences,
        ImmutableArray<DiagnosticAnalyzer> analyzers = default,
        ImmutableArray<ISourceGenerator> generators = default,
        ImmutableArray<IIncrementalGenerator> incrementalGenerators = default,
        ImmutableArray<Predicate<Diagnostic>> diagnosticFilters = default,
        string assemblyName = "CodeAnalysisVerification"
    )
    {
        analyzers = analyzers.IsDefault ? ImmutableArray<DiagnosticAnalyzer>.Empty : analyzers;
        generators = generators.IsDefault ? ImmutableArray<ISourceGenerator>.Empty : generators;
        incrementalGenerators = incrementalGenerators.IsDefault ? ImmutableArray<IIncrementalGenerator>.Empty : incrementalGenerators;
        diagnosticFilters = diagnosticFilters.IsDefault ? ImmutableArray<Predicate<Diagnostic>>.Empty : diagnosticFilters;

        MetaDataReferences = MetaDataReferences.AddRange(metaDataReferences);
        Analyzers = analyzers;
        Generators = generators;
        IncrementalGenerators = incrementalGenerators;
        AssemblyName = assemblyName;
        DiagnosticFilters = diagnosticFilters;
        GeneratorTypes = TestAndTrackDistinctGeneratorTypes(Generators.AsEnumerable<object>().Concat(incrementalGenerators));
    }

    public CodeTestConfiguration WithAdditionalMetadataReferences
        (params MetadataReference[] additionalMetadataReferences)
        => new(this) { MetaDataReferences = MetaDataReferences.AddRange(additionalMetadataReferences) };

    public CodeTestConfiguration WithAdditionalAnalyzers
        (params DiagnosticAnalyzer[] additionalAnalyzers)
        => new(this) { Analyzers = Analyzers.AddRange(additionalAnalyzers) };

    public CodeTestConfiguration WithAdditionalGenerators
        (params ISourceGenerator[] additionGenerators)
        => new(this)
        {
            GeneratorTypes = TestAndTrackDistinctGeneratorTypes(additionGenerators),
            Generators = Generators.AddRange(additionGenerators)
        };

    public CodeTestConfiguration WithAdditionalGenerators
        (params IIncrementalGenerator[] additionalIncrementalGenerators)
        => new(this)
        {
            GeneratorTypes = TestAndTrackDistinctGeneratorTypes(additionalIncrementalGenerators),
            IncrementalGenerators = IncrementalGenerators.AddRange(additionalIncrementalGenerators)
        };

    public CodeTestConfiguration WithAdditionalDiagnosticFilters(params Predicate<Diagnostic>[] additionalDiagnosticFilters)
        => new(this) { DiagnosticFilters = DiagnosticFilters.AddRange(additionalDiagnosticFilters) };
}