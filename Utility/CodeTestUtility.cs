using Microsoft.CodeAnalysis;
using Misbat.CodeAnalysis.Test.CodeTest;

namespace MsbRpc.Test.Generator;

public static class CodeTestUtility
{
    public static CodeTest GetSingleGeneratorCodeTest<TTest, TGenerator>(string code, string nameSpace, params Type[] referencedTypes) 
        where TGenerator : IIncrementalGenerator, new()
        => new CodeTest(CodeTestConfigurationUtility.GetSingleGeneratorCodeTestConfiguration<TTest, TGenerator>(referencedTypes))
            .WithAddedNamespaceImports("MsbRpc.Generator.Attributes", "MsbRpc.Contracts")
            .InNamespace(nameSpace)
            .WithCode(code);
}