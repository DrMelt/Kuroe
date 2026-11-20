using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryAssertions;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>控制模块边界：控制契约只描述流程视图与命令，流程运行时留在 Kernel 自己的项目内。</summary>
public sealed class KernelControlTests
{
    [Fact(DisplayName = "流程运行期独立成项目")]
    public void Flow_runtime_lives_in_its_own_project()
    {
        string runtime = Path.Combine(SourceRoot, "Kuroe.Kernel", "Control", "Runtime", "FlowRuntime.cs");
        string project = Path.Combine(SourceRoot, "Kuroe.Kernel", "Control", "Runtime", "Kuroe.Kernel.Control.Runtime.csproj");
        string old = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Control",
            "Core",
            "Runtime",
            "Engine",
            "FlowRuntime.cs");

        Assert.True(File.Exists(runtime), $"{runtime} 不存在。");
        Assert.True(File.Exists(project), $"{project} 不存在。");
        Assert.False(File.Exists(old), $"{old} 不应存在。");

        IReadOnlyList<string> references = ProjectReferences(project);

        Assert.DoesNotContain(references, reference =>
            IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Core")));
        Assert.DoesNotContain(references, reference =>
            IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Flow", "Graph")));
    }

    [Fact(DisplayName = "流程恢复按身份解析")]
    public void Flow_restore_resolves_by_identity()
    {
        string framework = Path.Combine(
            KernelRoot,
            "Control",
            "Shared",
            "IFlowIdentity.cs");
        string contract = Path.Combine(
            KernelRoot,
            "Control",
            "Shared",
            "IFlowProviderSource.cs");
        string source = Path.Combine(
            SourceRoot,
            "Kuroe.Kernel",
            "Tasks",
            "Runtime",
            "FlowProviderSource.cs");
        string restore = Path.Combine(
            SourceRoot,
            "Kuroe.Kernel",
            "Tasks",
            "Runtime",
            "Persistence",
            "TaskRestoreRuntime.cs");

        Assert.Contains("Handles(FlowIdentity identity)", File.ReadAllText(framework), StringComparison.Ordinal);
        Assert.Contains("Find(FlowIdentity identity)", File.ReadAllText(contract), StringComparison.Ordinal);
        Assert.Contains("Find(record.Flow.Identity)", File.ReadAllText(restore), StringComparison.Ordinal);
        Assert.Contains("Handles(identity)", File.ReadAllText(source), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "控制共享层只引用中性契约")]
    public void Control_shared_project_references_only_neutral_contracts()
    {
        string project = Path.Combine(
            KernelRoot,
            "Control",
            "Shared",
            "Kuroe.Kernel.Control.Shared.csproj");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");
        string tasksPresentation = Path.Combine(KernelRoot, "Tasks", "Presentation", "Shared");
        string tasksRecords = Path.Combine(KernelRoot, "Tasks", "Records", "Shared");
        string execution = Path.Combine(KernelRoot, "Execution", "Shared");
        string presentation = Path.Combine(KernelRoot, "Presentation", "Shared");

        ReferencesStayUnder(
            project,
            "Kuroe.Kernel.Control.Shared",
            "中性契约",
            tasks,
            tasksPresentation,
            tasksRecords,
            execution,
            presentation);

        Assert.DoesNotContain(
            ProjectReferences(project),
            reference => IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Flow")));
    }

    [Fact(DisplayName = "任务流程视图暴露待处理步骤")]
    public void Task_flow_view_exposes_pending_steps()
    {
        string file = Path.Combine(KernelRoot, "Control", "Shared", "ITaskFlowView.cs");

        Assert.Contains("Pending", File.ReadAllText(file), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "流程运行期只引用中性契约")]
    public void Flow_runtime_references_only_neutral_contracts()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Kernel",
            "Control",
            "Runtime",
            "Kuroe.Kernel.Control.Runtime.csproj");
        string control = Path.Combine(KernelRoot, "Control", "Shared");
        string execution = Path.Combine(KernelRoot, "Execution", "Shared");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");
        string taskPresentation = Path.Combine(KernelRoot, "Tasks", "Presentation", "Shared");
        string presentation = Path.Combine(KernelRoot, "Presentation", "Shared");

        ReferencesStayUnder(
            project,
            "Kuroe.Kernel.Control.Runtime",
            "中性契约",
            control,
            execution,
            tasks,
            taskPresentation,
            presentation);
    }

    [Fact(DisplayName = "输入端口承载接受、拒绝、待处理与交付")]
    public void Input_ports_own_accept_decline_pending_and_deliver()
    {
        string channel = Path.Combine(KernelRoot, "Input", "Shared", "IInputChannel.cs");
        string delivery = Path.Combine(KernelRoot, "Input", "Shared", "IInputDelivery.cs");

        Assert.False(File.Exists(Path.Combine(KernelRoot, "Control", "Shared", "IFlowAdvance.cs")), $"{Path.Combine(KernelRoot, "Control", "Shared", "IFlowAdvance.cs")} 不应存在。");
        Assert.Contains("Accept", File.ReadAllText(channel), StringComparison.Ordinal);
        Assert.Contains("Decline", File.ReadAllText(channel), StringComparison.Ordinal);
        Assert.Contains("Pending", File.ReadAllText(delivery), StringComparison.Ordinal);
        Assert.Contains("Deliver", File.ReadAllText(delivery), StringComparison.Ordinal);
        Assert.False(
            File.Exists(Path.Combine(KernelRoot, "Control", "Shared", "IFlowInputControl.cs")));
    }

    [Fact(DisplayName = "流程重跑位于独立契约")]
    public void Flow_rerun_lives_in_its_own_contract()
    {
        string file = Path.Combine(KernelRoot, "Control", "Shared", "IFlowRerun.cs");

        Assert.Contains("Rerun", File.ReadAllText(file), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "流程模板目录使用提供方来源")]
    public void Flow_template_catalog_uses_provider_sources()
    {
        string catalog = Path.Combine(SourceRoot, "Kuroe.Kernel", "Control", "Runtime", "FlowTemplateCatalog.cs");
        string catalogText = File.ReadAllText(catalog);

        Assert.Contains("IFlowTemplateSource", catalogText, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "流程控制通过网关运行与恢复")]
    public void Flow_control_uses_run_and_restore_on_the_gateway()
    {
        string gateway = Path.Combine(KernelRoot, "Control", "Shared", "IFlowExecutionGateway.cs");
        string gatewayText = File.ReadAllText(gateway);

        Assert.Contains("RunAsync", gatewayText, StringComparison.Ordinal);
        Assert.Contains("RunRestoreAsync", gatewayText, StringComparison.Ordinal);
        Assert.Contains("PrepareRestore", gatewayText, StringComparison.Ordinal);
    }
}
