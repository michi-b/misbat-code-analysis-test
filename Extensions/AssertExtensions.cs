#region

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#endregion

namespace Misbat.CodeAnalysis.Test.Extensions;

[SuppressMessage
(
    "Style",
    "IDE0060:Remove unused parameter",
    Justification =
        "extension methods using 'this Assert assert' is the suggested way to add custom assertions, "
        + "even though the assert parameter is not used"
)]
public static class AssertExtensions
{
    public static void DiagnosticsContain(this Assert assert, ImmutableArray<Diagnostic> diagnostics, params string[] expected)
    {
        Assert.That.DiagnosticsContain(diagnostics, expected.AsEnumerable());
    }

    public static void DiagnosticsContain(this Assert assert, ImmutableArray<Diagnostic> diagnostics, IEnumerable<string> expected)
    {
        foreach (string currentExpected in expected)
        {
            Assert.IsTrue
            (
                diagnostics.Any(diagnostic => diagnostic.Id == currentExpected),
                "expected diagnostic id {0} is not among received diagnostics {1}",
                currentExpected,
                diagnostics.GetIdsString()
            );
        }
    }

    public static void HasGeneratedAnyTree(this Assert assert, GeneratorDriverRunResult generatorResult)
    {
        Assert.IsTrue(generatorResult.GeneratedTrees.Length > 0);
    }

    public static void HasGeneratedAnyTree(this Assert assert, GeneratorDriverRunResult generatorResult, Predicate<SyntaxTree> predicate)
    {
        Assert.IsTrue(generatorResult.GeneratedTrees.Any(tree => predicate(tree)));
    }

    public static void Compiles(this Assert assert, Compilation compilation)
    {
        bool compiles = compilation.Test(out ImmutableArray<Diagnostic> diagnostics);

        const string title = "compilation failed";

        if (!compiles)
        {
            diagnostics.Log(title);
        }

        Assert.IsTrue(compiles, title);
    }
}