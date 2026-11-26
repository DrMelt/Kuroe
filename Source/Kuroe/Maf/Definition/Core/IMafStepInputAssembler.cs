namespace Kuroe.Maf.Definition;

/// <summary>步骤输入装配：按上下文把上游产出与流程开始输入映射为本次执行的输入。</summary>
public interface IMafStepInputAssembler
{
    /// <summary>装配本次执行的输入取用。一条绑定由目标端口与来源确定，节点按绑定身份保存来源取值。</summary>
    IReadOnlyList<MafStepInput> Assemble(MafStepInputContext context);
}