using Kuroe.Flow.Json.Parameters;
using Kuroe.Flow.Node.Approval.Json;
using Kuroe.Flow.Node.Input.Json;
using Kuroe.Flow.Node.Llm.Json;

namespace Kuroe.Tests.Support;

/// <summary>测试用的参数段形状注册表：扩展提供的各节点类型形状。</summary>
internal static class TestParameterFiles
{
    /// <summary>建出一份含全部可用形状的注册表。</summary>
    internal static NodeParameterFileRegistry Registry()
    {
        NodeParameterFileRegistry files = new();
        LlmParameterFiles.RegisterInto(files);
        InputParameterFiles.RegisterInto(files);
        ApprovalParameterFiles.RegisterInto(files);

        return files;
    }
}