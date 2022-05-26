﻿using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Misbat.CodeAnalysis.Test.Utility;

namespace Misbat.CodeAnalysis.Test.CodeTest;

public readonly struct CodeTestResult
{
    public ImmutableDictionary<Type, GeneratorDriver> GeneratorResults { get; init; }
    public ImmutableArray<Diagnostic> AnalyzerDiagnostics { get; init; }
    public ImmutableArray<Diagnostic> GeneratorDiagnostics { get; init; }
    public ImmutableArray<Diagnostic> AllDiagnostics { get; init; }

    public void LogDiagnostics()
    {
        if (AllDiagnostics.IsEmpty)
        {
            Console.WriteLine("no diagnostics are reported");
        }
        else
        {
            Console.Write("Reported Diagnostics: ");
            Console.Write(StringUtility.Join(AllDiagnostics, diagnostic => diagnostic.Id));
        }
    }
}