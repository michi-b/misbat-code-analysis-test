namespace Misbat.CodeAnalysis.Test.CodeTest;

public readonly record struct CodeTestCode
{
    public string? Path { get; init; }
    public string Code { get; init; }

    public CodeTestCode(string code)
    {
        Code = code;
        Path = null;
    }

    public CodeTestCode(string path, string code)
    {
        Path = path;
        Code = code;
    }
}