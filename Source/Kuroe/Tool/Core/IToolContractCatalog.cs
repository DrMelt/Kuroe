namespace Kuroe.Tool;

/// <summary>工具契约的只读目录，供流程编译校验工具路径与路径变量。</summary>
public interface IToolContractCatalog
{
    /// <summary>全部工具契约，按注册序。</summary>
    IReadOnlyList<ToolContract> Contracts { get; }
}