using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using SpireLoc.Core.Execution;
using SpireLoc.Core.Models;

namespace SpireLoc.Core.Steps.IO;

public sealed class ReadYamlLocalizationDirectoryOperation(
    string rootPath,
    string toSlot = LocalizationDirectoryOperationSupport.DefaultSlotName) : ILocOperation
{
    public LocOperationResult Execute(LocWorkspace workspace, LocExecutionContext context) =>
        LocalizationDirectoryOperationSupport.Read(workspace, rootPath, toSlot, ".yaml", Parse);

    private static LocTable Parse(string text)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(NullNamingConvention.Instance)
            .WithAttemptingUnquotedStringTypeDeserialization()
            .Build();
        return NestedLocalizationMapping.Read(deserializer.Deserialize<object>(text));
    }
}

public sealed class WriteYamlLocalizationDirectoryOperation(
    string rootPath,
    string fromSlot = LocalizationDirectoryOperationSupport.DefaultSlotName) : ILocOperation
{
    public LocOperationResult Execute(LocWorkspace workspace, LocExecutionContext context) =>
        LocalizationDirectoryOperationSupport.Write(workspace, rootPath, fromSlot, ".yaml", Serialize);

    private static string Serialize(LocTable table)
    {
        var serializer = new SerializerBuilder()
            .WithTypeConverter(new LiteralStringTypeConverter())
            .WithNamingConvention(NullNamingConvention.Instance)
            .Build();
        return serializer.Serialize(NestedLocalizationMapping.Write(table));
    }

    private sealed class LiteralStringTypeConverter : IYamlTypeConverter
    {
        private static readonly char[] SpecialStart =
            ['[', '{', '-', '#', '*', '&', '!', '>', '<', '%', '@', '`', '"', '\''];

        public bool Accepts(Type type) => type == typeof(string);

        public object? ReadYaml(IParser parser, Type type, ObjectDeserializer nestedObjectDeserializer) =>
            nestedObjectDeserializer(type);

        public void WriteYaml(
            IEmitter emitter,
            object? value,
            Type type,
            ObjectSerializer nestedObjectSerializer)
        {
            var text = value?.ToString() ?? string.Empty;
            var trimmed = text.TrimStart();
            var literal = text.Contains('\n') ||
                          (trimmed.Length > 0 && SpecialStart.Contains(trimmed[0]));
            emitter.Emit(new Scalar(null, null, text, literal ? ScalarStyle.Literal : ScalarStyle.Any, true, false));
        }
    }
}
