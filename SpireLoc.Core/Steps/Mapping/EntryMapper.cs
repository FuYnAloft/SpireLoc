using SpireLoc.Core.Diagnostics;
using SpireLoc.Core.Execution;
using SpireLoc.Core.Models;
using SpireLoc.Core.Transformations;

namespace SpireLoc.Core.Steps.Mapping;

/// <summary>Maps one bundle slot to another, applying either the first or every matching rule per entry.</summary>
public sealed class EntryMapper : ILocOperation
{
    private readonly Rule[] _rules;

    public EntryMapper(
        string inputSlot,
        string outputSlot,
        bool applyAllMatches,
        params Rule[] rules)
    {
        LocWorkspace.ValidateSlotName(inputSlot);
        LocWorkspace.ValidateSlotName(outputSlot);
        ArgumentNullException.ThrowIfNull(rules);

        InputSlot = inputSlot;
        OutputSlot = outputSlot;
        ApplyAllMatches = applyAllMatches;
        _rules = rules.ToArray();
    }

    public string InputSlot { get; }
    public string OutputSlot { get; }
    public bool ApplyAllMatches { get; }

    public sealed record Rule(
        Func<string, string, LocEntry, bool> Predicate,
        ILocEntryConverter Converter)
    {
        public Rule(
            IReadOnlyCollection<string>? languages,
            IReadOnlyCollection<string>? tableNames,
            ILocEntryConverter converter)
            : this(CreatePredicate(languages, tableNames), converter)
        {
        }

        public Rule(IReadOnlyCollection<string>? tableNames, ILocEntryConverter converter)
            : this(null, tableNames, converter)
        {
        }

        public Rule(ILocEntryConverter converter) : this(null, null, converter)
        {
        }

        private static Func<string, string, LocEntry, bool> CreatePredicate(
            IReadOnlyCollection<string>? languages,
            IReadOnlyCollection<string>? tableNames) =>
            (language, tableName, _) =>
                (languages is null || languages.Contains(language)) &&
                (tableNames is null || tableNames.Contains(tableName));
    }

    public LocOperationResult Execute(LocWorkspace workspace, LocExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(context);

        LocBundle input;
        try
        {
            input = workspace.Require<LocBundle>(InputSlot);
        }
        catch (LocWorkspaceException exception)
        {
            return Failure(workspace, "EntryMapper.Input", exception.Message);
        }

        var diagnostics = new List<Diagnostic>();
        var output = LocTableMapper.Map(input, (path, table) =>
            new LocTable(table.Select(entry => MapEntry(path, entry, diagnostics))));

        if (diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            return new LocOperationResult(workspace, diagnostics, LocOperationStatus.Failed);

        return new LocOperationResult(workspace.Set(OutputSlot, output), diagnostics);
    }

    private LocEntry MapEntry(
        LocTablePath path,
        LocEntry entry,
        ICollection<Diagnostic> diagnostics)
    {
        var current = entry;
        var matched = false;

        foreach (var rule in _rules)
        {
            bool matches;
            try
            {
                matches = rule.Predicate(path.Language, path.TableName, current);
            }
            catch (Exception exception)
            {
                diagnostics.Add(Diagnostic.Error(
                    "EntryMapper.Predicate",
                    $"Rule predicate failed for '{FormatKey(entry)}' in '{path}': {exception.Message}"));
                return entry;
            }

            if (!matches)
                continue;

            matched = true;
            try
            {
                current = rule.Converter.Convert(current)
                    ?? throw new InvalidOperationException("The entry converter returned null.");
            }
            catch (Exception exception)
            {
                diagnostics.Add(Diagnostic.Error(
                    "EntryMapper.Convert",
                    $"Rule converter failed for '{FormatKey(entry)}' in '{path}': {exception.Message}"));
                return entry;
            }

            if (!ApplyAllMatches)
                break;
        }

        if (!matched && !ApplyAllMatches)
        {
            diagnostics.Add(Diagnostic.Error(
                "EntryMapper.UnmatchedEntry",
                $"Entry '{FormatKey(entry)}' in '{path}' did not match any rule."));
            return entry;
        }

        return current;
    }

    private static LocOperationResult Failure(LocWorkspace workspace, string code, string message) =>
        new(workspace, [Diagnostic.Error(code, message)], LocOperationStatus.Failed);

    private static string FormatKey(LocEntry entry) => string.Join('.', entry.Key);
}
