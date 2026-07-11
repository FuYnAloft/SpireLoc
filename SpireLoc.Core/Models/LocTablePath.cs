namespace SpireLoc.Core.Models;

/// <summary>Identifies one localization table by two orthogonal dimensions.</summary>
public sealed record LocTablePath
{
    public LocTablePath(string language, string tableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        Language = language;
        TableName = tableName;
    }

    public string Language { get; }
    public string TableName { get; }

    public override string ToString() => $"{Language}/{TableName}";
}
