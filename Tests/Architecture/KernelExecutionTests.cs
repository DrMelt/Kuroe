using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>执行模块边界：共享层只放契约，核心层只引共享契约，记录保存来源标识。</summary>
public sealed class KernelExecutionTests
{
    [Fact(DisplayName = "执行共享层不引用其他项目")]
    public void Execution_shared_project_has_no_project_references()
    {
        string project = Path.Combine(
            KernelRoot,
            "Execution",
            "Shared",
            "Kuroe.Execution.Shared.csproj");

        Assert.Empty(ProjectReferences(project));
    }

    [Fact(DisplayName = "执行核只引用执行契约")]
    public void Execution_core_references_only_execution_contracts()
    {
        string shared = Path.Combine(
            KernelRoot,
            "Execution",
            "Shared",
            "Kuroe.Execution.Shared.csproj");
        string core = Path.Combine(
            KernelRoot,
            "Execution",
            "Core",
            "Kuroe.Execution.csproj");

        Assert.Equal([shared], ProjectReferences(core));
    }

    [Fact(DisplayName = "执行记录项目只引用执行共享层")]
    public void Execution_records_project_references_only_execution_shared()
    {
        string project = Path.Combine(
            KernelRoot,
            "Execution",
            "Records",
            "Shared",
            "Kuroe.Execution.Records.Shared.csproj");

        IReadOnlyList<string> references = ProjectReferences(project);

        Assert.Single(references);
        Assert.Contains(
            Path.Combine(KernelRoot, "Execution", "Shared", "Kuroe.Execution.Shared.csproj"),
            references);
    }

    [Fact(DisplayName = "执行记录保存来源标识")]
    public void Execution_records_capture_the_source_key()
    {
        string capture = Path.Combine(
            KernelRoot, "Execution", "Records", "Shared", "ExecutionRecordCapture.cs");

        Assert.Contains("SourceKey", File.ReadAllText(capture), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "执行状态机位于执行核")]
    public void Execution_state_machine_lives_in_core()
    {
        string shared = Path.Combine(KernelRoot, "Execution", "Shared");
        string core = Path.Combine(KernelRoot, "Execution", "Core");

        Assert.False(File.Exists(Path.Combine(shared, "ExecutionInstance.cs")), $"{Path.Combine(shared, "ExecutionInstance.cs")} 不应存在。");
        Assert.True(File.Exists(Path.Combine(core, "ExecutionInstance.cs")), $"{Path.Combine(core, "ExecutionInstance.cs")} 不存在。");
    }
}
