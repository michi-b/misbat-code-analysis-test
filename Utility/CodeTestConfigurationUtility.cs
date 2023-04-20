using System.Collections.Immutable;
using System.Net;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Misbat.CodeAnalysis.Test.CodeTest;

namespace Misbat.CodeAnalysis.Test.Utility;

public static class CodeTestConfigurationUtility
{
    public static CodeTestConfiguration GetSingleGeneratorCodeTestConfiguration<TTest, TGenerator>(params Type[] referencedTypes)
        where TGenerator : IIncrementalGenerator, new()
    {
        ImmutableArray<MetadataReference> metaDataReferences = ImmutableArray.Create
        (
            MetadataReferenceUtility.MsCoreLib,
            MetadataReferenceUtility.SystemRuntime,
            MetadataReferenceUtility.NetStandard,
            MetadataReferenceUtility.FromType<ILoggerFactory>(), //Microsoft.Extensions.Logging
            MetadataReferenceUtility.FromType<IPAddress>(), //System.Net.Primitives
            MetadataReferenceUtility.TransitivelyReferenced(typeof(TTest), "System.Threading.Tasks.Extensions"),
            MetadataReferenceUtility.TransitivelyReferenced(typeof(TTest), "System.Threading.Thread")
        );
        metaDataReferences = metaDataReferences.AddRange(referencedTypes.Select(MetadataReferenceUtility.FromType));
        
        return new CodeTestConfiguration
        (
            metaDataReferences
        ).WithAdditionalGenerators(new TGenerator());
    }
}