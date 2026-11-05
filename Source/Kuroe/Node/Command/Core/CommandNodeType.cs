using Kuroe.Flow.Compilation.Shared.Validation;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Kuroe.Process;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Node.Command;

/// <summary>命令节点类型：派发执行，按命令变量展开执行目录与参数并执行一次命令，把合并输出按内容产出发布到文本端口。</summary>
public sealed class CommandNodeType(CommandParameters? parameters, IProcessRunner process)
    : INodeType, INodeTypeValidation, INodeExecutionRunnerFactory
{
    /// <summary>本节点的类型专属声明，未声明时为空。</summary>
    public CommandParameters? Parameters { get; } = parameters;

    /// <summary>进程执行边界。</summary>
    public IProcessRunner Process { get; } = process;

    /// <summary>类型名。</summary>
    public NodeKind Kind => CommandNodeTypes.Name;

    /// <summary>命令节点类型的静态形状。</summary>
    public NodeTypeShape Shape => CommandNodeTypes.Shape;

    /// <summary>命令节点的类型规则：Directory 的变量映射给出执行目录，其余输入只作触发。</summary>
    public void Validate(NodeSpec node, NodeScope scope, ValidationErrorHandler fail)
    {
        void Reject(bool condition, string message)
        {
            if (condition)
            {
                fail(scope.Node(node.Label.Value, message));
            }
        }

        Reject(!node.Inputs.Any(port => port.Type.Semantics == PortSemantics.Signal),
            "命令执行节点必须声明至少一个 Signal 输入端口作为触发。");

        IReadOnlySet<string> variablePorts = CommandVariables.Validate(node, Parameters, message =>
            fail(scope.Node(node.Label.Value, message)));
        Reject(node.Inputs.Any(port => PortRules.CarriesText(port.Type) && !variablePorts.Contains(port.Name.Value)),
            "命令执行节点的 Text 输入必须是命令变量端口。");

        Reject(node.Spec?.MaxExecutions is { } limit && limit < 1, "MaxExecutions 必须是正整数。");
    }

    /// <summary>为一次执行建立命令运行器。</summary>
    public IExecutionRunner Create(ExecutableNode node, ExecutionId execution, ExecutionInput input) =>
        new CommandExecutionRunner(Parameters
            ?? throw new InvalidOperationException("命令执行节点缺少 Program 声明，无法建立执行器。"), Process);
}