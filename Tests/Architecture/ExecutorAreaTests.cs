using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryAssertions;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>执行器区域边界：共享层只引用任务契约，装配层只引用执行器共享层、流程描述与任务契约。</summary>
public sealed class ExecutorAreaTests
{
    [Fact(DisplayName = "执行器共享层只引用任务契约")]
    public void Executor_shared_references_only_task_contracts()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Executor",
            "Shared",
            "Kuroe.Executor.Shared.csproj");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");

        ReferencesStayUnder(project, "Kuroe.Executor.Shared", "任务契约", tasks);
    }

    [Fact(DisplayName = "执行器装配层只引用执行器共享、流程描述与任务契约")]
    public void Executor_compilation_references_only_shared_description_and_task_contracts()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Executor",
            "Compilation",
            "Core",
            "Kuroe.Executor.Compilation.csproj");
        string shared = Path.Combine(SourceRoot, "Kuroe.Executor", "Shared");
        string document = Path.Combine(SourceRoot, "Kuroe.FlowDescription", "Shared");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");

        ReferencesStayUnder(
            project,
            "Kuroe.Executor.Compilation",
            "执行器共享层、流程描述与任务契约",
            shared,
            document,
            tasks);
    }
}