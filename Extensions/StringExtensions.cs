﻿using Misbat.CodeAnalysis.Test.Utility;

namespace Misbat.CodeAnalysis.Test.Extensions
{
    public static class StringExtensions
    {
        public static string Indent(this string target, int indentLevel)
        {
            return StringUtility.Indent(indentLevel) + target;
        }
    }
}