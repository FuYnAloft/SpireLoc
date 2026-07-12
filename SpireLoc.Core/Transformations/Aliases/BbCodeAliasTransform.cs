using System.Text;
using System.Text.RegularExpressions;
using SpireLoc.Core.Models;

namespace SpireLoc.Core.Transformations.Aliases;

/// <summary>Renames configured BBCode tags while preserving unmatched text and tags.</summary>
public sealed partial class BbCodeAliasTransform(params BbCodeAliasTransform.Rule[] rules) : ReversibleLocEntryTransform
{
    public sealed record Rule(string Alias, string GameTag, string? GameMeta = null);

    protected override LocEntry TransformToGame(LocEntry entry, LocEntryTransformContext context) =>
        new(entry.Key, AliasToOriginal(entry.Value));

    protected override LocEntry TransformToSource(LocEntry entry, LocEntryTransformContext context) =>
        new(entry.Key, OriginalToAlias(entry.Value));

    private string OriginalToAlias(string text)
    {
        if (text.Length == 0)
            return text;

        var nodes = Parse(text);
        foreach (var node in nodes)
        {
            if (node is not TagNode { IsClosing: false, IsRenamed: false } tag)
                continue;

            var rule = rules.FirstOrDefault(rule =>
                string.Equals(rule.GameTag, tag.Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(rule.GameMeta, tag.Value, StringComparison.OrdinalIgnoreCase));
            if (rule is null)
                continue;

            tag.IsRenamed = true;
            tag.NewName = rule.Alias;
            tag.NewValue = null;
            if (tag.Companion is not null)
            {
                tag.Companion.IsRenamed = true;
                tag.Companion.NewName = rule.Alias;
            }
        }

        return Rebuild(nodes);
    }

    private string AliasToOriginal(string text)
    {
        if (text.Length == 0)
            return text;

        var nodes = Parse(text);
        foreach (var node in nodes)
        {
            if (node is not TagNode { IsClosing: false, IsRenamed: false } tag)
                continue;

            var rule = rules.FirstOrDefault(rule =>
                string.Equals(rule.Alias, tag.Name, StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrEmpty(tag.Value));
            if (rule is null)
                continue;

            tag.IsRenamed = true;
            tag.NewName = rule.GameTag;
            tag.NewValue = rule.GameMeta;
            if (tag.Companion is not null)
            {
                tag.Companion.IsRenamed = true;
                tag.Companion.NewName = rule.GameTag;
            }
        }

        return Rebuild(nodes);
    }

    private static List<Node> Parse(string text)
    {
        var nodes = new List<Node>();
        var matches = TagRegex().Matches(text);
        var lastIndex = 0;
        foreach (Match match in matches)
        {
            if (match.Index > lastIndex)
                nodes.Add(new TextNode(text.Substring(lastIndex, match.Index - lastIndex)));

            var value = match.Groups[3].Success
                ? match.Groups[3].Value
                : match.Groups[4].Success
                    ? match.Groups[4].Value
                    : match.Groups[5].Success
                        ? match.Groups[5].Value
                        : null;
            nodes.Add(new TagNode(
                match.Value,
                match.Groups[1].Success,
                match.Groups[2].Value,
                value));
            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < text.Length)
            nodes.Add(new TextNode(text[lastIndex..]));

        var openTags = new Stack<TagNode>();
        foreach (var node in nodes)
        {
            if (node is not TagNode tag)
                continue;
            if (!tag.IsClosing)
            {
                openTags.Push(tag);
                continue;
            }

            var displaced = new List<TagNode>();
            TagNode? matching = null;
            while (openTags.Count > 0)
            {
                var open = openTags.Pop();
                if (string.Equals(open.Name, tag.Name, StringComparison.OrdinalIgnoreCase))
                {
                    matching = open;
                    break;
                }

                displaced.Add(open);
            }

            if (matching is not null)
            {
                matching.Companion = tag;
                tag.Companion = matching;
            }

            for (var index = displaced.Count - 1; index >= 0; index--)
                openTags.Push(displaced[index]);
        }

        return nodes;
    }

    private static string Rebuild(IEnumerable<Node> nodes)
    {
        var builder = new StringBuilder();
        foreach (var node in nodes)
        {
            switch (node)
            {
                case TextNode text:
                    builder.Append(text.Text);
                    break;
                case TagNode { IsRenamed: true, IsClosing: true } tag:
                    builder.Append($"[/{tag.NewName}]");
                    break;
                case TagNode { IsRenamed: true } tag:
                    if (tag.NewValue is null)
                    {
                        builder.Append($"[{tag.NewName}]");
                    }
                    else
                    {
                        var value = tag.NewValue.Contains(' ')
                            ? $"\"{tag.NewValue}\""
                            : tag.NewValue;
                        builder.Append($"[{tag.NewName}={value}]");
                    }

                    break;
                case TagNode tag:
                    builder.Append(tag.Raw);
                    break;
            }
        }

        return builder.ToString();
    }

    [GeneratedRegex("""\[(/)?([a-zA-Z0-9*+\-_:]+)(?:=(?:\"([^\"]*)\"|'([^']*)'|([^\]]*)))?\]""")]
    private static partial Regex TagRegex();

    private abstract class Node;

    private sealed class TextNode(string text) : Node
    {
        public string Text { get; } = text;
    }

    private sealed class TagNode(string raw, bool isClosing, string name, string? value) : Node
    {
        public string Raw { get; } = raw;
        public bool IsClosing { get; } = isClosing;
        public string Name { get; } = name;
        public string? Value { get; } = value;
        public TagNode? Companion { get; set; }
        public bool IsRenamed { get; set; }
        public string? NewName { get; set; }
        public string? NewValue { get; set; }
    }
}
