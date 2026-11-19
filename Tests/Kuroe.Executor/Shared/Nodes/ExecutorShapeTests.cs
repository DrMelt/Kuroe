using Kuroe.Executor.Shared.Nodes;
using Kuroe.Executor.Shared.Ports;
using Kuroe.Tasks.Shared;
using Xunit;

namespace Kuroe.Executor.Shared.Tests.Nodes;

/// <summary>执行器形状与端口声明：默认类型、默认模式、默认角色与输出声明方式。</summary>
public sealed class ExecutorShapeTests
{
    [Fact(DisplayName = "输入端口未写类型与模式时取默认值")]
    public void Input_port_takes_default_type_mode_and_role()
    {
        ExecutorInputPort port = new(new PortName("目标"));

        Assert.Equal(PortTypes.Text, port.Type);
        Assert.Equal(PortMode.Plain, port.Mode);
        Assert.Equal(InputRole.Content, port.Role);
        Assert.False(port.IsStatic);
        Assert.False(port.Trigger);
    }

    [Fact(DisplayName = "常量输入端口是静态配置角色")]
    public void Constant_input_port_is_static_configuration()
    {
        ExecutorInputPort port = new(new PortName("配置"), PortTypes.Context, mode: PortMode.Constant);

        Assert.Equal(PortMode.Constant, port.Mode);
        Assert.Equal(InputRole.Configuration, port.Role);
        Assert.True(port.IsStatic);
        Assert.False(port.Trigger);
    }

    [Fact(DisplayName = "信号输入端口默认作为控制角色")]
    public void Signal_input_port_defaults_to_control_role()
    {
        ExecutorInputPort port = new(new PortName("触发"), PortTypes.Signal);

        Assert.Equal(InputRole.Control, port.Role);
        Assert.False(port.IsStatic);
    }

    [Fact(DisplayName = "输出提示端口时允许输出类型取端口类型")]
    public void Given_outputs_decide_allowed_output_types()
    {
        ExecutorShape shape = new()
        {
            OutputTypes = [PortTypes.Text],
            Outputs = new ExecutorOutputDeclaration.Given(
            [
                new ExecutorOutputPort(new PortName("结论"), PortTypes.Signal),
                new ExecutorOutputPort(new PortName("说明"), PortTypes.Signal),
                new ExecutorOutputPort(new PortName("正文"), PortTypes.Text),
            ]),
        };

        Assert.Equal([PortTypes.Signal, PortTypes.Text], shape.AllowedOutputTypes);
    }

    [Fact(DisplayName = "种类名按原文比较")]
    public void Executor_kind_compares_by_text()
    {
        Assert.Equal(new ExecutorKind("Llm"), new ExecutorKind("Llm"));
        Assert.NotEqual(new ExecutorKind("Llm"), new ExecutorKind("llm"));
        Assert.Equal("Llm", new ExecutorKind("Llm").ToString());
    }
}