using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.TestSupport;

namespace Kuroe.Flow.Persistence.Json.Tests;

/// <summary>JSON 检查点编解码的契约实现。</summary>
public sealed class TaskCheckpointCodecTests : TaskCheckpointCodecContractTests
{
    protected override ITaskCheckpointCodec CreateCodec() => new TaskCheckpointCodec();
}
