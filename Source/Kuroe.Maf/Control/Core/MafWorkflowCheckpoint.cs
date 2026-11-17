namespace Kuroe.Maf.Control;

/// <summary>运行期检查点：会话标识、检查点标识与 JSON 文本，三者同时有效。</summary>
internal sealed record MafWorkflowCheckpoint(string Session, string Id, string Data);