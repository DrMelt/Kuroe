using Kuroe.Flow.Compilation.Shared.Validation;
using Kuroe.Flow.Execution;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Kuroe.Files.Shared;

namespace Kuroe.Node.File;

/// <summary>文件写入节点类型：派发执行，按路径变量解析路径，把唯一的内容 Text 输入写入文件，写入内容作为产出。</summary>
public sealed class FileWriteNodeType(FileParameters? parameters, IWorkspaceFiles files)
    : INodeType, INodeTypeValidation, IExecutionRunnerFactory, IExecutionParameterProvider
{
    /// <summary>本节点的类型专属声明，未声明时为空。</summary>
    public FileParameters? Parameters { get; } = parameters;

    /// <summary>类型名。</summary>
    public NodeKind Kind => FileWriteNodeTypes.Name;

    /// <summary>文件写入节点类型的静态形状。</summary>
    public NodeTypeShape Shape => FileWriteNodeTypes.Shape;

    /// <summary>文件写入节点的类型规则：一个内容 Text 输入加若干路径变量 Text 输入，输出可选，不接受执行方式。</summary>
    public void Validate(NodeSpec node, NodeScope scope, ValidationErrorHandler fail)
    {
        void Reject(bool condition, string message)
        {
            if (condition)
            {
                fail(scope.Node(node.Label.Value, message));
            }
        }

        IReadOnlySet<string> variablePorts = FilePathVariables.Validate(node, Parameters, message =>
            fail(scope.Node(node.Label.Value, message)));
        Reject(node.Inputs.Count(port => PortRules.CarriesText(port.Type) && !variablePorts.Contains(port.Name.Value)) != 1,
            "文件写入节点必须声明一个 Text 输入端口，写入内容取自它。");

        Reject(node.Outputs.Count > 1, "文件写入节点最多声明一个输出端口。");
        Reject(node.Spec?.MaxExecutions is { } limit && limit < 1, "MaxExecutions 必须是正整数。");
    }

    /// <inheritdoc/>
    string IExecutionRunnerFactory.Kind => Kind.Value;

    /// <summary>为一次执行建立文件写入运行器。</summary>
    public IExecutionRunner Create(IExecutionTarget target, ExecutionId execution) =>
        target.Parameters is FileParameters declared
            ? new FileWriteExecutionRunner(declared, files)
            : throw new InvalidOperationException("文件写入节点的参数类型不匹配。");

    /// <summary>按本次执行建立文件参数。</summary>
    public IExecutionParameters Create(ExecutionParameterContext context) =>
        Parameters ?? throw new InvalidOperationException("文件写入节点缺少 Path 声明，无法建立执行器。");
}