#region

using Microsoft.CodeAnalysis.Text;
using Misbat.CodeAnalysis.Test.Utility;

#endregion

namespace Misbat.CodeAnalysis.Test.Extensions;

public static class SourceTextExtensions
{
    public static async ValueTask<string> GetFormattedCodeAsync(this SourceText sourceText, CancellationToken cancellationToken)
    {
        TextLineCollection lines = sourceText.Lines;
        string format = FormatUtility.GetContainingDigitsFormat(lines.Count);
        var testCodeWriter = new StringWriter();

        for (int i = 0; i < lines.Count; i++)
        {
            string lineNumber = string.Format(format, i);
            string formattedLine = $"{lineNumber} | {lines[i]}";

            if (i < lines.Count - 1)
            {
                await testCodeWriter.WriteLineAsync(formattedLine.AsMemory(), cancellationToken);
            }
            else
            {
                await testCodeWriter.WriteAsync(formattedLine.AsMemory(), cancellationToken);
            }
        }

        return testCodeWriter.ToString();
    }
}