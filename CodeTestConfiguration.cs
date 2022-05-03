using System.Collections.Immutable;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Misbat.CodeAnalysisTest.Utility;

namespace Misbat.CodeAnalysisTest;

[PublicAPI]
public class CodeTestConfiguration
{
    public readonly ImmutableArray<DiagnosticAnalyzer> Analyzers;

    public readonly string AssemblyName;

    public readonly CSharpCompilationOptions CompilationOptions = new(OutputKind.DynamicallyLinkedLibrary);

    public readonly ImmutableArray<ISourceGenerator> Generators;

    public readonly ImmutableArray<MetadataReference> MetaDataReferences = ImmutableArray.Create
    (
        MetadataReferenceUtility.MsCoreLib,
        MetadataReferenceUtility.SystemRuntime
    );

    public CodeTestConfiguration(
        ImmutableArray<MetadataReference> additionalMetaDataReferences = new(),
        ImmutableArray<DiagnosticAnalyzer> analyzers = new(),
        ImmutableArray<ISourceGenerator> generators = new(),
        string assemblyName = "CodeAnalysisVerification")
    {
        MetaDataReferences = MetaDataReferences.AddRange(additionalMetaDataReferences);
        Analyzers = analyzers;
        Generators = generators;
        AssemblyName = assemblyName;
    }
}