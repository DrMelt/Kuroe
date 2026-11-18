using Kuroe.Flow.Compilation.Shared.NodeTypes;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Node.Input;

/// <summary>输入节点描述的注册入口。</summary>
public static class InputNodeTypeDescriptions
{
    /// <summary>把输入节点的描述注册进注册表。</summary>
    public static void RegisterInto(
        INodeTypeDescriptionRegistry files,
        IExecutionRunnerFactory runnerFactory) =>
        files.Register(new InputNodeTypeDescription(runnerFactory));
}