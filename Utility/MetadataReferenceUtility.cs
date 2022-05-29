using JetBrains.Annotations;
using Microsoft.CodeAnalysis;

namespace Misbat.CodeAnalysis.Test.Utility;

[PublicAPI]
public static class MetadataReferenceUtility
{
    public static readonly MetadataReference MsCoreLib = GetAssemblyReference<object>();

    public static readonly MetadataReference SystemRuntime = GetAssemblyReference("System.Runtime");

    public static readonly MetadataReference NetStandard = GetAssemblyReference("netstandard");

    public static MetadataReference GetAssemblyReference<T>() => MetadataReference.CreateFromFile(typeof(T).Assembly.Location);

    public static MetadataReference GetAssemblyReference(string name)
    {
        return MetadataReference.CreateFromFile
        (
            AppDomain.CurrentDomain.GetAssemblies()
                .Single(a => a.GetName().Name == name)
                .Location
        );
    }
}