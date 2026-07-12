using SpireLoc.Core.Registration;
using SpireLoc.Core.Steps.Support;

namespace SpireLoc.Core.Steps.Processing.ModelIds;

public static class ModelIdProcessorFactories
{
    [OperationFactory("model-id", "vanilla")]
    public static UnaryLocBundleProcessor CreateVanilla(
        [OperationParameter("reversed", IsFlag = true)] bool reversed = false) =>
        new VanillaModelIdProcessor(GetDirection(reversed));

    [OperationFactory("model-id", "baselib")]
    public static UnaryLocBundleProcessor CreateBaseLib(
        [OperationParameter("namespace-top", 0)] string namespaceTop,
        [OperationParameter("reversed", IsFlag = true)] bool reversed = false) =>
        new BaseLibModelIdProcessor(GetDirection(reversed), namespaceTop);

    [OperationFactory("model-id", "ritsulib")]
    public static UnaryLocBundleProcessor CreateRitsuLib(
        [OperationParameter("mod-id", 0)] string modId,
        [OperationParameter("reversed", IsFlag = true)] bool reversed = false) =>
        new RitsuLibModelIdProcessor(GetDirection(reversed), modId);

    private static ModelIdDirection GetDirection(bool reversed) =>
        reversed ? ModelIdDirection.ToSource : ModelIdDirection.ToGame;
}
