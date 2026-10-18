using ApiHub.Shared.Models;

namespace Kuroe.Node.Llm;

/// <summary>执行请求用到的模型相关设置，由宿主提供。</summary>
public interface IModelSettings
{
    /// <summary>当前选中的模型名，未选择时为空。</summary>
    ModelName? Model { get; }

    /// <summary>采样温度，未设置时为空。</summary>
    float? Temperature { get; }

    /// <summary>单轮输出上限，未设置时为空。</summary>
    int? MaxOutputTokens { get; }
}
