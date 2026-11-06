using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Xunit;

namespace Kuroe.Flow.Shared.Tests.RuntimeGraph.Execution;

/// <summary>过程记录存储：默认保留全部记录。</summary>
public sealed class ProcessJournalStoreTests
{
    [Fact]
    public void Default_store_keeps_all_entries()
    {
        ProcessJournalStore store = new();

        for (int index = 0; index <= 400; index++)
        {
            store.Append(Entry(index.ToString()));
        }

        Assert.Equal(401, store.Entries.Count);
        Assert.Equal(0, store.DroppedEntries);
        Assert.Equal("0", store.Entries[0].Text);
        Assert.Equal("400", store.Entries[^1].Text);
    }

    private static JournalEntry Entry(string text) =>
        new(DateTimeOffset.UtcNow, JournalLevel.Information, "条目", text);
}