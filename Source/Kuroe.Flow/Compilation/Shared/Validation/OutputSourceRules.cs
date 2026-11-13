using Kuroe.Flow.Node.StaticGraph.Ports;

namespace Kuroe.Flow.Compilation.Shared.Validation;

/// <summary>输出与来源端口的绑定规则：常量性与类型必须一致，触发由输出自身声明，来源没有触发值时按触发处理。</summary>
public static class OutputSourceRules
{
    /// <summary>逐条给出常量性与类型不一致的说明。</summary>
    public static IEnumerable<string> Check(Port output, PortType sourceType, bool sourceConstant, string source)
    {
        if (output.IsConstant != sourceConstant)
        {
            yield return $"输出端口 {output.Name.Value} 的常量性必须与来源 {source} 一致。";
        }

        if (output.Type != sourceType)
        {
            yield return $"输出端口 {output.Name.Value} 的类型必须与来源 {source} 一致。";
        }
    }
}
