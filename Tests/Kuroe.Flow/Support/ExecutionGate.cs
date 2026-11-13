using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Execution;

namespace Kuroe.Flow.TestSupport;

/// <summary>执行闸门的实现：在场执行按执行号登记，保持集合按节点生效。</summary>
public sealed class ExecutionGate : IExecutionGate
{
    private readonly Lock _gate = new();
    private readonly Dictionary<int, Entry> _entries = [];
    private readonly HashSet<NodePath> _held = [];
    private readonly ChangeSignal _signal = new();
    private bool _holdAll;

    public ExecutionHandle Arrive(ExecutionId execution, NodePath node, int round)
    {
        ExecutionHandle handle = new(execution, node, round);
        lock (_gate)
        {
            _entries[execution.Value] = new Entry(handle);
        }

        _signal.Raise();

        return handle;
    }

    public Task WaitOpen(ExecutionHandle handle)
    {
        Task open;
        lock (_gate)
        {
            if (!_entries.TryGetValue(handle.Id.Value, out Entry? entry)
                || !_holdAll && !_held.Contains(entry.Handle.Node))
            {
                return Task.CompletedTask;
            }

            open = entry.Open.Task;
        }

        return WaitOpenAsync(handle, open);
    }

    public void Leave(ExecutionHandle handle)
    {
        lock (_gate)
        {
            _entries.Remove(handle.Id.Value);
        }

        _signal.Raise();
    }

    public void Hold(NodePath node)
    {
        lock (_gate)
        {
            _held.Add(node);
        }
    }

    public void HoldAll()
    {
        lock (_gate)
        {
            _holdAll = true;
        }
    }

    public void ClearHolds()
    {
        List<TaskCompletionSource> opened;
        lock (_gate)
        {
            _held.Clear();
            _holdAll = false;
            opened = [.. _entries.Values.Select(entry => entry.Open)];
        }

        foreach (TaskCompletionSource open in opened)
        {
            open.TrySetResult();
        }
    }

    public Task<ExecutionHandle> Entered(NodePath node) => Observe(node);

    public Task<ExecutionHandle[]> Entered(IReadOnlyList<NodePath> nodes) => Observe(nodes);

    public void Open(ExecutionHandle handle)
    {
        TaskCompletionSource? open;
        lock (_gate)
        {
            open = _entries.TryGetValue(handle.Id.Value, out Entry? entry) ? entry.Open : null;
        }

        open?.TrySetResult();
    }

    public void Open(NodePath node)
    {
        List<TaskCompletionSource> opened;
        lock (_gate)
        {
            _held.Remove(node);
            opened =
            [
                .. _entries.Values
                    .Where(entry => entry.Handle.Node == node)
                    .Select(entry => entry.Open),
            ];
        }

        foreach (TaskCompletionSource open in opened)
        {
            open.TrySetResult();
        }
    }

    private async Task<ExecutionHandle> Observe(NodePath node)
    {
        while (true)
        {
            long version = _signal.Version;
            if (Present(node) is { } found)
            {
                return found;
            }

            await NextChangeAsync(node.Value, version).ConfigureAwait(false);
        }
    }

    private async Task<ExecutionHandle[]> Observe(IReadOnlyList<NodePath> nodes)
    {
        while (true)
        {
            long version = _signal.Version;
            if (Present(nodes) is { } found)
            {
                return found;
            }

            await NextChangeAsync(string.Join("、", nodes.Select(node => node.Value)), version).ConfigureAwait(false);
        }
    }

    /// <summary>给定节点的在场执行，没有时返回 null。</summary>
    private ExecutionHandle? Present(NodePath node)
    {
        lock (_gate)
        {
            foreach (Entry entry in _entries.Values)
            {
                if (entry.Handle.Node == node)
                {
                    return entry.Handle;
                }
            }

            return null;
        }
    }

    /// <summary>这些节点同时各有一次在场执行，任一缺席时返回 null。</summary>
    private ExecutionHandle[]? Present(IReadOnlyList<NodePath> nodes)
    {
        ExecutionHandle[] found = new ExecutionHandle[nodes.Count];
        lock (_gate)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                ExecutionHandle? handle = null;
                foreach (Entry entry in _entries.Values)
                {
                    if (entry.Handle.Node == nodes[i])
                    {
                        handle = entry.Handle;
                        break;
                    }
                }

                if (handle is null)
                {
                    return null;
                }

                found[i] = handle.Value;
            }
        }

        return found;
    }

    private async Task NextChangeAsync(string target, long version)
    {
        try
        {
            await _signal.WaitAsync(version).WaitAsync(TestTimeouts.HangGuard).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException(
                $"{target} 在 {TestTimeouts.HangGuard.TotalSeconds} 秒内没有执行进入或离开。");
        }
    }

    private static async Task WaitOpenAsync(ExecutionHandle handle, Task open)
    {
        try
        {
            await open.WaitAsync(TestTimeouts.HangGuard).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException(
                $"执行 {handle.Id.Value} 在 {TestTimeouts.HangGuard.TotalSeconds} 秒内没有被放行。");
        }
    }

    private sealed class Entry(ExecutionHandle handle)
    {
        /// <summary>在场执行的句柄。</summary>
        public ExecutionHandle Handle { get; } = handle;

        /// <summary>放行信号，测试放行后完成。</summary>
        public TaskCompletionSource Open { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}