using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Tool;

namespace Kuroe.Node.Llm;

/// <summary>Llm 节点描述的注册入口。</summary>
public static class LlmNodeTypeDescriptions
{
    /// <summary>把 Llm 节点的描述注册进注册表，不提供工具契约。</summary>
    public static void RegisterInto(INodeTypeDescriptionRegistry files) =>
        RegisterInto(files, EmptyToolContractCatalog.Instance);

    /// <summary>把 Llm 节点的描述注册进注册表，装载期按给定契约校验工具映射。</summary>
    public static void RegisterInto(INodeTypeDescriptionRegistry files, IToolContractCatalog contracts) =>
        files.Register(new LlmNodeTypeDescription(contracts));
}