using Kuroe.Flow.InputChannel;
using Kuroe.Flow.Json.Parameters;
using Kuroe.Node.Approval.Json;
using Kuroe.Node.Input.Json;
using Kuroe.Node.Llm.Json;

namespace Kuroe.Tests.Support;

/// <summary>测试用的节点类型文件形状注册表：引擎内置的容器与扩展提供的各节点类型。</summary>
internal static class TestTypeFiles
{
    /// <summary>建出一份含全部可用类型的注册表，外部输入协议取调用方给出的实例。</summary>
    internal static NodeTypeFileRegistry Registry(IInputChannel inputs)
    {
        NodeTypeFileRegistry files = new();
        ContainerTypeFiles.RegisterInto(files);
        LlmTypeFiles.RegisterInto(files);
        InputTypeFiles.RegisterInto(files, inputs);
        ApprovalTypeFiles.RegisterInto(files, inputs);

        return files;
    }
}