using Kuroe.FlowDescription.Definition.Shared.Ports;

namespace Kuroe.FlowDescription.Semantics.Shared;

/// <summary>一个输出端口的来源解析：端口声明、直接引用与来源终点。固定值输出没有直接引用，动态执行产出没有静态终点。</summary>
public sealed record FlowSemanticOutput(Port Port, FlowBinding? Binding, FlowSource? Source);