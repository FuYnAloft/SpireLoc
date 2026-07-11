using Tomlyn;
using SpireLoc.Core.Execution;
using SpireLoc.Core.Models;

namespace SpireLoc.Core.Steps.IO;

public sealed class ReadTomlLocalizationDirectoryOperation(
    string rootPath,
    string toSlot = LocalizationDirectoryOperationSupport.DefaultSlotName) : ILocOperation
{
    public LocOperationResult Execute(LocWorkspace workspace, LocExecutionContext context) =>
        LocalizationDirectoryOperationSupport.Read(workspace, rootPath, toSlot, ".toml", Parse);

    private static LocTable Parse(string text) =>
        NestedLocalizationMapping.Read(Toml.ToModel(text));
}

public sealed class WriteTomlLocalizationDirectoryOperation(
    string rootPath,
    string fromSlot = LocalizationDirectoryOperationSupport.DefaultSlotName) : ILocOperation
{
    public LocOperationResult Execute(LocWorkspace workspace, LocExecutionContext context) =>
        LocalizationDirectoryOperationSupport.Write(workspace, rootPath, fromSlot, ".toml", Serialize);

    private static string Serialize(LocTable table) =>
        Toml.FromModel(NestedLocalizationMapping.Write(table));
}
