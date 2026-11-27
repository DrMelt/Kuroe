using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Maf.Definition;

/// <summary>Context 端口的一次内容：端口、文本与来源描述。</summary>
public sealed record MafContextValue(PortName Port, string Text, string? Source = null);