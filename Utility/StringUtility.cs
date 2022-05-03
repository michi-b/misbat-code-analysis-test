namespace Misbat.CodeAnalysisTest.Utility
{
    public static class StringUtility
    {
        public static string Indent(int indentLevel)
        {
            return new string('\t', indentLevel);
        }
    }
}