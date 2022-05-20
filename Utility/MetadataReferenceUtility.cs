using Microsoft.CodeAnalysis;

namespace Misbat.CodeAnalysis.Test.Utility;

public static class MetadataReferenceUtility
{
    public static readonly MetadataReference MsCoreLib = GetAssemblyReference<object>();

    public static readonly MetadataReference SystemRuntime = GetAssemblyReference("System.Runtime");

    public static MetadataReference GetAssemblyReference<T>()
    {
        return MetadataReference.CreateFromFile(typeof(T).Assembly.Location);
    }

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