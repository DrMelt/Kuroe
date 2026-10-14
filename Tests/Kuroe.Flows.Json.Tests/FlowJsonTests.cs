using System.Text.Json;
using Kuroe.Flows.Json.Model;
using Kuroe.Flows.Shared.StaticGraph;
using Xunit;

namespace Kuroe.Flows.Json.Tests;

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
                  "models": [{ "name": "执行者", "model": "fake" }],
                  "start": { "outputs": [{ "name": "目标", "type": "Text" }] },
                  "nodes": [],
                },
              ],
            }
            """;

        FlowFileDto file = JsonSerializer.Deserialize(json, FlowJson.Default.FlowFileDto)!;

        Assert.Empty(file.Nodes);
        FlowDto flow = Assert.Single(file.Flows);
        Assert.Equal("默认", flow.Name);
        Assert.Equal("fake", Assert.Single(flow.Models).Model);
        Assert.Equal(JsonValueKind.Object, Assert.Single(flow.Start!.Outputs!).ValueKind);
    }

    /// <summary>缺键的列表属性取声明的空集合，未写的可选标量取 null。</summary>
    [Fact]
    public void Missing_keys_fall_back_to_declared_defaults()
    {
        const string json = """{ "Flows": [ { "Name": "默认" } ] }""";

        FlowFileDto file = JsonSerializer.Deserialize(json, FlowJson.Default.FlowFileDto)!;

        FlowDto flow = Assert.Single(file.Flows);
        Assert.Empty(file.Nodes);
        Assert.Empty(flow.Models);
        Assert.Empty(flow.Nodes);
        Assert.Null(flow.Description);
        Assert.Null(flow.Start);
    }

    /// <summary>节点类型与门控按枚举名字绑定，名字不区分大小写。</summary>
    [Fact]
    public void Enums_bind_by_name_ignoring_case()
    {
        const string json = """{ "Nodes": [ { "Name": "整体", "Kind": "container", "Gate": "review" } ] }""";

        FlowFileDto file = JsonSerializer.Deserialize(json, FlowJson.Default.FlowFileDto)!;

        NodeDto node = Assert.Single(file.Nodes);
        Assert.Equal(NodeKind.Container, node.Kind);
        Assert.Equal(NodeGate.Review, node.Gate);
        Assert.Null(node.Use);
    }

    /// <summary>枚举位置收到不认识的名字时拒绝。</summary>
    [Fact]
    public void Unknown_enum_name_is_rejected()
    {
        const string json = """{ "Nodes": [ { "Name": "整体", "Kind": "Bogus" } ] }""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(json, FlowJson.Default.FlowFileDto));
    }

    /// <summary>JsonElement 成员保留原始形状：模型槽位声明是数组、槽位绑定是对象，端口条目是对象。</summary>
    [Fact]
    public void Raw_json_members_keep_their_shape()
    {
        const string json = """
            {
              "Nodes": [
                { "Name": "整体", "Kind": "Container", "Models": ["执行者"], "Outputs": [{ "Name": "结论", "Ref": "分析@结论" }] },
                { "Name": "组实例", "Use": "整体", "Models": { "执行者": "执行者" } }
              ]
            }
            """;

        FlowFileDto file = JsonSerializer.Deserialize(json, FlowJson.Default.FlowFileDto)!;

        Assert.Equal(JsonValueKind.Array, file.Nodes[0].Models!.Value.ValueKind);
        Assert.Equal(JsonValueKind.Object, file.Nodes[1].Models!.Value.ValueKind);
        Assert.Equal(JsonValueKind.Object, Assert.Single(file.Nodes[0].Outputs!).ValueKind);
    }

    /// <summary>端口条目的端口类型写成枚举名字。</summary>
    [Fact]
    public void Port_entry_writes_its_type_by_name()
    {
        PortEntryDto entry = new() { Name = "目标", Type = PortType.Text };

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
    }

    /// <summary>写侧缩进输出，非 ASCII 字符不转义，节点类型写成枚举名字。</summary>
    [Fact]
    public void Write_keeps_indentation_and_readable_text()
    {
        FlowFileDto file = new()
        {
            Nodes = [new NodeDto { Name = "制定计划", Kind = NodeKind.Executable }],
            Flows = [new FlowDto { Name = "默认" }],
        };

        string text = JsonSerializer.Serialize(file, FlowJson.WriteOptions.GetTypeInfo(typeof(FlowFileDto)));

        Assert.Contains("\"Kind\": \"Executable\"", text);
        Assert.Contains("制定计划", text);
        Assert.Contains("\n", text);
        Assert.Contains("  ", text);
    }
}
