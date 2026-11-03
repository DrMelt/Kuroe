using System.Text.Json;
using Kuroe.Flow.Json.Files;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Xunit;

namespace Kuroe.Flow.Json.Tests;

/// <summary>流程文件的 JSON 绑定：读侧的宽容与缺键默认、枚举与原始形状，写侧的排版与转义。</summary>
public sealed class FlowJsonTests
{
    /// <summary>读侧忽略键名大小写、跳过注释、允许尾随逗号。</summary>
    [Fact]
    public void Read_tolerates_case_comments_and_trailing_commas()
    {
        const string json = """
            {
              // 顶层节点库与流程
              "flows": [
                {
                  "name": "默认",
                  "start": { "outputs": [{ "name": "目标", "type": "Text" }] },
                  "root": null,
                },
              ],
            }
            """;

        FlowFileDto file = Assert.IsType<FlowFileDto>(JsonSerializer.Deserialize(json, FlowJson.Default.FlowFileDto));

        FlowDto flow = Assert.Single(file.Flows);
        Assert.Equal("默认", flow.Name);
        StartDto start = Assert.IsType<StartDto>(flow.Start);
        Assert.NotNull(start.Outputs);
        Assert.Equal(JsonValueKind.Object, Assert.Single(start.Outputs).ValueKind);
    }

    /// <summary>缺键的属性取 null。</summary>
    [Fact]
    public void Missing_keys_fall_back_to_declared_defaults()
    {
        const string json = """{ "Flows": [ { "Name": "默认" } ] }""";

        FlowFileDto file = Assert.IsType<FlowFileDto>(JsonSerializer.Deserialize(json, FlowJson.Default.FlowFileDto));

        FlowDto flow = Assert.Single(file.Flows);
        Assert.Null(flow.Root);
        Assert.Null(flow.Description);
        Assert.Null(flow.Start);
        Assert.Null(flow.HostStartInputs);
    }

    /// <summary>开始端口到宿主输入名的绑定按原样读写。</summary>
    [Fact]
    public void Host_start_inputs_round_trip()
    {
        const string json = """
            {
              "Flows": [
                {
                  "Name": "默认",
                  "HostStartInputs": { "启动路径": "WORKSPACE_PATH" }
                }
              ]
            }
            """;

        FlowFileDto file = Assert.IsType<FlowFileDto>(JsonSerializer.Deserialize(json, FlowJson.Default.FlowFileDto));
        FlowDto flow = Assert.Single(file.Flows);

        Assert.NotNull(flow.HostStartInputs);
        Assert.Equal("WORKSPACE_PATH", Assert.Single(flow.HostStartInputs).Value);

        string written = FlowFileJson.Serialize(file);

        Assert.Contains("\"HostStartInputs\"", written);
        Assert.Contains("\"启动路径\": \"WORKSPACE_PATH\"", written);
    }

    /// <summary>节点类型名按文件里写的原样保留，大小写不敏感的比较在装配层。</summary>
    [Fact]
    public void Node_kind_is_kept_as_written()
    {
        const string json = """{ "Nodes": [ { "Name": "整体", "Kind": "container" } ] }""";

        NodeFileDto file = Assert.IsType<NodeFileDto>(JsonSerializer.Deserialize(json, FlowJson.Default.NodeFileDto));

        NodeDto node = Assert.Single(file.Nodes);
        Assert.Equal("container", node.Kind);
        Assert.Null(node.Use);
    }

    /// <summary>节点输入绑定按字符串映射读入，端口条目保留原始对象形状。</summary>
    [Fact]
    public void Input_bindings_are_read_as_a_string_map()
    {
        const string json = """
            {
              "Nodes": [
                { "Name": "整体", "Kind": "Container", "Bindings": { "MODEL_NAME": "Start@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Ref": "分析@结论" }] },
                { "Label": "组实例", "Use": "整体", "Bindings": { "MODEL_NAME": "@MODEL_NAME" } }
              ]
            }
            """;

        NodeFileDto file = Assert.IsType<NodeFileDto>(JsonSerializer.Deserialize(json, FlowJson.Default.NodeFileDto));

        Dictionary<string, JsonElement>? startBindings = file.Nodes[0].Bindings;
        Assert.NotNull(startBindings);
        Assert.Equal("Start@MODEL_NAME", startBindings["MODEL_NAME"].GetString());
        Dictionary<string, JsonElement>? groupBindings = file.Nodes[1].Bindings;
        Assert.NotNull(groupBindings);
        Assert.Equal("@MODEL_NAME", groupBindings["MODEL_NAME"].GetString());
        List<JsonElement>? outputs = file.Nodes[0].Outputs;
        Assert.NotNull(outputs);
        Assert.Equal(JsonValueKind.Object, Assert.Single(outputs).ValueKind);
    }

    /// <summary>端口条目的端口类型写成枚举名字。</summary>
    [Fact]
    public void Port_entry_writes_its_type_by_name()
    {
        PortEntryDto entry = new() { Name = "目标", Type = PortTypes.Text.Name };

        string text = JsonSerializer.Serialize(entry, FlowJson.WriteOptions.GetTypeInfo(typeof(PortEntryDto)));

        Assert.Contains("\"Type\": \"Text\"", text);
    }

    /// <summary>端口条目未写的可选字段不写出。</summary>
    [Fact]
    public void Port_entry_omits_unwritten_optional_fields()
    {
        PortEntryDto entry = new() { Name = "结论", Ref = "分析@结论" };

        string text = JsonSerializer.Serialize(entry, FlowJson.WriteOptions.GetTypeInfo(typeof(PortEntryDto)));

        Assert.Contains("\"Ref\": \"分析@结论\"", text);
        Assert.DoesNotContain("\"Type\"", text);
        Assert.DoesNotContain("\"Description\"", text);
        Assert.DoesNotContain("\"Value\"", text);
        Assert.DoesNotContain("\"Default\"", text);
    }

    /// <summary>写侧缩进输出，非 ASCII 字符不转义，节点类型写成枚举名字。</summary>
    [Fact]
    public void Write_keeps_indentation_and_readable_text()
    {
        FlowFileDto file = new()
        {
            Flows = [new FlowDto { Name = "默认" }],
        };

        string text = JsonSerializer.Serialize(file, FlowJson.WriteOptions.GetTypeInfo(typeof(FlowFileDto)));

        Assert.Contains("\"Flows\"", text);
        Assert.Contains("默认", text);
        Assert.Contains("\n", text);
        Assert.Contains("  ", text);
    }
}
