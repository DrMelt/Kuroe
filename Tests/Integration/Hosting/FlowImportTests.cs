using System.Text.Json;
using ErrorOr;
using Kuroe.Configuration.Files;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Kuroe.Workflows.Flows;
using Xunit;

namespace Kuroe.Integration.Tests.Hosting;

/// <summary>流程文件的加载与导入：真实工作目录、同名覆盖与相对路径解析。</summary>
public sealed class FlowImportTests
{
    [Fact]
    public void Flow_loads_and_imports_a_custom_definition()
    {
        using TestEnvironment environment = TestEnvironment.Create(SingleFunnelFlow);
        Assert.Contains(environment.Flows.All(), flow => flow.Name == new FlowName("默认"));

        string source = Path.Combine(Path.GetTempPath(), $"kuroe-flow-{Guid.NewGuid():N}.json");
        File.WriteAllText(source, TwoCustomFlows);
        try
        {
            FlowImport imported = environment.Flows.Import(source).ThrowIfError();

            Assert.Single(imported.Names);
            Assert.Contains(environment.Flows.All(), flow => flow.Name == new FlowName("两级"));
            Assert.Equal(2, environment.Flows.Find(new FlowName("两级")).ThrowIfError().RootNode.Nodes!.Count);
        }
        finally
        {
            File.Delete(source);
        }
    }

    /// <summary>输入节点挂点的环：回复回边两侧都声明 Signal，载入后类型保持，写回文件仍写 Signal。</summary>
    [Fact]
    public void Loop_with_input_node_loads()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        File.WriteAllText(Path.Combine(environment.Root, "loop.json"), LoopWithInputNode);

        environment.Flows.Import("loop.json").ThrowIfError();

        IReadOnlyList<NodeSpec> members = environment.Flows.Find(new FlowName("默认")).ThrowIfError().RootNode.Nodes!;

        Assert.Equal(PortTypes.Signal, members[0].Inputs.Find(new PortName("回复"))?.Type);
        Assert.Equal(PortTypes.Signal, members[1].Outputs.Find(new PortName("回复"))?.Type);

        string text = File.ReadAllText(KuroePaths.At(environment.Root).FlowsFile);
        Assert.Contains("\"Type\": \"Signal\"", text);
    }

    /// <summary>流程名两侧空白在加载时去掉，按去空白后的名字可查到。</summary>
    [Fact]
    public void Flow_name_with_surrounding_spaces_is_trimmed_on_load()
    {
        using TestEnvironment environment = TestEnvironment.Create(SurroundingSpacesFlowName);

        FlowDefinition flow = Assert.Single(environment.Flows.All());
        Assert.Equal(new FlowName("默认"), flow.Name);
        Assert.True(environment.Flows.Find(new FlowName("默认")).IsSuccess);
    }

    [Fact]
    public void Import_resolves_a_relative_path_against_the_work_directory()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        File.WriteAllText(Path.Combine(environment.Root, "extra.json"), SingleFunnelFlow);

        FlowImport imported = environment.Flows.Import("extra.json").ThrowIfError();

        Assert.Single(imported.Names);
        Assert.True(Path.IsPathRooted(imported.Source), $"导入来源应是绝对路径，收到 {imported.Source}。");
    }

    [Fact]
    public void Import_reports_new_flow()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        File.WriteAllText(Path.Combine(environment.Root, "extra.json"), ExtraFlow);

        FlowImport imported = environment.Flows.Import("extra.json").ThrowIfError();

        Assert.Equal(["新增流程 额外"], imported.Notes);
    }

    [Fact]
    public void Import_reports_overwritten_flow()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        File.WriteAllText(Path.Combine(environment.Root, "extra.json"), OverwritingFlow);

        FlowImport imported = environment.Flows.Import("extra.json").ThrowIfError();

        Assert.Equal(["覆盖流程 默认"], imported.Notes);
    }

    [Fact]
    public void Import_rejects_duplicated_flow_names()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        File.WriteAllText(Path.Combine(environment.Root, "duplicated.json"), DuplicateImportedFlows);

        ErrorOr<FlowImport> imported = environment.Flows.Import("duplicated.json");

        Assert.True(imported.IsError);
        Assert.Contains(imported.ErrorsOrEmptyList, error => error.Description.Contains("流程名重复"));
    }

    [Fact]
    public void Import_keeps_library_and_reference_shape_on_save()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        File.WriteAllText(Path.Combine(environment.Root, "library.json"), LibraryFlow);

        FlowImport imported = environment.Flows.Import("library.json").ThrowIfError();
        Assert.Contains(imported.Notes, note => note.Contains("节点库"));

        string text = File.ReadAllText(KuroePaths.At(environment.Root).FlowsFile);
        Assert.Contains("\"Nodes\"", text);
        Assert.Contains("\"Use\"", text);
        Assert.Contains("\"In\"", text);
        Assert.Contains("\"Inputs\"", text);
        Assert.Contains("\"Static\"", text);
        Assert.DoesNotContain("\"Models\"", text);
        Assert.DoesNotContain("\"ModelSlots\"", text);

        using JsonDocument saved = JsonDocument.Parse(text);
        JsonElement libraryContainer = saved.RootElement.GetProperty("Nodes")
            .EnumerateArray()
            .Single(node => node.GetProperty("Name").GetString() == "交付");
        JsonElement modelInput = libraryContainer.GetProperty("Inputs")
            .EnumerateArray()
            .Single(input => input.GetProperty("Name").GetString() == "MODEL_NAME");
        Assert.Equal("Constant", modelInput.GetProperty("Mode").GetString());

        JsonElement reference = saved.RootElement.GetProperty("Flows")[0].GetProperty("Nodes")[0]
            .GetProperty("Nodes")[1];
        Assert.Equal("@MODEL_NAME", reference.GetProperty("In").GetProperty("MODEL_NAME").GetString());
    }

    [Fact]
    public void Import_rejects_flow_referencing_library_node_outside_the_file()
    {
        using TestEnvironment environment = TestEnvironment.Create(LibraryFlow);
        File.WriteAllText(Path.Combine(environment.Root, "reuse.json"), LibraryReferenceOnlyFlow);

        ErrorOr<FlowImport> imported = environment.Flows.Import("reuse.json");

        Assert.Equal(
            [
                "流程 复用 的节点 干活：引用的节点 外部库 不在节点库。",
                "导入的文件必须能独立装配：流程引用的节点要用内置节点或写在同一文件里。",
            ],
            imported.ErrorsOrEmptyList.Select(error => error.Description));
    }

    private const string SingleFunnelFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                    { "Name": "制定计划", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Prompt": "写出计划。", "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                    { "Name": "分配执行", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "计划": "制定计划@计划" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    private const string TwoCustomFlows = """
        {
          "Flows": [
            {
              "Name": "两级",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                    { "Name": "制定计划", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                    { "Name": "分配执行", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "计划": "制定计划@计划" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    private const string LoopWithInputNode = """
        { "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
            { "Name": "收话", "Kind": "Input", "Inputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "回复", "Type": "Signal" }], "Outputs": [{ "Name": "回答", "Type": "Text" }], "From": [ { "目标": "@目标", "回复": "回话@回复" } ] },
            { "Name": "回话", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "回复", "Type": "Signal" }], "From": [ { "回答": "收话@回答" } ] }
          ] }
        ] } ] }
        """;

    /// <summary>流程名两侧带空白，加载时去掉。</summary>
    private const string SurroundingSpacesFlowName = """
        { "Flows": [ { "Name": "  默认 ", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
            { "Name": "分析", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] }
          ] }
        ] } ] }
        """;
    private const string ExtraFlow = """
        { "Flows": [
          { "Name": "额外", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "干活", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] }
            ] }
          ] }
        ] }
        """;

    /// <summary>一条与测试自带流程同名的流程，可独立装配。</summary>
    private const string OverwritingFlow = """
        { "Flows": [
          { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Name": "干活", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] }
            ] }
          ] }
        ] }
        """;

    private const string DuplicateImportedFlows = """
        { "Flows": [
          { "Name": "重复", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Nodes": [ { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "Start@MODEL_NAME" }, "From": [ { "目标": "Start@目标" } ] } ] },
          { "Name": "重复", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Nodes": [ { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "Start@MODEL_NAME" }, "From": [ { "目标": "Start@目标" } ] } ] }
        ] }
        """;

    /// <summary>只写流程，引用的节点库定义不在文件里，按文件自身装配会报缺定义。</summary>
    private const string LibraryReferenceOnlyFlow = """
        { "Flows": [
          { "Name": "复用", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Label": "干活", "Use": "外部库", "Static": { "MODEL_NAME": "@MODEL_NAME" } }
            ] }
          ] }
        ] }
        """;

    private const string LibraryFlow = """
        {
          "Nodes": [
            { "Name": "执行", "Kind": "Llm" },
            {
              "Name": "交付",
              "Kind": "Container",
              "Inputs": [{ "Name": "计划" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }],
              "Nodes": [
                { "Label": "实施", "Use": "执行", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "计划": "@计划" } ] }
              ]
            }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                { "Name": "制定计划", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                { "Label": "交付", "Use": "交付", "In": { "计划": "制定计划@计划", "MODEL_NAME": "@MODEL_NAME" } }
                  ]
                }
              ]
            }
          ]
        }
        """;

}
