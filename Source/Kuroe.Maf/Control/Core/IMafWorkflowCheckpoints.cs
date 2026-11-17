namespace Kuroe.Maf.Control;

/// <summary>运行期检查点的读写。实现负责与任务状态同步。</summary>
internal interface IMafWorkflowCheckpoints
{
    /// <summary>读取当前检查点，没有时为空。</summary>
    MafWorkflowCheckpoint? Load();

    /// <summary>保存检查点，实现可以按任务控制状态忽略写入。</summary>
    void Save(MafWorkflowCheckpoint checkpoint);
}