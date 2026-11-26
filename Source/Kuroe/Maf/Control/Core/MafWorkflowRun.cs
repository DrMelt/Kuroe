using System.Runtime.CompilerServices;
using Kuroe.Maf.Definition;
using Microsoft.Agents.AI.Workflows;

namespace Kuroe.Maf.Control;

/// <summary>一次 MAF 运行，事件流来自 MAF 运行期，事件携带执行器对应的步骤。</summary>
internal sealed class MafWorkflowRun(
    StreamingRun run,
    IReadOnlyDictionary<string, MafStep> steps,
    string startId) : IMafWorkflowRun
{
    /// <inheritdoc/>
    public async IAsyncEnumerable<MafWorkflowEvent> WatchAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (WorkflowEvent update in run.WatchStreamAsync(cancellationToken).ConfigureAwait(false))
        {
            if (MafWorkflowEventTranslator.Translate(update, steps, startId) is { } translated)
            {
                yield return translated;
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => run.DisposeAsync();
}