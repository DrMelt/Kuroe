using Kuroe.Maf.Definition;
using Microsoft.Agents.AI.Workflows;

namespace Kuroe.Maf.Control;

/// <summary>执行一个步骤：接收输入绑定、按启动组判定齐备、调用执行桥接并发送产出。</summary>
internal delegate ValueTask<MafStepMessage> MafStepRun(
    MafStep step,
    IReadOnlyList<MafStepInput> inputs,
    CancellationToken cancellationToken);

/// <summary>MAF 步骤节点：按输入计划累积消息，组齐备后只执行一次。</summary>
internal sealed class MafStepNode(
    MafStep step,
    Func<IReadOnlyList<MafStepOutput>, IReadOnlyList<MafStepInput>> plans,
    MafStepRun run) : Executor<MafStepMessage>(step.Id.Value)
{
    private const string TextPrefix = "text:";
    private const string FiresPrefix = "fires:";
    private const string ContextPrefix = "context:";
    private const string SeenPrefix = "seen:";
    private const string DoneKey = "done";
    private const char KeySeparator = '|';

    private IReadOnlyList<MafStepInput> _current = [];

    private string InputScope => $"maf:{step.Id.Value}:inputs";

    private string MetaScope => $"maf:{step.Id.Value}:meta";

    [SendsMessage(typeof(MafStepMessage))]
    public override async ValueTask HandleAsync(
        MafStepMessage message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        if (await DoneAsync(context, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        IReadOnlyList<MafStepInput> current = plans(message.Outputs);
        _current = current;
        for (int index = 0; index < current.Count; index++)
        {
            MafStepInput input = current[index];
            if (input.UpstreamStep is not { } upstream || input.UpstreamPort is not { } port)
            {
                continue;
            }

            MafStepOutput? output = message.Outputs.FirstOrDefault(candidate =>
                candidate.Step == upstream && candidate.Port == port);
            if (output is null)
            {
                continue;
            }

            string key = Key(input);
            MafContextValue[] values = [.. output.Context];
            await context.QueueStateUpdateAsync(TextKey(key), output.Text, InputScope, cancellationToken).ConfigureAwait(false);
            await context.QueueStateUpdateAsync(FiresKey(key), output.Fires, InputScope, cancellationToken).ConfigureAwait(false);
            await context.QueueStateUpdateAsync(ContextKey(key), values, InputScope, cancellationToken).ConfigureAwait(false);
            await context.QueueStateUpdateAsync(SeenKey(key), true, InputScope, cancellationToken).ConfigureAwait(false);
        }
    }

    protected override async ValueTask OnMessageDeliveryFinishedAsync(
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        if (await DoneAsync(context, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        IReadOnlyList<MafStepInput> current = _current;
        if (current.Count == 0)
        {
            await ExecuteAsync([], context, cancellationToken).ConfigureAwait(false);
            return;
        }

        bool[] available = new bool[current.Count];
        bool[] accepted = new bool[current.Count];
        for (int index = 0; index < current.Count; index++)
        {
            MafStepInput input = current[index];
            if (input.UpstreamStep is null)
            {
                available[index] = true;
                accepted[index] = input.Condition == MafTriggerCondition.Positive;
                continue;
            }

            bool seen = await context
                .ReadStateAsync<bool>(SeenKey(Key(input)), InputScope, cancellationToken)
                .ConfigureAwait(false);
            if (!seen)
            {
                continue;
            }

            bool fires = await context
                .ReadStateAsync<bool>(FiresKey(Key(input)), InputScope, cancellationToken)
                .ConfigureAwait(false);
            available[index] = true;
            accepted[index] = input.Condition == MafTriggerCondition.Negative ? !fires : fires;
        }

        bool staticReady = true;
        for (int index = 0; index < current.Count; index++)
        {
            MafStepInput input = current[index];
            if (input.Group < 0 && input.UpstreamStep is not null && (!available[index] || !accepted[index]))
            {
                staticReady = false;
                break;
            }
        }

        IGrouping<int, (MafStepInput Input, int Index)>? group = current
            .Select((input, index) => (Input: input, Index: index))
            .Where(item => item.Input.Group >= 0)
            .GroupBy(item => item.Input.Group)
            .Where(candidate => candidate.All(item => available[item.Index] && accepted[item.Index]))
            .OrderBy(candidate => candidate.Key)
            .FirstOrDefault();

        bool hasDynamic = current.Any(input => input.Group >= 0);
        if ((hasDynamic && group is null) || !staticReady)
        {
            return;
        }

        List<int> selected =
        [
            .. current.Select((input, index) => (input, index))
                .Where(item => item.input.Group < 0)
                .Select(item => item.index),
            .. group?.Select(item => item.Index) ?? [],
        ];

        await ExecuteAsync(selected, context, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask ExecuteAsync(
        IReadOnlyList<int> selected,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        if (await DoneAsync(context, cancellationToken).ConfigureAwait(false))
        {
            await context.QueueClearScopeAsync(InputScope, cancellationToken).ConfigureAwait(false);
            return;
        }

        List<MafStepInput> inputs = [];
        foreach (int index in selected.Distinct().Order())
        {
            inputs.Add(await MaterializeAsync(index, context, cancellationToken).ConfigureAwait(false));
        }

        MafStepMessage output = await run(step, inputs, cancellationToken).ConfigureAwait(false);
        await context.QueueStateUpdateAsync(DoneKey, true, MetaScope, cancellationToken).ConfigureAwait(false);
        await context.QueueClearScopeAsync(InputScope, cancellationToken).ConfigureAwait(false);
        await context.SendMessageAsync(output, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<MafStepInput> MaterializeAsync(
        int index,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        MafStepInput input = _current[index];
        if (input.UpstreamStep is null)
        {
            return input;
        }

        string text = await context
            .ReadStateAsync<string>(TextKey(Key(input)), InputScope, cancellationToken)
            .ConfigureAwait(false) ?? input.Text;
        MafContextValue[]? values = await context
            .ReadStateAsync<MafContextValue[]>(ContextKey(Key(input)), InputScope, cancellationToken)
            .ConfigureAwait(false);

        return input with
        {
            Text = text,
            Context = values is { Length: > 0 } ? values : input.Context,
        };
    }

    private async ValueTask<bool> DoneAsync(IWorkflowContext context, CancellationToken cancellationToken) =>
        await context
            .ReadStateAsync<bool>(DoneKey, MetaScope, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>输入绑定的状态键：目标端口、来源步骤与来源端口确定一条绑定。</summary>
    private static string Key(MafStepInput input) =>
        string.Join(
            KeySeparator,
            input.Port.Value,
            input.UpstreamStep?.Value ?? string.Empty,
            input.UpstreamPort?.Value ?? string.Empty);

    private static string TextKey(string binding) => $"{TextPrefix}{binding}";

    private static string FiresKey(string binding) => $"{FiresPrefix}{binding}";

    private static string ContextKey(string binding) => $"{ContextPrefix}{binding}";

    private static string SeenKey(string binding) => $"{SeenPrefix}{binding}";
}