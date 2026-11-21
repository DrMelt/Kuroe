using Kuroe.Kernel.Control.Shared;

namespace Kuroe.Maf.Control;

/// <summary>把运行期检查点写入 MAF 流程状态，写入受 <see cref="ITaskFlowContext.Gate"/> 与检查点冻结控制。</summary>
internal sealed class MafFlowCheckpoints(ITaskFlowContext task, MafFlowState state) : IMafWorkflowCheckpoints
{
    /// <inheritdoc/>
    public MafWorkflowCheckpoint? Load()
    {
        lock (task.Gate)
        {
            return state.Checkpoint;
        }
    }

    /// <inheritdoc/>
    public void Save(MafWorkflowCheckpoint checkpoint)
    {
        lock (task.Gate)
        {
            if (!state.FreezeCheckpoint)
            {
                state.Checkpoint = checkpoint;
            }
        }
    }
}