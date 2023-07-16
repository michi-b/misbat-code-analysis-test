#region

using Misbat.CodeAnalysis.Test.Utility;

#endregion

namespace Misbat.CodeAnalysis.Test.Extensions;

public static class StringExtensions
{
    public static string Indent(this string target, int indentLevel) => StringUtility.Indent(indentLevel) + target;
}