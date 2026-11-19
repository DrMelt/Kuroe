using ErrorOr;
using Kuroe.Tasks.Shared;

namespace Kuroe.FlowDescription.Json.Files;

/// <summary>文件错误的作用域：节点库定义或某条流程，条目标识与说明在此拼成统一错误文本。</summary>
readonly record struct DescriptionScope
{
    private DescriptionScope(string name, bool isLibrary)
    {
        Name = name;
        IsLibrary = isLibrary;
    }

    /// <summary>节点库定义的作用域。</summary>
    internal static DescriptionScope Library { get; } = new("节点库", true);

    /// <summary>一条流程的作用域，name 是流程名。</summary>
    internal static DescriptionScope Flow(string name) => new(name, false);

    /// <summary>作用域名：库作用域是固定文本，流程作用域是流程名。</summary>
    internal string Name { get; }

    /// <summary>是否节点库定义的作用域。</summary>
    internal bool IsLibrary { get; }

    /// <summary>作用域内一个节点的错误。</summary>
    internal Error Node(string node, string message) =>
        Error.Validation(FlowErrorCodes.FlowEntry, IsLibrary
            ? $"节点库定义 {node}：{message}"
            : $"流程 {Name} 的节点 {node}：{message}");
}