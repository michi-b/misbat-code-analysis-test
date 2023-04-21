using Microsoft.CodeAnalysis;
using Misbat.CodeAnalysis.Test.CodeTest;

namespace Misbat.CodeAnalysis.Test.Utility;

public static class CodeTestUtility
{
    public static CodeTest.CodeTest GetSingleGeneratorCodeTest<TTest, TGenerator>
    (
        string code,
        string nameSpace,
        Predicate<Diagnostic>? diagnosticFilter = null,
        params Type[] referencedTypes
    )
        where TGenerator : IIncrementalGenerator, new()
    {
        CodeTestConfiguration configuration = CodeTestConfigurationUtility.GetSingleGeneratorCodeTestConfiguration<TTest, TGenerator>(referencedTypes);
        if (diagnosticFilter != null)
        {
            configuration = configuration.WithAdditionalDiagnosticFilters(diagnosticFilter);
        }
        return new CodeTest.CodeTest(configuration)
            .WithAddedNamespaceImports("MsbRpc.Generator.Attributes", "MsbRpc.Contracts")
            .InNamespace(nameSpace)
            .WithCode(code);
    }
}