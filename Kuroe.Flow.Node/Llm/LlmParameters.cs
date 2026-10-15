using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.Node.Llm;

/// <summary>Llm 类型的参数：对执行单元的额外要求、能力白名单与恒定系统指令块。</summary>
public sealed record LlmParameters : INodeParameters, INodeParameterRun
{
    /// <summary>对执行单元的额外要求，与目标一起构成指令。</summary>
    public string? Prompt { get; init; }

    /// <summary>能力白名单，按能力路径前缀匹配，写上级路径即放行整棵子树。未写或空时不给出任何能力。</summary>
    public IReadOnlyList<CapabilityPath> Tools { get; init; } = [];

    /// <summary>恒定系统指令块：作为系统指令置于请求最前，内容必须恒定以命中服务商前缀缓存。</summary>
    public IReadOnlyList<string> SystemPrompt { get; init; } = [];

    /// <summary>本轮请求内容：节点要求在指令最前，恒定块在系统指令最前，能力面在契约能力之前。</summary>
    public RunRequest Compose(RunMaterials materials)
    {
        List<string> instruction = [];
        if (Prompt is { Length: > 0 } prompt)
        {
            instruction.Add(prompt);
        }

        instruction.AddRange(materials.Steps);

        List<string> blocks = [.. SystemPrompt];
        if (materials.Briefing.Length > 0)
        {
            if (blocks.Count > 0)
            {
                blocks.Add(string.Empty);
            }

            blocks.Add(materials.Briefing);
        }

        return new RunRequest
        {
            Instruction = string.Join('\n', instruction),
            SystemPrompt = blocks.Count == 0 ? null : string.Join('\n', blocks),
            Tools = [.. Tools, .. materials.ContractTools],
        };
    }
}