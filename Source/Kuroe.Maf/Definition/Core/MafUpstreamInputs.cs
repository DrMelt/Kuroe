using Kuroe.Execution.Shared.Ports;

namespace Kuroe.Maf.Definition;

/// <summary>上游产出即输入的装配：上游每个端口成为必填的文本内容输入。</summary>
public sealed class MafUpstreamInputs : IMafStepInputAssembler
{
    /// <summary>上游端口直接作为输入的装配。</summary>
    public static MafUpstreamInputs Instance { get; } = new();

    /// <inheritdoc/>
    public IReadOnlyList<MafStepInput> Assemble(MafStepInputContext context) =>
        [.. context.Upstream.Select(output => new MafStepInput(
            output.Port,
            ExecutionPortKind.Text,
            ExecutionPortRole.Content,
            Required: true,
            DefaultText: null,
            Source: null,
            SourceKey: null,
            output.Text,
            output.Step))];
}