using SpireLoc.Core.Registration;
using SpireLoc.Core.Steps.Support;

namespace SpireLoc.Core.Steps.Reshape.Processing;

public static class RitsuLibModelCapabilityReshapeFactories
{
    [OperationFactory(
        "reshape",
        "ritsulib-model-capability",
        "merge",
        Description = "Merge RitsuLib model capability localization tables into cards.")]
    public static UnaryLocBundleProcessor CreateMerge() =>
        new RitsuLibModelCapabilityMergeProcessor();

    [OperationFactory(
        "reshape",
        "ritsulib-model-capability",
        "split",
        Description = "Split RitsuLib model capability localization tables out of cards.")]
    public static UnaryLocBundleProcessor CreateSplit(
        [OperationParameter(
            "mod-id",
            0,
            Description = "Mod ID used for stable prefix matching; omit to use heuristic category detection.")]
        string? modId = null) =>
        new RitsuLibModelCapabilitySplitProcessor(modId);
}
