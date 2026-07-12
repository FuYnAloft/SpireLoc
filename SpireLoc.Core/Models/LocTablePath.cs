namespace SpireLoc.Core.Models;

/// <summary>Identifies one localization table by two orthogonal dimensions.</summary>
public sealed record LocTablePath
{
    public LocTablePath(string language, string tableName)
    {
        if (language.Length == 0 || language.All(char.IsWhiteSpace))
            throw new ArgumentException("Language cannot be empty or whitespace.", nameof(language));
        if (tableName.Length == 0 || tableName.All(char.IsWhiteSpace))
            throw new ArgumentException("Table name cannot be empty or whitespace.", nameof(tableName));

        Language = language;
        TableName = tableName;
    }

    public string Language { get; }
    public string TableName { get; }

    public override string ToString() => $"{Language}/{TableName}";
}
