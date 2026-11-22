namespace Kuroe.Maf.Control;

/// <summary>检查点键的生成。运行期给跨进程唯一键，测试可给出可预期的固定序列。</summary>
public interface ICheckpointIdSource
{
    /// <summary>取下一个检查点键。</summary>
    string Next();
}
