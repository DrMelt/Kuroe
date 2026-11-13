using ErrorOr;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.Shared.StaticGraph.Flow;
using Kuroe.Flow.Node.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using Kuroe.Flow.Tasks.Shared;
using Kuroe.Shared;
using Kuroe.Workflows.Flows;
using Kuroe.Workflows.Tasks;
using Xunit;

namespace Kuroe.Tests.Workflows.Flows;

/// <summary>宿主开始输入表的校验：表项与流程开始入口的一致性在任务提交时判定，流程编译不涉及这份配置。</summary>
public sealed class HostStartInputsTests
{
    private static readonly FlowName Name = new("默认");

    private static readonly PortName Target = new("目标");
    private static readonly PortName StartPath = new("启动路径");
    private static readonly PortName Model = new("模型");
    private static readonly PortName FixedPort = new("环境");
    private static readonly PortName DefaultedPort = new("分支");

    /// <summary>表单按端口给出宿主输入名，解析结果原样返回。</summary>
    [Fact]
    public void A_form_maps_its_ports_to_host_inputs()
    {
        HostStartInputs table = Table((StartPath, StartSources.Workspace), (Model, StartSources.Model));

        ErrorOr<IReadOnlyDictionary<PortName, string>> resolved = table.Resolve(Flow());

        Assert.False(resolved.IsError);
        Assert.Equal(StartSources.Workspace, resolved.Value[StartPath]);
        Assert.Equal(StartSources.Model, resolved.Value[Model]);
    }

    /// <summary>没有表项的流程不获得宿主输入。</summary>
    [Fact]
    public void A_flow_without_a_form_gets_no_host_input()
    {
        HostStartInputs table = Table((StartPath, StartSources.Workspace));

        Assert.Empty(table.Bindings(new FlowName("另一条")));
        Assert.Empty(table.Resolve(Flow() with { Name = new FlowName("另一条") }).Value);
    }

    /// <summary>端口名与宿主输入名相同但不在表里时不发生绑定。</summary>
    [Fact]
    public void A_port_named_after_a_host_input_is_not_bound_without_a_form()
    {
        Assert.Empty(Table().Resolve(Flow(new Port(new PortName(StartSources.Workspace), PortTypes.Text))).Value);
    }

    /// <summary>表单里的开始端口必须在开始入口声明。</summary>
    [Fact]
    public void A_form_port_must_be_declared()
    {
        ErrorOr<IReadOnlyDictionary<PortName, string>> resolved =
            Table((new PortName("未声明"), StartSources.Workspace)).Resolve(Flow());

        Assert.True(resolved.IsError);
        Assert.Equal(ErrorCodes.TaskStartInput, resolved.FirstError.Code);
        Assert.Equal("流程 默认 的开始入口没有声明开始输入表里的端口 未声明。", resolved.FirstError.Description);
    }

    /// <summary>带固定值的开始端口不能进表。</summary>
    [Fact]
    public void A_constant_port_cannot_enter_a_form()
    {
        ErrorOr<IReadOnlyDictionary<PortName, string>> resolved =
            Table((FixedPort, StartSources.Workspace)).Resolve(Flow());

        Assert.True(resolved.IsError);
        Assert.Equal("流程 默认 的开始端口 环境 带固定值，不能由宿主给值。", resolved.FirstError.Description);
    }

    /// <summary>带默认值的开始端口可以进表，进表后由宿主给值。</summary>
    [Fact]
    public void A_defaulted_port_may_enter_a_form()
    {
        ErrorOr<IReadOnlyDictionary<PortName, string>> resolved =
            Table((DefaultedPort, StartSources.Workspace)).Resolve(Flow());

        Assert.False(resolved.IsError);
        Assert.Equal(StartSources.Workspace, resolved.Value[DefaultedPort]);
    }

    /// <summary>非 Text 开始端口不承载宿主给出的文本，进表时报错。</summary>
    [Fact]
    public void A_non_text_port_cannot_enter_a_form()
    {
        PortName trigger = new("触发");
        ErrorOr<IReadOnlyDictionary<PortName, string>> resolved =
            Table((trigger, StartSources.Workspace))
                .Resolve(Flow(new Port(trigger, PortTypes.Signal, mode: PortMode.Plain)));

        Assert.True(resolved.IsError);
        Assert.Equal("流程 默认 的开始端口 触发 不是 Text 端口，不能由宿主给值。", resolved.FirstError.Description);
    }

    /// <summary>表单给出的宿主输入名必须受支持。</summary>
    [Fact]
    public void A_form_input_must_be_supported()
    {
        ErrorOr<IReadOnlyDictionary<PortName, string>> resolved =
            Table((StartPath, "NOT_AN_INPUT")).Resolve(Flow());

        Assert.True(resolved.IsError);
        Assert.Equal("流程 默认 的开始端口 启动路径 引用了不受支持的宿主输入 NOT_AN_INPUT。", resolved.FirstError.Description);
    }

    /// <summary>表项的错误一次给全。</summary>
    [Fact]
    public void Every_inconsistent_entry_is_reported()
    {
        ErrorOr<IReadOnlyDictionary<PortName, string>> resolved = Table(
            (new PortName("未声明"), StartSources.Workspace),
            (FixedPort, StartSources.Task),
            (Model, "NOT_AN_INPUT")).Resolve(Flow());

        Assert.True(resolved.IsError);
        Assert.Equal(3, resolved.ErrorsOrEmptyList.Count);
    }

    /// <summary>表里的流程名不是合法流程名时在建立输入表时报错。</summary>
    [Fact]
    public void An_invalid_flow_name_is_rejected_while_creating_the_table()
    {
        ErrorOr<HostStartInputs> created = HostStartInputs.Create(
            new Dictionary<string, IReadOnlyDictionary<string, string>> { ["  "] = new Dictionary<string, string>() },
            StartSources.Supported);

        Assert.True(created.IsError);
        Assert.Equal(ErrorCodes.TaskStartInput, created.FirstError.Code);
    }

    private static HostStartInputs Table(params (PortName Port, string Input)[] entries) =>
        new(new Dictionary<FlowName, IReadOnlyDictionary<PortName, string>>
        {
            [Name] = entries.ToDictionary(entry => entry.Port, entry => entry.Input),
        }, StartSources.Supported);

    /// <summary>一条名为默认的流程：目标与启动路径是普通开始端口，环境带固定值，分支带默认值。</summary>
    private static FlowDefinition Flow(params Port[] extra) =>
        new(Name, null,
            [
                new Port(Target, PortTypes.Text),
                new Port(StartPath, PortTypes.Text, mode: PortMode.Constant),
                new Port(Model, PortTypes.Text, mode: PortMode.Constant),
                new Port(FixedPort, PortTypes.Text, value: "固定", mode: PortMode.Constant),
                new Port(DefaultedPort, PortTypes.Text, mode: PortMode.Constant, defaultValue: "main"),
                .. extra,
            ],
            NodeSpec.Define(new NodeName("整体")));
}