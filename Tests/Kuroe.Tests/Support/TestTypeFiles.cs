using Kuroe.Flow.Json.Parameters;
using Kuroe.Flow.Node.Approval.Json;
using Kuroe.Flow.Node.Input.Json;
using Kuroe.Flow.Node.Llm.Json;

namespace Kuroe.Tests.Support;

/// <summary>测试用的节点类型文件形状注册表：引擎内置的容器与扩展提供的各节点类型。</summary>
internal static class TestTypeFiles
{
    /// <summary>建出一份含全部可用类型的注册表。</summary>
    internal static NodeTypeFileRegistry Registry()
    {
        NodeTypeFileRegistry files = new();
        ContainerTypeFiles.RegisterInto(files);
        LlmTypeFiles.RegisterInto(files);
        InputTypeFiles.RegisterInto(files);
        ApprovalTypeFiles.RegisterInto(files);

        return files;
    }
}