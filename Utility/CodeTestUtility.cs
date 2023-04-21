﻿using Microsoft.CodeAnalysis;

namespace Misbat.CodeAnalysis.Test.Utility;

public static class CodeTestUtility
{
    public static CodeTest.CodeTest GetSingleGeneratorCodeTest<TTest, TGenerator>(string code, string nameSpace, params Type[] referencedTypes) 
        where TGenerator : IIncrementalGenerator, new()
        => new CodeTest.CodeTest(CodeTestConfigurationUtility.GetSingleGeneratorCodeTestConfiguration<TTest, TGenerator>(referencedTypes))
            .WithAddedNamespaceImports("MsbRpc.Generator.Attributes", "MsbRpc.Contracts")
            .InNamespace(nameSpace)
            .WithCode(code);
}