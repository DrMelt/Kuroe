namespace Kuroe.Executor.Shared.Nodes;

/// <summary>执行器描述注册表：种类名到描述的唯一入口，同名覆盖，按注册序保留种类名。</summary>
public interface IExecutorRegistry
{
    /// <summary>注册一个描述，同名覆盖。</summary>
    void Register(IExecutorDescription description);

    /// <summary>按种类取描述，未注册时为空。</summary>
    IExecutorDescription? Find(ExecutorKind kind);

    /// <summary>按文本种类名取描述，名字不区分大小写，未注册时为空。</summary>
    IExecutorDescription? Find(string name);

    /// <summary>已注册的种类名，按注册序。</summary>
    IReadOnlyList<ExecutorKind> Kinds { get; }
}