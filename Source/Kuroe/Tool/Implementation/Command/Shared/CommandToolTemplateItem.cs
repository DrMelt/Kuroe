namespace Kuroe.Tool.Command.Shared;

/// <summary>模板里的一个参数项。Text 是命令行文字，可含 {参数名} 占位符；OmitWhenMissing 为关联参数名，
/// 该参数缺省时整个参数项从命令中去掉，null 表示普通参数项。</summary>
/// <param name="Text">命令行文字。</param>
/// <param name="OmitWhenMissing">关联参数名，null 表示普通参数项。</param>
public sealed record CommandToolTemplateItem(string Text, string? OmitWhenMissing);