#region

using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#endregion

namespace Misbat.CodeAnalysis.Test.TestBases;

public abstract class Test
{
    // ReSharper disable once MemberCanBePrivate.Global
    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Global
    // MSTest needs the public setter
    public TestContext TestContext { private get; set; } = null!;

    [PublicAPI] protected CancellationToken CancellationToken => TestContext.CancellationTokenSource.Token;
}