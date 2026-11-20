using Kuroe.Kernel.Execution.Shared.Ports;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Maf.Definition;

/// <summary>步骤执行的一个输入取用：目标端口、取值形状、取值与来源。</summary>
/// <param name="Port">目标输入端口。</param>
/// <param name="Kind">端口取值种类。</param>
/// <param name="Role">执行角色，只有内容角色进入执行的内容输入。</param>
/// <param name="Required">是否必须给出取值。</param>
/// <param name="DefaultText">省略取值时的回退文本。</param>
/// <param name="Source">来源描述，未给出时由流程控制按上游端口给出。</param>
/// <param name="SourceKey">执行记录使用的来源标识，未给出时使用来源描述。</param>
/// <param name="Text">取到的内容。</param>
/// <param name="UpstreamStep">取值来自的上游步骤，取值不来自步骤时为空。</param>
public sealed record MafStepInput(
    PortName Port,
    ExecutionPortKind Kind,
    ExecutionPortRole Role,
    bool Required,
    string? DefaultText,
    string? Source,
    string? SourceKey,
    string Text,
    MafStepId? UpstreamStep);