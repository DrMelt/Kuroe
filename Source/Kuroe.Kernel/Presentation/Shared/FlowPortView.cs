namespace Kuroe.Presentation.Shared;

/// <summary>展示用端口：名称、种类、文本与触发位。</summary>
public sealed record FlowPortView(
    string Name,
    string Kind,
    string? Text,
    bool Fires = true)
{
    /// <summary>端口是否为运行期常量，文本即常量值。</summary>
    public bool Constant { get; init; }

    /// <summary>输入端口是否为静态配置，模板视图使用。</summary>
    public bool Static { get; init; }
}