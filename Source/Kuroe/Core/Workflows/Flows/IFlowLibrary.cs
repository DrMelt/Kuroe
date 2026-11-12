using ErrorOr;

namespace Kuroe.Workflows.Flows;

/// <summary>宿主可用的流程库操作：不可用流程与文件导入。</summary>
public interface IFlowLibrary
{
    /// <summary>不可用流程：流程名与编译错误，按文件顺序。</summary>
    IReadOnlyList<FlowFailure> Failures { get; }

    /// <summary>从文件导入节点库或流程，同名覆盖。</summary>
    ErrorOr<FlowImport> Import(string source);
}