using Microsoft.CodeAnalysis;
using static Misbat.CodeAnalysis.Test.Utility.MetadataReferenceUtility;

namespace Misbat.CodeAnalysis.Test;

public class MetadataReferences
{
    public static readonly MetadataReference NetStandard = GetAssemblyReference("netstandard");
}