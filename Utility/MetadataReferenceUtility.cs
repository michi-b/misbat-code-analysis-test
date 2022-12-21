using System.Reflection;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Misbat.CodeAnalysis.Test.Extensions;

namespace Misbat.CodeAnalysis.Test.Utility;

[PublicAPI]
public static class MetadataReferenceUtility
{
    public static readonly MetadataReference MsCoreLib = FromType<object>();

    public static readonly MetadataReference SystemRuntime = FromName("System.Runtime");

    public static readonly MetadataReference NetStandard = FromName("netstandard");

    public static MetadataReference FromType<T>()
    {
        Type type = typeof(T);
        return FromType(type);
    }

    public static MetadataReference FromPath(string path) => MetadataReference.CreateFromFile(path);

    public static MetadataReference FromType(Type type)
    {
        Assembly assembly = type.Assembly;
        string location = assembly.Location;
        MetadataReference metadataReference = MetadataReference.CreateFromFile(location);
        return metadataReference;
    }

    public static MetadataReference FromName(string name)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        Assembly assembly = assemblies.Single(a => a.GetName().Name == name);
        return FromAssembly(assembly);
    }

    public static MetadataReference FromAssembly(Assembly assembly) => MetadataReference.CreateFromFile(assembly.Location);
    
    public static MetadataReference TransitivelyReferenced(Type searchStartType, string name)
        => TransitivelyReferenced(searchStartType.Assembly, name);
    
    public static MetadataReference TransitivelyReferenced(Assembly assembly, string name) => FromAssembly(assembly.FindTransitivelyReferenced(name));
}