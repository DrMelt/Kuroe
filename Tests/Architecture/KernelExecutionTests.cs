using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryAssertions;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>执行模块边界：共享层只放契约，核心层只引共享契约，记录与运行时暴露控制命令而不是状态迁移。</summary>
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

    [Fact(DisplayName = "执行项目不含流程控制词汇")]
    public void Execution_projects_stay_free_of_flow_control_vocabulary()
    {
        string execution = Path.Combine(KernelRoot, "Execution");
        string pattern = @"(?<![A-Za-z])(FlowStepPath|StartInputs|FlowName|FlowSeed|PortName)(?![A-Za-z])";

        Assert.NotEmpty(SourceFiles(execution));
        foreach (string file in SourceFiles(execution))
        {
            string text = File.ReadAllText(file);

            Assert.False(
                System.Text.RegularExpressions.Regex.IsMatch(text, pattern),
                $"{Path.GetFileName(file)} 使用了流程控制词汇。");
            Assert.DoesNotContain("Kuroe.Tasks", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Kuroe.Flow.Node", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Kuroe.Flow.Control", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Kuroe.Control", text, StringComparison.Ordinal);
        }
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

    [Fact(DisplayName = "执行项目不含图与节点语义")]
    public void Execution_projects_stay_free_of_graph_and_node_semantics()
    {
        string execution = Path.Combine(KernelRoot, "Execution");
        string[] forbidden =
        [
            "NodePath",
            "NodeGraph",
            "ExecutableNode",
            "ModelPort",
            "GraphExecution",
            "RuntimeGraph",
            "StaticGraph",
            "IExecutionNodeDeclarationResolver",
            "IsConstant",
            "RequiresSubmission",
            "PortMode",
            "NodeEmission",
        ];

        SourcesExclude(execution, "图或节点语义", forbidden);
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

    [Fact(DisplayName = "执行共享层不定义具体状态机")]
    public void Execution_shared_defines_no_concrete_state_machine()
    {
        string shared = Path.Combine(KernelRoot, "Execution", "Shared");
        string core = Path.Combine(KernelRoot, "Execution", "Core");

        Assert.False(File.Exists(Path.Combine(shared, "ExecutionInstance.cs")), $"{Path.Combine(shared, "ExecutionInstance.cs")} 不应存在。");
        Assert.True(File.Exists(Path.Combine(core, "ExecutionInstance.cs")), $"{Path.Combine(core, "ExecutionInstance.cs")} 不存在。");
        Assert.All(
            SourceFiles(shared),
            file => Assert.DoesNotContain(
                "sealed class ExecutionInstance :",
                File.ReadAllText(file),
                StringComparison.Ordinal));
    }

    [Fact(DisplayName = "执行运行期暴露控制命令而不是状态迁移")]
    public void Execution_runtime_exposes_control_commands_not_state_transitions()
    {
        string file = Path.Combine(
            KernelRoot,
            "Execution",
            "Shared",
            "Runtime",
            "IExecutionRuntime.cs");
        string text = File.ReadAllText(file);
        string[] forbidden =
        [
            "MarkRunning",
            "MarkFailed",
            "MarkSucceeded",
            "MarkInterrupted",
            "MarkUncollected",
            "AttachJournal",
            "AttachUsageReader",
            "SettleUsage",
            "AttachRestoreDataReader",
            "SettleRestoreData",
            "ContinueFrom",
            "CreateRunner",
            "CreateRestored",
            "DispatchAsync",
            "Succeed(",
            "Fail(",
        ];

        foreach (string word in forbidden)
        {
            Assert.DoesNotContain(word, text, StringComparison.Ordinal);
        }
    }

    [Fact(DisplayName = "执行派发器只调度运行")]
    public void Execution_dispatcher_only_schedules_runs()
    {
        string core = Path.Combine(KernelRoot, "Execution", "Core");
        string dispatcher = File.ReadAllText(Path.Combine(core, "ExecutionDispatcher.cs"));
        string run = Path.Combine(core, "ExecutionRun.cs");

        Assert.True(File.Exists(run), $"{run} 不存在。");
        foreach (string word in (string[])["AttachJournal", "IExecutionUsageSource", "IExecutionRestoreDataSource", "SettleUsage", "SettleRestoreData"])
        {
            Assert.DoesNotContain(word, dispatcher, StringComparison.Ordinal);
        }
    }
}
