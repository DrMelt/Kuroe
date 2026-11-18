using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryAssertions;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>控制模块边界：控制契约只描述流程视图与命令，流程运行时留在 Kernel 自己的项目内。</summary>
public sealed class KernelControlTests
{
    [Fact]
    public void Flow_runtime_stays_free_of_graph_control()
    {
        string file = Path.Combine(
            SourceRoot,
            "Kuroe.Kernel",
            "Control",
            "Runtime",
            "FlowRuntime.cs");
        string text = File.ReadAllText(file);

        foreach (string word in (string[])["GraphFlowControl", "TaskRuntime", "GraphExecution", "NodeGraph"])
        {
            Assert.DoesNotContain(word, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Flow_runtime_lives_in_its_own_project()
    {
        string runtime = Path.Combine(SourceRoot, "Kuroe.Kernel", "Control", "Runtime", "FlowRuntime.cs");
        string project = Path.Combine(SourceRoot, "Kuroe.Kernel", "Control", "Runtime", "Kuroe.Control.Runtime.csproj");
        string old = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Control",
            "Graph",
            "Runtime",
            "Engine",
            "FlowRuntime.cs");

        Assert.True(File.Exists(runtime));
        Assert.True(File.Exists(project));
        Assert.False(File.Exists(old));

        IReadOnlyList<string> references = ProjectReferences(project);

        Assert.DoesNotContain(references, reference =>
            IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Graph")));
        Assert.DoesNotContain(references, reference =>
            IsUnder(reference, Path.Combine(SourceRoot, "Kuroe.Flow", "Node")));
    }

    [Fact]
    public void Control_shared_stays_free_of_graph_runtime()
    {
        string control = Path.Combine(KernelRoot, "Control", "Shared");
        string[] forbidden =
        [
            "TaskRuntime",
            "GraphExecution",
            "NodeGraph",
            "ExecutableNode",
            "GraphFlow",
            "ModelPort",
        ];

        SourcesExclude(control, "图运行期语义", forbidden);
    }

    [Fact]
    public void Execution_index_stays_free_of_flow_payload()
    {
        string file = Path.Combine(
            SourceRoot,
            "Kuroe.Kernel",
            "Control",
            "Runtime",
            "ExecutionIndex.cs");
        string text = File.ReadAllText(file);

        Assert.DoesNotContain("Payload", text, StringComparison.Ordinal);
        Assert.DoesNotContain("object?", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Execution_gateway_stays_neutral()
    {
        string file = Path.Combine(
            KernelRoot,
            "Control",
            "Shared",
            "IFlowExecutionGateway.cs");

        Assert.DoesNotContain("Attach", File.ReadAllText(file), StringComparison.Ordinal);
    }

    [Fact]
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
        Assert.DoesNotContain("FlowRecord", File.ReadAllText(framework), StringComparison.Ordinal);
        Assert.Contains("Find(FlowIdentity identity)", File.ReadAllText(contract), StringComparison.Ordinal);
        Assert.Contains("Find(record.Flow.Identity)", File.ReadAllText(restore), StringComparison.Ordinal);
        Assert.Contains("Handles(identity)", File.ReadAllText(source), StringComparison.Ordinal);
    }

    [Fact]
    public void Control_shared_project_references_only_neutral_contracts()
    {
        string project = Path.Combine(
            KernelRoot,
            "Control",
            "Shared",
            "Kuroe.Control.Shared.csproj");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");
        string tasksPresentation = Path.Combine(KernelRoot, "Tasks", "Presentation", "Shared");
        string tasksRecords = Path.Combine(KernelRoot, "Tasks", "Records", "Shared");
        string execution = Path.Combine(KernelRoot, "Execution", "Shared");
        string presentation = Path.Combine(KernelRoot, "Presentation", "Shared");

        ReferencesStayUnder(
            project,
            "Kuroe.Control.Shared",
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

    [Fact]
    public void Flow_control_port_excludes_presentation_and_stop()
    {
        string control = Path.Combine(KernelRoot, "Control", "Shared", "IFlowControl.cs");
        string factory = Path.Combine(KernelRoot, "Control", "Shared", "IFlowControlFactory.cs");
        string controlText = File.ReadAllText(control);

        Assert.DoesNotContain("CaptureView", controlText, StringComparison.Ordinal);
        Assert.DoesNotContain("StopExecution", controlText, StringComparison.Ordinal);
        Assert.DoesNotContain("Attach", File.ReadAllText(factory), StringComparison.Ordinal);
    }

    [Fact]
    public void Flow_ports_expose_views_pending_steps_and_flow_records()
    {
        string presentation = Path.Combine(
            KernelRoot, "Control", "Shared", "IFlowPresentation.cs");
        string records = Path.Combine(
            KernelRoot, "Control", "Shared", "IFlowRecordSource.cs");
        string graphRecords = Path.Combine(
            SourceRoot, "Kuroe.Flow", "Control", "Graph", "Runtime", "Engine", "GraphFlowRecordSource.cs");
        string dialogueRecords = Path.Combine(
            SourceRoot, "Kuroe", "Assistant", "Core", "Dialogue", "Flow", "DialogueFlowRecordSource.cs");

        Assert.DoesNotContain("CreateSnapshot", File.ReadAllText(presentation), StringComparison.Ordinal);
        Assert.Contains(
            "Pending",
            File.ReadAllText(Path.Combine(KernelRoot, "Control", "Shared", "ITaskFlowView.cs")),
            StringComparison.Ordinal);
        Assert.DoesNotContain("CaptureExecutions", File.ReadAllText(records), StringComparison.Ordinal);
        Assert.DoesNotContain("CaptureExecutions", File.ReadAllText(graphRecords), StringComparison.Ordinal);
        Assert.DoesNotContain("CaptureExecutions", File.ReadAllText(dialogueRecords), StringComparison.Ordinal);
    }

    [Fact]
    public void Flow_runtime_references_only_neutral_contracts()
    {
        string project = Path.Combine(
            SourceRoot,
            "Kuroe.Kernel",
            "Control",
            "Runtime",
            "Kuroe.Control.Runtime.csproj");
        string control = Path.Combine(KernelRoot, "Control", "Shared");
        string execution = Path.Combine(KernelRoot, "Execution", "Shared");
        string tasks = Path.Combine(KernelRoot, "Tasks", "Shared");
        string taskPresentation = Path.Combine(KernelRoot, "Tasks", "Presentation", "Shared");
        string presentation = Path.Combine(KernelRoot, "Presentation", "Shared");

        ReferencesStayUnder(
            project,
            "Kuroe.Control.Runtime",
            "中性契约",
            control,
            execution,
            tasks,
            taskPresentation,
            presentation);
    }

    [Fact]
    public void Flow_control_does_not_name_concrete_execution()
    {
        string[] directories =
        [
            Path.Combine(KernelRoot, "Control", "Shared"),
            Path.Combine(SourceRoot, "Kuroe.Flow", "Control", "Graph"),
            Path.Combine(SourceRoot, "Kuroe", "Assistant", "Core"),
        ];
        string[] forbidden =
        [
            "Kuroe.Execution.ExecutionInstance;",
            "Kuroe.Execution.ExecutionInstance ",
            "Kuroe.Execution.ExecutionInstance)",
            "Kuroe.Execution.ExecutionInstance,",
        ];

        foreach (string directory in directories)
        {
            foreach (string file in SourceFiles(directory))
            {
                string text = File.ReadAllText(file);
                foreach (string name in forbidden)
                {
                    Assert.False(
                        text.Contains(name, StringComparison.Ordinal),
                        $"{Path.GetFileName(file)} 引用了执行状态机。");
                }
            }
        }
    }

    [Fact]
    public void Flow_control_port_excludes_input_delivery()
    {
        string control = Path.Combine(KernelRoot, "Control", "Shared", "IFlowControl.cs");
        string runtime = Path.Combine(KernelRoot, "Control", "Shared", "IFlowRuntime.cs");
        string channel = Path.Combine(KernelRoot, "Input", "Shared", "IInputChannel.cs");
        string delivery = Path.Combine(KernelRoot, "Input", "Shared", "IInputDelivery.cs");
        string controlText = File.ReadAllText(control);

        Assert.DoesNotContain("Pending", controlText, StringComparison.Ordinal);
        Assert.DoesNotContain("Deliver", controlText, StringComparison.Ordinal);
        Assert.DoesNotContain("Accept", controlText, StringComparison.Ordinal);
        Assert.DoesNotContain("Advance", File.ReadAllText(runtime), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(KernelRoot, "Control", "Shared", "IFlowAdvance.cs")));
        Assert.Contains("Accept", File.ReadAllText(channel), StringComparison.Ordinal);
        Assert.Contains("Decline", File.ReadAllText(channel), StringComparison.Ordinal);
        Assert.Contains("Pending", File.ReadAllText(delivery), StringComparison.Ordinal);
        Assert.Contains("Deliver", File.ReadAllText(delivery), StringComparison.Ordinal);
        Assert.False(
            File.Exists(Path.Combine(KernelRoot, "Control", "Shared", "IFlowInputControl.cs")));
    }

    [Fact]
    public void Flow_control_port_excludes_process_specific_commands()
    {
        string control = Path.Combine(KernelRoot, "Control", "Shared", "IFlowControl.cs");
        string rerun = Path.Combine(KernelRoot, "Control", "Shared", "IFlowRerun.cs");
        string controlText = File.ReadAllText(control);

        Assert.DoesNotContain("Advance", controlText, StringComparison.Ordinal);
        Assert.DoesNotContain("Rerun", controlText, StringComparison.Ordinal);
        Assert.Contains("Rerun", File.ReadAllText(rerun), StringComparison.Ordinal);
    }

    [Fact]
    public void Flow_template_catalog_uses_provider_sources()
    {
        string catalog = Path.Combine(SourceRoot, "Kuroe.Kernel", "Control", "Runtime", "FlowTemplateCatalog.cs");
        string catalogText = File.ReadAllText(catalog);

        Assert.Contains("IFlowTemplateSource", catalogText, StringComparison.Ordinal);
        Assert.DoesNotContain("IFlowProviderSource", catalogText, StringComparison.Ordinal);
        Assert.DoesNotContain("FlowProvider", catalogText, StringComparison.Ordinal);
    }

    [Fact]
    public void Flow_control_uses_one_execution_gateway()
    {
        string gateway = Path.Combine(KernelRoot, "Control", "Shared", "IFlowExecutionGateway.cs");
        string graphFactory = Path.Combine(
            SourceRoot,
            "Kuroe.Flow",
            "Control",
            "Graph",
            "Runtime",
            "Engine",
            "GraphFlowControlFactory.cs");
        string dialogueFactory = Path.Combine(
            SourceRoot,
            "Kuroe",
            "Assistant",
            "Core",
            "Dialogue",
            "Flow",
            "DialogueFlowControlFactory.cs");
        string gatewayText = File.ReadAllText(gateway);

        Assert.Contains("RunAsync", gatewayText, StringComparison.Ordinal);
        Assert.Contains("RunRestoreAsync", gatewayText, StringComparison.Ordinal);
        Assert.Contains("PrepareRestore", gatewayText, StringComparison.Ordinal);
        Assert.DoesNotContain("Func<", gatewayText, StringComparison.Ordinal);
        Assert.DoesNotContain("IExecutionRuntime", File.ReadAllText(graphFactory), StringComparison.Ordinal);
        Assert.DoesNotContain("IExecutionRuntime", File.ReadAllText(dialogueFactory), StringComparison.Ordinal);
    }
}
