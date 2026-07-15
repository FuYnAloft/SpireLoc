using SpireLoc.Core.Registration;
using SpireLoc.Core.Steps.Support;

namespace SpireLoc.Core.Steps.Processing.ModelIds;

public static class ModelIdProcessorFactories
{
    [OperationFactory("model-id", "vanilla", Description = "Convert model IDs using the base game's conventions.")]
    public static UnaryLocBundleProcessor CreateVanilla(
        [OperationParameter("reversed", IsFlag = true, Description = "Convert game localization back to source form.")]
        bool reversed = false) =>
        new VanillaModelIdProcessor(GetDirection(reversed));

    [OperationFactory("model-id", "baselib", Description = "Convert model IDs using BaseLib conventions.")]
    public static UnaryLocBundleProcessor CreateBaseLib(
        [OperationParameter("namespace-top", 0, Description = "Top-level BaseLib namespace.")]
        string namespaceTop,
        [OperationParameter("reversed", IsFlag = true, Description = "Convert game localization back to source form.")]
        bool reversed = false) =>
        new BaseLibModelIdProcessor(GetDirection(reversed), namespaceTop);

    [OperationFactory("model-id", "ritsulib", Description = "Convert model IDs using RitsuLib conventions.")]
    public static UnaryLocBundleProcessor CreateRitsuLib(
        [OperationParameter("mod-id", 0, Description = "Mod ID used by RitsuLib.")]
        string modId,
        [OperationParameter("reversed", IsFlag = true, Description = "Convert game localization back to source form.")]
        bool reversed = false) =>
        new RitsuLibModelIdProcessor(GetDirection(reversed), modId);

    [OperationFactory("model-id", "prefix", Description = "Convert selected model ID key segments using a custom prefix.")]
    public static UnaryLocBundleProcessor CreatePrefix(
        [OperationParameter("prefix", 0, Description = "Prefix added to generated game model IDs.")]
        string prefix,
        [OperationParameter("table", 1, Description = "Table name, optionally followed by a zero-based key index as table:index.")]
        IReadOnlyList<string> tableSpecifications,
        [OperationParameter("reversed", IsFlag = true, Description = "Convert game localization back to source form.")]
        bool reversed = false) =>
        new PrefixModelIdProcessor(GetDirection(reversed), prefix, tableSpecifications);

    private static ModelIdDirection GetDirection(bool reversed) =>
        reversed ? ModelIdDirection.ToSource : ModelIdDirection.ToGame;
}
