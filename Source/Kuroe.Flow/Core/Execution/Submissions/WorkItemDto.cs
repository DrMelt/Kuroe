namespace Kuroe.Flow.Execution.Submissions;

/// <summary>模型交回的条目文本的形状，不含序号与去空白后的结果。</summary>
internal sealed class WorkItemDto
{
    public string? Title { get; set; }

    public string? Instruction { get; set; }

    public string? Acceptance { get; set; }

    public string? Branch { get; set; }
}
