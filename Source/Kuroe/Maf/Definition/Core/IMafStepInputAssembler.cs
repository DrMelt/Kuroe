namespace Kuroe.Maf.Definition;

/// <summary>步骤输入装配：按上下文把上游产出与流程开始输入映射为本次执行的输入。</summary>
public interface IMafStepInputAssembler
{
    /// <summary>装配本次执行的输入取用，按输入端口排列。</summary>
    IReadOnlyList<MafStepInput> Assemble(MafStepInputContext context);
}