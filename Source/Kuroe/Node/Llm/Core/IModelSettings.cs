using ApiHub.Shared.Models;

namespace Kuroe.Node.Llm;

/// <summary>执行请求用到的模型相关设置，由宿主提供。</summary>
public interface IModelSettings
{
    /// <summary>当前选中的模型名，未选择时为空。</summary>
    ModelName? Model { get; }
}
