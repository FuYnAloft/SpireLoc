using SpireLoc.Cli.Registration;
using SpireLoc.Core.Execution;

namespace SpireLoc.Cli.Pipeline;

internal sealed class OperationCompiler(OperationRegistry registry)
{
    public IReadOnlyList<ILocOperation> Compile(IReadOnlyList<OperationInvocationSpec> invocations) =>
        invocations.Select(Compile).ToArray();

    private ILocOperation Compile(OperationInvocationSpec invocation)
    {
        var descriptor = registry.Resolve(invocation.FactoryPath, invocation.Source);
        return descriptor.Create(invocation);
    }
}
