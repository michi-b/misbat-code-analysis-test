using Misbat.CodeAnalysisTest.Utility;

namespace Misbat.CodeAnalysisTest.Extensions
{
    public static class StringExtensions
    {
        public static string Indent(this string target, int indentLevel)
        {
            return StringUtility.Indent(indentLevel) + target;
        }
    }
}