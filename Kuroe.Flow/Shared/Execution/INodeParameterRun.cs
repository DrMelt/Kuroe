namespace Kuroe.Flow.Shared.Execution;

/// <summary>节点类型参数的运行形状：按类型语义把参数与本轮材料合成为模型请求内容。
/// 与文件形状相对，引擎只调用合成，不读参数内容。</summary>
public interface INodeParameterRun
{
    /// <summary>把参数与本轮材料合成为本轮请求内容，材料是引擎侧给出的全部输入。</summary>
    RunRequest Compose(RunMaterials materials);
}