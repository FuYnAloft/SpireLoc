using System.Text.RegularExpressions;
using SpireLoc.Core.Diagnostics;
using SpireLoc.Core.Execution;
using SpireLoc.Core.Models;
using SpireLoc.Core.Registration;

namespace SpireLoc.Core.Steps.Workspace;

/// <summary>Partitions one localization bundle into matched and unmatched workspace slots.</summary>
public sealed class PartitionLocBundleOperation : ILocOperation
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    private readonly Func<LocTablePath, bool>? _tableMatcher;
    private readonly IReadOnlyList<Regex>? _entryPathPatterns;

    private PartitionLocBundleOperation(
        Func<LocTablePath, bool>? tableMatcher,
        IReadOnlyList<Regex>? entryPathPatterns,
        string fromSlot,
        string matchedSlot,
        string unmatchedSlot)
    {
        LocWorkspace.ValidateSlotName(fromSlot);
        LocWorkspace.ValidateSlotName(matchedSlot);
        LocWorkspace.ValidateSlotName(unmatchedSlot);
        if (string.Equals(matchedSlot, unmatchedSlot, StringComparison.Ordinal))
            throw new ArgumentException("Matched and unmatched slots must be different.", nameof(unmatchedSlot));

        _tableMatcher = tableMatcher;
        _entryPathPatterns = entryPathPatterns;
        FromSlot = fromSlot;
        MatchedSlot = matchedSlot;
        UnmatchedSlot = unmatchedSlot;
    }

    public string FromSlot { get; }
    public string MatchedSlot { get; }
    public string UnmatchedSlot { get; }

    [OperationFactory("partition", "table", Description = "Partition a localization bundle by table name.")]
    public static PartitionLocBundleOperation ByTable(
        [OperationParameter("condition", 0, Description = "Table names; a table matches when any name matches.")]
        IReadOnlyList<string> tableNames,
        [OperationParameter("from", Description = "Source localization bundle slot.")]
        string fromSlot = "main",
        [OperationParameter("matched", Description = "Destination slot for matching tables or entries.")]
        string matchedSlot = "matched",
        [OperationParameter("unmatched", Description = "Destination slot for non-matching tables or entries.")]
        string unmatchedSlot = "unmatched")
    {
        var names = RequireNonBlankConditions(tableNames, "table");
        return new PartitionLocBundleOperation(
            path => names.Contains(path.TableName),
            null,
            fromSlot,
            matchedSlot,
            unmatchedSlot);
    }

    [OperationFactory("partition", "language", Description = "Partition a localization bundle by language.")]
    public static PartitionLocBundleOperation ByLanguage(
        [OperationParameter("condition", 0, Description = "Languages; a table matches when any language matches.")]
        IReadOnlyList<string> languages,
        [OperationParameter("from", Description = "Source localization bundle slot.")]
        string fromSlot = "main",
        [OperationParameter("matched", Description = "Destination slot for matching tables or entries.")]
        string matchedSlot = "matched",
        [OperationParameter("unmatched", Description = "Destination slot for non-matching tables or entries.")]
        string unmatchedSlot = "unmatched")
    {
        var names = RequireNonBlankConditions(languages, "language");
        return new PartitionLocBundleOperation(
            path => names.Contains(path.Language),
            null,
            fromSlot,
            matchedSlot,
            unmatchedSlot);
    }

    [OperationFactory("partition", "regex", Description = "Partition localization entries by their structured path.")]
    public static PartitionLocBundleOperation ByRegex(
        [OperationParameter("condition", 0,
            Description = "Regex patterns matched against language/table/key.segment paths.")]
        IReadOnlyList<string> patterns,
        [OperationParameter("from", Description = "Source localization bundle slot.")]
        string fromSlot = "main",
        [OperationParameter("matched", Description = "Destination slot for matching tables or entries.")]
        string matchedSlot = "matched",
        [OperationParameter("unmatched", Description = "Destination slot for non-matching tables or entries.")]
        string unmatchedSlot = "unmatched")
    {
        if (patterns.Count == 0)
            throw new ArgumentException("At least one regex condition is required.", nameof(patterns));
        var regexes = patterns
            .Select(pattern => new Regex(pattern, RegexOptions.CultureInvariant, RegexTimeout))
            .ToArray();
        return new PartitionLocBundleOperation(
            null,
            regexes,
            fromSlot,
            matchedSlot,
            unmatchedSlot);
    }

    public LocOperationResult Execute(LocWorkspace workspace, LocExecutionContext context)
    {
        LocBundle source;
        try
        {
            source = workspace.Require<LocBundle>(FromSlot);
        }
        catch (LocWorkspaceException exception)
        {
            return Failure(workspace, "LocBundlePartition.Input", exception.Message);
        }

        try
        {
            var (matched, unmatched) = Partition(source);
            return new LocOperationResult(
                workspace.Set(MatchedSlot, matched).Set(UnmatchedSlot, unmatched));
        }
        catch (RegexMatchTimeoutException exception)
        {
            return Failure(
                workspace,
                "LocBundlePartition.RegexTimeout",
                $"Regex '{exception.Pattern}' timed out while partitioning localization entries.");
        }
    }

    private (LocBundle Matched, LocBundle Unmatched) Partition(LocBundle source)
    {
        var matched = new Dictionary<LocTablePath, LocTable>();
        var unmatched = new Dictionary<LocTablePath, LocTable>();
        foreach (var (path, table) in source)
        {
            if (_tableMatcher is not null)
            {
                (_tableMatcher(path) ? matched : unmatched).Add(path, table);
                continue;
            }

            var matchedEntries = new List<LocEntry>();
            var unmatchedEntries = new List<LocEntry>();
            foreach (var entry in table)
            {
                (MatchesEntryPath(path, entry) ? matchedEntries : unmatchedEntries).Add(entry);
            }

            if (matchedEntries.Count > 0)
                matched.Add(path, new LocTable(matchedEntries));
            if (unmatchedEntries.Count > 0 || table.Count == 0)
                unmatched.Add(path, new LocTable(unmatchedEntries));
        }

        return (new LocBundle(matched), new LocBundle(unmatched));
    }

    private bool MatchesEntryPath(LocTablePath tablePath, LocEntry entry)
    {
        var path = $"{tablePath.Language}/{tablePath.TableName}/{string.Join('.', entry.Key)}";
        return _entryPathPatterns!.Any(pattern => pattern.IsMatch(path));
    }

    private static HashSet<string> RequireNonBlankConditions(
        IReadOnlyList<string> conditions,
        string conditionName)
    {
        if (conditions.Count == 0)
            throw new ArgumentException($"At least one {conditionName} condition is required.", nameof(conditions));
        if (conditions.Any(static condition => condition.Length == 0 || condition.All(char.IsWhiteSpace)))
            throw new ArgumentException($"A {conditionName} condition cannot be empty or whitespace.",
                nameof(conditions));
        return conditions.ToHashSet(StringComparer.Ordinal);
    }

    private static LocOperationResult Failure(LocWorkspace workspace, string code, string message) =>
        new(workspace, [Diagnostic.Error(code, message)], LocOperationStatus.Failed);
}
