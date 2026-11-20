using Kuroe.FlowDescription.Description.Shared.Reporting;
using Kuroe.FlowDescription.Definition.Shared.Validation;
using Kuroe.FlowDescription.Definition.Shared.Model;
using Kuroe.FlowDescription.Definition.Shared.Nodes;
using Kuroe.FlowDescription.Definition.Shared.Ports;
using Kuroe.Runner.File;

namespace Kuroe.Node.File;

/// <summary>文件写入节点类型：按路径变量解析路径，把唯一的内容 Text 输入写入文件，写入内容作为产出。</summary>
public sealed class FileWriteNodeType(FileExecutionParameters? parameters)
    : INodeType, INodeTypeValidation
{
    /// <summary>本节点的类型专属声明，未声明时为空。</summary>
    public FileExecutionParameters? Parameters { get; } = parameters;

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
}
