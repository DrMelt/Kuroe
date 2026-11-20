using Kuroe.Execution.Shared.Runtime;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Execution;
using Kuroe.Node.Command;
using Kuroe.Node.File;
using Kuroe.Node.Llm;

namespace Kuroe.Flow.NodeExecution;

/// <summary>图流程内置节点的执行器装配：把节点类型的中立执行器绑定与图能力注册集中到一处。</summary>
public static class FlowNodeExecution
{
    /// <summary>注册图流程核心节点的执行器。</summary>
    public static void Register(
        GraphNodeCapabilities capabilities,
        FlowNodeExecutionRunners runners)
    {
        capabilities.Register(LlmNodeTypes.Name, type => LlmNodeExecution.Bind(type, runners.Llm));
        capabilities.Register(CommandNodeTypes.Name, type => CommandNodeExecution.Bind(type, runners.Command));
        capabilities.Register(FileReadNodeTypes.Name, type => FileNodeExecution.BindRead(type, runners.FileRead));
        capabilities.Register(FileWriteNodeTypes.Name, type => FileNodeExecution.BindWrite(type, runners.FileWrite));
    }

    /// <summary>注册等待型节点的执行器与输出投影。</summary>
    public static void RegisterInteractive(
        GraphNodeCapabilities capabilities,
        FlowNodeExecutionRunners runners) =>
        RegisterInteractive(capabilities, runners.Input, runners.Approval);

    /// <summary>注册等待型节点的执行器与输出投影。</summary>
    public static void RegisterInteractive(
        GraphNodeCapabilities capabilities,
        IExecutionRunnerFactory input,
        IExecutionRunnerFactory approval)
    {
        InputNodeExecution.RegisterGraph(capabilities, input);
        ApprovalNodeExecution.RegisterGraph(capabilities, approval);
    }
}

/// <summary>图流程内置节点使用的执行运行器工厂。</summary>
public sealed record FlowNodeExecutionRunners(
    IExecutionRunnerFactory Llm,
    IExecutionRunnerFactory Command,
    IExecutionRunnerFactory FileRead,
    IExecutionRunnerFactory FileWrite,
    IExecutionRunnerFactory Input,
    IExecutionRunnerFactory Approval);