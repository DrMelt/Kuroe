namespace Kuroe.Node.Llm.Executions;

/// <summary>一次执行的模型请求次数预算，每次请求取一次。</summary>
internal sealed class TurnBudget(int limit)
{
    private int _remaining = limit;

    /// <summary>预算的上限。</summary>
    public int Limit { get; } = limit;

    /// <summary>取一次请求名额，用尽时返回 false。</summary>
    public bool Take()
    {
        if (_remaining <= 0)
        {
            return false;
        }

        _remaining--;

        return true;
    }
}