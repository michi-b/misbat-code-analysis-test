using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MsbRpc.Test.Generator;

namespace Misbat.CodeAnalysis.Test.TestBases;

public abstract class SingleSourceGeneratorTest<TTest, TGenerator>
    where TGenerator : IIncrementalGenerator, new()
{
    protected readonly CodeTest.CodeTest CodeTest;

    /// <summary>
    ///     must NOT rely on the constructor being called
    /// </summary>
    protected abstract string Code { get; }

    /// <summary>
    ///     must NOT rely on the constructor being called
    /// </summary>
    protected abstract string Namespace { get; }

    /// <summary>
    ///     must NOT rely on the constructor being called
    /// </summary>
    protected abstract Type[] ReferencedTypes { get; }

    protected abstract ILogger Logger { get; }

    // ReSharper disable once MemberCanBePrivate.Global
    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Global
    // MSTest needs the public setter
    public TestContext TestContext { private get; set; } = null!;

    [PublicAPI] protected CancellationToken CancellationToken => TestContext.CancellationTokenSource.Token;

    protected SingleSourceGeneratorTest(params Type[] referencedTypes)
        =>
            // ReSharper disable thrice VirtualMemberCallInConstructor
            // yes, this is bad practice, derived types must make sure not to have these overrides rely on the constructor being called
            CodeTest = CodeTestUtility.GetSingleGeneratorCodeTest<TTest, TGenerator>(Code, Namespace, ReferencedTypes);
}