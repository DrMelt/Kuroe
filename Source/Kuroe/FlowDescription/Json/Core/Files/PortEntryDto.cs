namespace Kuroe.FlowDescription.Json.Files;

/// <summary>端口的对象形状：输出端口与输入端口共用条目。Type 写类型名，Mode 写 Plain、Trigger 或 Constant。</summary>
public sealed class PortEntryDto
{
    /// <summary>端口名。</summary>
    public string? Name { get; set; }

    /// <summary>端口类型名，取值由端口类型注册表决定。未写时按 Text。</summary>
    public string? Type { get; set; }

    /// <summary>端口模式：Plain、Trigger 或 Constant。未写时按类型默认模式。</summary>
    public string? Mode { get; set; }

    /// <summary>输出端口的来源引用，写 成员@端口、来源@端口 或 @本节点输入。</summary>
    public string? Ref { get; set; }

    /// <summary>端口承载内容的说明，未写时为空。</summary>
    public string? Description { get; set; }

    /// <summary>常量输出端口的固定值，未写时由 Ref 或 Once 执行产出提供来源。</summary>
    public string? Value { get; set; }

    /// <summary>默认值：开始端口表示提交时的预填值与省略时的回退值，输入端口表示来源组没有连接该端口时的回退值，其他输出端口不使用。</summary>
    public string? Default { get; set; }

    /// <summary>输入端口的执行角色，取值 Content、Configuration 或 Control。</summary>
    public string? Role { get; set; }
}
