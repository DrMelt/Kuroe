namespace Kuroe.Maf.Control;

/// <summary>以唯一键生成检查点标识，进程间不重复。</summary>
public sealed class GuidCheckpointIdSource : ICheckpointIdSource
{
    /// <inheritdoc/>
    public string Next() => Guid.NewGuid().ToString("N");
}
