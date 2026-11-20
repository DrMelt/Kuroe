namespace Kuroe.Kernel.Control.Shared;

/// <summary>流程模板来源的装配期登记入口。</summary>
public interface IFlowTemplateRegistrar
{
    /// <summary>登记一个流程模板来源。</summary>
    void Register(IFlowTemplateSource source);
}