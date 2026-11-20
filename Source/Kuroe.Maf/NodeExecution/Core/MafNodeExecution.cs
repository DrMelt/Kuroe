using Kuroe.Kernel.Execution.Shared.Runtime;
using Kuroe.Maf.Compilation;
using Kuroe.Node.Command;
using Kuroe.Node.File;
using Kuroe.Node.Llm;

namespace Kuroe.Maf.NodeExecution;

/// <summary>MAF 内置节点的执行器装配：只登记 MAF 步骤能表达的节点类型。</summary>
public static class MafNodeExecution
{
    /// <summary>注册 MAF 步骤可用的节点执行器。</summary>
    public static void Register(
        MafNodeExecutions executions,
        MafNodeExecutionRunners runners)
    {
        executions.Register(LlmNodeTypes.Name, type => LlmNodeExecution.Bind(type, runners.Llm));
        executions.Register(CommandNodeTypes.Name, type => CommandNodeExecution.Bind(type, runners.Command));
        executions.Register(FileReadNodeTypes.Name, type => FileNodeExecution.BindRead(type, runners.FileRead));
        executions.Register(FileWriteNodeTypes.Name, type => FileNodeExecution.BindWrite(type, runners.FileWrite));
    }
}

/// <summary>MAF 内置节点使用的执行运行器工厂。</summary>
public sealed record MafNodeExecutionRunners(
    IExecutionRunnerFactory Llm,
    IExecutionRunnerFactory Command,
    IExecutionRunnerFactory FileRead,
    IExecutionRunnerFactory FileWrite);