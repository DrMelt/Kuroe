using Kuroe.Flow.Compiler.Description;
using Kuroe.Flow.InputChannel.Shared;
using Kuroe.Flow.NodeType.Container;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Node.Approval;
using Kuroe.Node.Input;
using Kuroe.Node.Llm;

namespace Kuroe.Tests.Support;

/// <summary>测试用的节点类型描述注册表：引擎内置的容器与扩展提供的各节点类型。</summary>
internal static class TestTypeFiles
{
    /// <summary>建出一份含全部可用类型的注册表，外部输入协议取调用方给出的实例。</summary>
    internal static NodeTypeDescriptionRegistry Registry(IInputChannel inputs)
    {
        NodeTypeDescriptionRegistry files = new();
        ContainerNodeTypeDescriptions.RegisterInto(files);
        LlmNodeTypeDescriptions.RegisterInto(files);
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