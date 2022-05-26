using System.Collections.Immutable;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Misbat.CodeAnalysis.Test.Utility;

namespace Misbat.CodeAnalysis.Test.CodeTest;

[PublicAPI]
public readonly struct CodeTestConfiguration
{
    public readonly CSharpCompilationOptions CompilationOptions = new(OutputKind.DynamicallyLinkedLibrary);

    public readonly string AssemblyName;

    public ImmutableArray<DiagnosticAnalyzer> Analyzers { get; init; }

    public ImmutableArray<ISourceGenerator> Generators { get; init; }

    public ImmutableArray<IIncrementalGenerator> IncrementalGenerators { get; init; }

    private ImmutableHashSet<Type> GeneratorTypes { get; init; } = ImmutableHashSet<Type>.Empty;

    public ImmutableArray<MetadataReference> MetaDataReferences { get; init; } = ImmutableArray.Create
    (
        MetadataReferenceUtility.MsCoreLib,
        MetadataReferenceUtility.SystemRuntime
    );

    private CodeTestConfiguration(CodeTestConfiguration other)
    {
        MetaDataReferences = other.MetaDataReferences;
        Analyzers = other.Analyzers;
        Generators = other.Generators;
        IncrementalGenerators = other.IncrementalGenerators;
        AssemblyName = other.AssemblyName;
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
        ImmutableArray<MetadataReference> additionalMetadataReferences = new(),
        ImmutableArray<DiagnosticAnalyzer> analyzers = default,
        ImmutableArray<ISourceGenerator> generators = default,
        ImmutableArray<IIncrementalGenerator> incrementalGenerators = default,
        string assemblyName = "CodeAnalysisVerification"
    )
    {
        analyzers = analyzers.IsDefault ? ImmutableArray<DiagnosticAnalyzer>.Empty : analyzers;
        generators = generators.IsDefault ? ImmutableArray<ISourceGenerator>.Empty : generators;
        incrementalGenerators = incrementalGenerators.IsDefault ? ImmutableArray<IIncrementalGenerator>.Empty : incrementalGenerators;

        MetaDataReferences = MetaDataReferences.AddRange(additionalMetadataReferences);
        Analyzers = analyzers;
        Generators = generators;
        IncrementalGenerators = incrementalGenerators;
        AssemblyName = assemblyName;
        GeneratorTypes = TestAndTrackDistinctGeneratorTypes(generators.AsEnumerable<object>().Concat(incrementalGenerators));
    }

    public CodeTestConfiguration WithAdditionalMetadataReferences
        (ImmutableArray<MetadataReference> additionalMetadataReferences) =>
        new(this) { MetaDataReferences = MetaDataReferences.AddRange(additionalMetadataReferences) };

    public CodeTestConfiguration WithAdditionalAnalyzers
        (ImmutableArray<DiagnosticAnalyzer> additionalAnalyzers) =>
        new(this) { Analyzers = Analyzers.AddRange(additionalAnalyzers) };

    public CodeTestConfiguration WithAdditionalGenerators
        (ImmutableArray<ISourceGenerator> additionGenerators) =>
        new(this)
        {
            GeneratorTypes = TestAndTrackDistinctGeneratorTypes(additionGenerators),
            Generators = Generators.AddRange(additionGenerators)
        };

    public CodeTestConfiguration WithAdditionalIncrementalGenerators
        (ImmutableArray<IIncrementalGenerator> additionalIncrementalGenerators) =>
        new(this)
        {
            GeneratorTypes = TestAndTrackDistinctGeneratorTypes(additionalIncrementalGenerators),
            IncrementalGenerators = IncrementalGenerators.AddRange(additionalIncrementalGenerators)
        };
}