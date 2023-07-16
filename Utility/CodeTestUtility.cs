#region

using Microsoft.CodeAnalysis;
using Misbat.CodeAnalysis.Test.CodeTest;

#endregion

namespace Misbat.CodeAnalysis.Test.Utility;

public abstract class CodeTestUtility
{
    public static CodeTest.CodeTest GetSingleGeneratorCodeTest<TTest, TGenerator>
    (
        Predicate<Diagnostic>? diagnosticFilter = null,
        params Type[] referencedTypes
    )
        where TGenerator : IIncrementalGenerator, new()
    {
        CodeTestConfiguration configuration = CodeTestConfigurationUtility.GetSingleGeneratorCodeTestConfiguration<TTest, TGenerator>(referencedTypes);

        if (diagnosticFilter != null)
        {
            configuration = configuration.WithAdditionalDiagnosticFilters(diagnosticFilter);
        }

        return new CodeTest.CodeTest(configuration);
    }
}