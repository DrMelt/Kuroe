namespace Kuroe.Shared.Workflows.Flows;

/// <summary>容器传入端口声明：名字与说明。传入端口不落种类，种类由引用处的绑定来源端口决定；说明未写时为空。</summary>
public readonly record struct ContainerInput(PortName Name, string? Description = null)
{
    /// <summary>端口名的文本形式。</summary>
    public override string ToString() => Name.Value;
}
