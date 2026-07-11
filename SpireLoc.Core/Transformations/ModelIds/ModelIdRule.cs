namespace SpireLoc.Core.Transformations.ModelIds;

/// <summary>Configures the prefix transformation for one structured model ID key segment.</summary>
public sealed record ModelIdRule(int KeyIndex, string Prefix);
