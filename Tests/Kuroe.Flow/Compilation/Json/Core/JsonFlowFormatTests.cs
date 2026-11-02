using ErrorOr;
using Kuroe.Flow.Compilation.Shared;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Xunit;

namespace Kuroe.Flow.Json.Tests;

/// <summary>流程文件的 JSON 格式：文本到流程描述的映射与写回保真。</summary>
public sealed class JsonFlowFormatTests
{
    private static readonly JsonFlowFormat Format = new();

    [Fact]
    public void Read_maps_flow_root_and_reference()
    {
        IReadOnlyList<FlowDocumentFlow> flows = Ok(Format.Read(Flow));

        NodeDefinitionDocument root = Assert.IsType<NodeDefinitionDocument>(Assert.Single(flows).Root);
        NodeReferenceDocument reference =
            Assert.IsType<NodeReferenceDocument>(Assert.Single(root.Nodes!, node => node is NodeReferenceDocument));
        Assert.Equal("副本", reference.Label);
        Assert.Equal("计划", reference.Use);
        Assert.Equal("Start@MODEL_NAME", reference.Bindings!["MODEL_NAME"]);
    }

    [Fact]
    public void Write_keeps_parameters_and_static_model()
    {
        IReadOnlyList<FlowDocumentFlow> flows = Ok(Format.Read(Flow));
        string text = Ok(Format.Write(flows));

        Assert.Contains("\"Prompt\": \"写出计划\"", text);
        Assert.Contains("\"Mode\": \"Constant\"", text);
        Assert.Contains("\"MODEL_NAME\": \"Start@MODEL_NAME\"", text);
        Assert.Contains("\"Label\": \"副本\"", text);
        Assert.DoesNotContain("\": null", text);

        Assert.Equal(text, Ok(Format.Write(Ok(Format.Read(text)))));
    }

    [Fact]
    public void Trigger_port_types_round_trip()
    {
        const string flow = """
            {
              "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
                { "Name": "整体", "Kind": "Container", "Nodes": [
                  { "Name": "来源", "Kind": "Llm", "Outputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }, { "Name": "信号", "Type": "Signal", "Mode": "Trigger" }] },
                  { "Name": "下游", "Kind": "Llm", "Inputs": [{ "Name": "结论", "Type": "Text", "Mode": "Trigger" }, { "Name": "信号", "Type": "Signal", "Mode": "Trigger" }] }
                ] }
              ] } ]
            }
            """;

        IReadOnlyList<FlowDocumentFlow> flows = Ok(Format.Read(flow));
        string text = Ok(Format.Write(flows));

        Assert.Contains("\"Mode\": \"Trigger\"", text);
        Assert.Contains("\"Type\": \"Text\"", text);
        Assert.Contains("\"Type\": \"Signal\"", text);
        Assert.Equal(text, Ok(Format.Write(Ok(Format.Read(text)))));
    }

    [Fact]
    public void Input_role_and_explicit_trigger_round_trip()
    {
        const string flow = """
            {
              "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
                { "Name": "整体", "Kind": "Container", "Nodes": [
                  { "Name": "来源", "Kind": "Llm",
                    "Inputs": [
                      { "Name": "配置", "Type": "Text", "Mode": "Constant", "Role": "Configuration" },
                      { "Name": "信号", "Type": "Signal", "Mode": "Plain" }
                    ],
                    "Bindings": { "MODEL_NAME": "Start@MODEL_NAME" },
                    "Outputs": [
                      { "Name": "结论", "Type": "Text", "Mode": "Trigger" },
                      { "Name": "完成", "Type": "Signal" }
                    ] }
                ] }
              ] } ]
            }
            """;

        IReadOnlyList<FlowDocumentFlow> flows = Ok(Format.Read(flow));
        NodeDefinitionDocument root = Assert.IsType<NodeDefinitionDocument>(Assert.Single(flows).Root);
        NodeDefinitionDocument source = Assert.IsType<NodeDefinitionDocument>(Assert.Single(root.Nodes!));
        InputPortDocument configured = Assert.Single(source.Inputs, port => port.Name == "配置");
        InputPortDocument signal = Assert.Single(source.Inputs, port => port.Name == "信号");
        Assert.Equal(InputRole.Configuration, configured.Role);
        Assert.Equal(PortMode.Constant, configured.Mode);
        Assert.Equal(PortMode.Plain, signal.Mode);
        Assert.Equal(PortMode.Trigger, source.Outputs[0].Mode);
        Assert.Null(source.Outputs[1].Mode);

        string text = Ok(Format.Write(flows));
        Assert.Contains("\"Role\": \"Configuration\"", text);
        Assert.Contains("\"Mode\": \"Constant\"", text);
        Assert.Contains("\"Mode\": \"Plain\"", text);
        Assert.Contains("\"Mode\": \"Trigger\"", text);
        Assert.Equal(text, Ok(Format.Write(Ok(Format.Read(text)))));
    }

    [Fact]
    public void Output_entries_share_value_and_ref_shape_on_all_nodes()
    {
        const string flow = """
            {
              "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
                { "Name": "整体", "Kind": "Container", "Outputs": [
                  { "Name": "配置", "Type": "Text", "Mode": "Constant", "Value": "固定" }
                ], "Nodes": [
                  { "Name": "配置节点", "Kind": "Llm", "Bindings": { "MODEL_NAME": "Start@MODEL_NAME" },
                    "Outputs": [{ "Name": "资料", "Type": "Text", "Mode": "Constant", "Ref": "@MODEL_NAME" }] }
                ] }
              ] } ]
            }
            """;

        IReadOnlyList<FlowDocumentFlow> flows = Ok(Format.Read(flow));
        NodeDefinitionDocument root = Assert.IsType<NodeDefinitionDocument>(Assert.Single(flows).Root);
        PortDocument output = Assert.Single(root.Outputs);
        Assert.Equal("固定", output.Value);
        Assert.Equal(PortTypes.Text, output.Type);
        Assert.Equal(PortMode.Constant, output.Mode);

        string text = Ok(Format.Write(flows));
        Assert.Contains("\"Value\": \"固定\"", text);
        Assert.Contains("\"Ref\": \"@MODEL_NAME\"", text);
        Assert.Equal(text, Ok(Format.Write(Ok(Format.Read(text)))));
    }

    [Fact]
    public void Read_rejects_the_nodes_field()
    {
        ErrorOr<IReadOnlyList<FlowDocumentFlow>> document =
            Format.Read("""{ "Nodes": [ { "Name": "计划", "Kind": "Llm" } ] }""");

        Assert.True(document.IsError);
        Assert.Contains(document.ErrorsOrEmptyList,
            error => error.Description.Contains("节点库写在 nodes.json", StringComparison.Ordinal));
    }

    [Fact]
    public void Read_rejects_the_old_constant_field()
    {
        ErrorOr<IReadOnlyList<FlowDocumentFlow>> document = Format.Read("""
            {
              "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
                { "Name": "整体", "Kind": "Container", "Outputs": [{ "Name": "配置", "Type": "Text", "Constant": true }] }
              ] } ]
            }
            """);

        Assert.True(document.IsError);
        Assert.Contains(document.ErrorsOrEmptyList,
            error => error.Description.Contains("输出端口不认识键 Constant"));
    }

    [Fact]
    public void Read_rejects_a_reference_with_name_and_without_label()
    {
        ErrorOr<IReadOnlyList<FlowDocumentFlow>> document = Format.Read("""
            { "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
              { "Name": "整体", "Kind": "Container", "Nodes": [ { "Name": "副本", "Use": "计划" } ] }
            ] } ] }
            """);

        Assert.True(document.IsError);
        Assert.Contains(document.ErrorsOrEmptyList,
            error => error.Description.Contains("被使用的节点写 Label 区分，不写 Name。"));
    }

    private static T Ok<T>(ErrorOr<T> result)
    {
        Assert.False(result.IsError, string.Join("；", result.ErrorsOrEmptyList.Select(error => error.Description)));

        return result.Value;
    }

    private const string Flow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [ { "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" } ] },
              "Nodes": [
                { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }], "Bindings": { "目标": "Start@目标" }, "Nodes": [
                  { "Name": "配置", "Kind": "Llm", "Prompt": "写出计划", "Bindings": { "MODEL_NAME": "Start@MODEL_NAME" } },
                  { "Label": "副本", "Use": "计划", "Bindings": { "MODEL_NAME": "Start@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] }
                ] }
              ]
            }
          ]
        }
        """;
}