using Kuroe.Flow.Compilation.Description;
using Kuroe.Flow.Compilation.Shared.NodeTypes;
using Kuroe.Flow.InputChannel.Shared;
using Kuroe.Flow.NodeType.Container;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Node.Approval;
using Kuroe.Node.Input;
using Kuroe.Node.Llm;
using Kuroe.Tool;
using Kuroe.Tool.Dotnet;
using Kuroe.Tool.File;
using Kuroe.Tool.Git;
using Kuroe.Tool.Time;
using Kuroe.Tools;

namespace Kuroe.Tests.Support;

/// <summary>测试用的节点类型描述注册表：引擎内置的容器与扩展提供的各节点类型。</summary>
internal static class TestTypeFiles
{
    /// <summary>核心可变变量工具的契约，供直接编译流程的测试校验 ToolPorts。</summary>
    internal static IToolContractCatalog Contracts { get; } = new ToolContractCatalog(
    [
        .. new FileToolSource().Definitions,
        .. new GitToolSource().Definitions,
        .. new DotnetToolSource().Definitions,
        .. new TimeToolSource().Definitions,
    ]);

    /// <summary>建出一份含全部可用类型的注册表，外部输入协议取调用方给出的实例。</summary>
    internal static NodeTypeDescriptionRegistry Registry(IInputChannel inputs)
    {
        NodeTypeDescriptionRegistry files = new();
        ContainerNodeTypeDescriptions.RegisterInto(files);
        LlmNodeTypeDescriptions.RegisterInto(files, Contracts);
        RegisterAll(files, inputs);

        return files;
    }

    /// <summary>把输入与审批节点描述注册进注册表。</summary>
    internal static void RegisterAll(INodeTypeDescriptionRegistry files, IInputChannel inputs)
    {
        InputNodeTypeDescriptions.RegisterInto(files, inputs);
        ApprovalNodeTypeDescriptions.RegisterInto(files, inputs);
    }
}