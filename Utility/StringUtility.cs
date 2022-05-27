namespace Misbat.CodeAnalysis.Test.Utility;

public static class StringUtility
{
    public static string Indent(int indentLevel) => new string('\t', indentLevel);

    public static string Join<TItem>(IEnumerable<TItem> items, Func<TItem, string> convert) => Join(items.Select(convert));

    public static string Join(IEnumerable<string> items) => string.Join(", ", items);
}