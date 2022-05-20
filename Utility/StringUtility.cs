namespace Misbat.CodeAnalysis.Test.Utility
{
    public static class StringUtility
    {
        public static string Indent(int indentLevel)
        {
            return new string('\t', indentLevel);
        }

        public static string Join<TItem>(IEnumerable<TItem> items, Func<TItem, string> convert)
        {
            return Join(items.Select(convert));
        }

        public static string Join(IEnumerable<string> items)
        {
            return string.Join(", ", items);
        }
    }
}