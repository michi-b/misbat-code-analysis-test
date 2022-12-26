using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Misbat.CodeAnalysis.Test.Utility;

namespace Misbat.CodeAnalysis.Test.Extensions;

public static class SyntaxTreeExtensions
{
    private const string UnknownFileName = "Unknown";

    public static bool TryGetString(this ImmutableArray<SyntaxTree> trees, out string? treesString, int indentLevel = 0)
    {
        string indent = indentLevel > 0 ? new string('\t', indentLevel) : string.Empty;

        if (trees.Length > 0)
        {
            string firstFileName = trees[0].GetShortFilename();
            var treePathsBuilder = new StringBuilder(trees.Length * firstFileName.Length * 2);
            treePathsBuilder.Append($"{indent}[0] {firstFileName}");
            for (int i = 1; i < trees.Length; i++)
            {
                treePathsBuilder.Append($"\n{indent}[{i}] {trees[i].GetShortFilename()}");
            }

            treesString = treePathsBuilder.ToString();
            return true;
        }

        treesString = null;
        return false;
    }

    public static string GetShortFilename(this SyntaxTree tree)
        => FormatUtility.TryGetShortFileName(tree.FilePath, out string? shortFileName)
            ? shortFileName!
            : UnknownFileName;
}