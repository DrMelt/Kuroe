using System.Text.Json;
using Kuroe.Configuration;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.TestSupport;
using Kuroe.Workflows.FlowAssembly;
using Kuroe.Workflows.FlowFiles;
using Xunit;

namespace Kuroe.Tests;

/// <summary>流程配置的载入：文件形状的转换与校验、读写往返、从文件加载与导入。</summary>
public sealed class FlowLoadTests
{
    [Fact]
    public void Flow_without_root_node_is_rejected() =>
        Assert.Equal(["流程 默认：流程缺少根节点。"], Errors(NoNodes));

    [Fact]
    public void Flow_with_multiple_root_nodes_is_rejected() =>
        Assert.Equal(["流程 默认：流程只能有一个根节点。"], Errors(MultipleRootNodes));

    [Fact]
    public void Bad_node_mode_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 制定计划：Mode 应为 Single 或 PerItem，收到 PerItemm。"], Errors(BadNodeMode));

    [Fact]
    public void Container_prompt_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 容器：容器节点不是执行节点，不支持 Prompt。"], Errors(ContainerPrompt));

    [Fact]
    public void Container_mode_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 实施：容器不支持 Mode，收到 Parallel。"], Errors(ContainerModeRejected));

    [Fact]
    public void Execution_in_binding_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 实施：执行节点不写输入端口绑定，ContextInput 用 From 条目的 Context 标记。"], Errors(ExecutionWithIn));

    [Fact]
    public void Input_in_binding_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 收集：执行节点不写输入端口绑定，ContextInput 用 From 条目的 Context 标记。"], Errors(InputWithIn));

    [Fact]
    public void Reference_node_with_outputs_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 分析：引用节点不能声明输出端口与系统指令。"], Errors(ReferenceWithOutputs));

    [Fact]
    public void Context_entry_with_signal_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 实施：From 条目不能同时写 Or、Signal、Context。"], Errors(ContextWithSignal));

    [Fact]
    public void Context_entry_with_group_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 实施：From 条目不能同时写 Or、Signal、Context。"], Errors(ContextWithOr));

    [Fact]
    public void Referenced_container_port_missing_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 复盘：容器 交付 没有输出端口 不存在。"], Errors(ContainerOutPortMissing));

    [Fact]
    public void Cross_container_member_reference_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 复盘：不能从容器外直接引用容器内成员或端口 交付.实施，容器对外只暴露输出端口。"], Errors(ContainerOutCrossContainer));

    [Fact]
    public void Referenced_container_with_from_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 组实例：引用容器不能声明 From，容器自身不接线。"], Errors(ReferenceContainerFrom));

    [Fact]
    public void Structural_fields_survive_a_write_read_round_trip()
    {
        string root = Path.Combine(Path.GetTempPath(), $"kuroe-map-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string file = Path.Combine(root, "flows.json");
        File.WriteAllText(file, StructuralFlow);
        try
        {
            FlowStore store = new(file, root);

            FlowFile first = FlowAssembler.Read(file).ThrowIfError();
            FlowAssembler.Assemble(first).ThrowIfError();
            FlowAssembler.Save(store, first).ThrowIfError();
            string once = File.ReadAllText(file);

            FlowAssembler.Save(store, FlowAssembler.Read(file).ThrowIfError()).ThrowIfError();

            Assert.Equal(once, File.ReadAllText(file));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Flow_loads_and_imports_a_custom_definition()
    {
        using KuroeHarness harness = KuroeHarness.Create(SingleFunnelFlow);
        Assert.Contains(harness.Flows.All(), flow => flow.Name == new FlowName("默认"));

        string source = Path.Combine(Path.GetTempPath(), $"kuroe-flow-{Guid.NewGuid():N}.json");
        File.WriteAllText(source, TwoCustomFlows);
        try
        {
            FlowImport imported = harness.Flows.Import(source).ThrowIfError();

            Assert.Single(imported.Names);
            Assert.Contains(harness.Flows.All(), flow => flow.Name == new FlowName("两级"));
            Assert.Equal(2, harness.Flows.Find(new FlowName("两级")).ThrowIfError().RootNode.Nodes!.Count);
        }
        finally
        {
            File.Delete(source);
        }
    }

    [Fact]
    public void Loop_with_input_node_loads()
    {
        using KuroeHarness harness = KuroeHarness.Create(LoopWithInputNode);

        Assert.Contains(harness.Flows.All(), flow => flow.Name == new FlowName("默认"));
    }

    /// <summary>流程名两侧空白在加载时去掉，按去空白后的名字可查到。</summary>
    [Fact]
    public void Flow_name_with_surrounding_spaces_is_trimmed_on_load()
    {
        using KuroeHarness harness = KuroeHarness.Create(SurroundingSpacesFlowName);

        FlowDefinition flow = Assert.Single(harness.Flows.All());
        Assert.Equal(new FlowName("默认"), flow.Name);
        Assert.True(harness.Flows.Find(new FlowName("默认")).IsSuccess);
    }

    [Fact]
    public void Import_resolves_a_relative_path_against_the_work_directory()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        File.WriteAllText(Path.Combine(harness.Root, "extra.json"), SingleFunnelFlow);

        FlowImport imported = harness.Flows.Import("extra.json").ThrowIfError();

        Assert.Single(imported.Names);
        Assert.True(Path.IsPathRooted(imported.Source), $"导入来源应是绝对路径，收到 {imported.Source}。");
    }

    /// <summary>写回的流程文件保留缩进与可读中文，枚举写成名字。</summary>
    [Fact]
    public void Saved_flow_file_keeps_readable_text_and_string_enums()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        File.WriteAllText(Path.Combine(harness.Root, "extra.json"), SingleFunnelFlow);

        harness.Flows.Import("extra.json").ThrowIfError();

        string text = File.ReadAllText(KuroePaths.At(harness.Root).FlowsFile);
        Assert.Contains("\"Output\": \"Plan\"", text);
        Assert.Contains("制定计划", text);
        Assert.Contains("分配执行", text);
    }

    private const string NoNodes = """
        { "Flows": [ { "Name": "默认", "Models": [] } ] }
        """;

    private const string MultipleRootNodes = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "实施", "Model": "执行者" },
          { "Name": "收尾", "Model": "执行者", "From": ["实施@结论"] }
        ] } ] }
        """;

    private const string BadNodeMode = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
          { "Name": "制定计划", "Model": "规划者", "Output": "Plan", "Mode": "PerItemm" }
          ] }
        ] } ] }
        """;

    private const string ContainerPrompt = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }], "Nodes": [
          { "Name": "容器", "Prompt": "不落地的提示", "Nodes": [ { "Name": "规划", "Model": "规划者", "Output": "Plan" } ] }
        ] } ] }
        """;

    private const string ContainerModeRejected = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "实施", "Mode": "Parallel",
            "Nodes": [ { "Name": "撰写", "Model": "执行者" } ] }
        ] } ] }
        """;

    private const string ExecutionWithIn = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "准备", "Output": "Text", "Model": "执行者" },
            { "Name": "实施", "Output": "Text", "Model": "执行者", "In": { "ContextInput": "准备" } }
          ] }
        ] } ] }
        """;

    private const string InputWithIn = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "收集", "Output": "Input", "In": { "ContextInput": "准备" } },
            { "Name": "准备", "Output": "Text", "Model": "执行者" }
          ] }
        ] } ] }
        """;

    private const string ReferenceWithOutputs = """
        { "Nodes": [ { "Name": "分析库", "Output": "Text" } ],
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "分析", "Use": "分析库", "Model": "执行者", "Outputs": ["结论"] }
            ] }
          ] } ] }
        """;

    private const string ContextWithSignal = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "准备", "Output": "Text", "Model": "执行者", "Outputs": ["结论"] },
            { "Name": "实施", "Output": "Text", "Model": "执行者", "From": [ { "Node": "准备@结论", "Context": true, "Signal": true } ] }
          ] }
        ] } ] }
        """;

    private const string ContextWithOr = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "准备", "Output": "Text", "Model": "执行者", "Outputs": ["结论"] },
            { "Name": "实施", "Output": "Text", "Model": "执行者", "From": [ { "Node": "准备@结论", "Context": true, "Or": "组" } ] }
          ] }
        ] } ] }
        """;

    private const string ContainerOutPortMissing = """
        { "Nodes": [
            { "Name": "允许工具", "Output": "Text", "Outputs": ["结论"] },
            {
              "Name": "交付",
              "Out": { "结论": "实施@结论" },
              "Nodes": [ { "Name": "实施", "Use": "允许工具", "Model": "执行者" } ]
            }
          ],
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "交付", "Use": "交付", "Models": { "执行者": "执行者" } },
              { "Name": "复盘", "Output": "Text", "Model": "执行者", "From": ["交付@不存在"] }
            ] }
          ] } ]
        }
        """;

    private const string ContainerOutCrossContainer = """
        { "Nodes": [
            { "Name": "允许工具", "Output": "Text", "Outputs": ["结论"] },
            {
              "Name": "交付",
              "Out": { "结论": "实施@结论" },
              "Nodes": [ { "Name": "实施", "Use": "允许工具", "Model": "执行者" } ]
            }
          ],
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "交付", "Use": "交付", "Models": { "执行者": "执行者" } },
              { "Name": "复盘", "Output": "Text", "Model": "执行者", "From": ["交付.实施@结论"] }
            ] }
          ] } ]
        }
        """;

    private const string ReferenceContainerFrom = """
        {
          "Nodes": [ { "Name": "组", "Nodes": [ { "Name": "做", "Model": "执行者" } ] } ],
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
            { "Name": "整体", "Nodes": [
              { "Name": "组实例", "Use": "组", "Models": { "执行者": "执行者" }, "From": [{ "Node": "做@结论", "Signal": true }] }
            ] }
          ] } ]
        }
        """;

    private const string SingleFunnelFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "规划者" },
                { "Name": "执行者", "Tools": ["GetLocalTime"] }
              ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                    { "Name": "制定计划", "Model": "规划者", "Output": "Plan", "Prompt": "拆分条目。" },
                    { "Name": "分配执行", "Model": "执行者", "Mode": "PerItem", "From": ["制定计划@拆分"] }
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
              "Models": [
                { "Name": "规划者" },
                { "Name": "执行者" }
              ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                    { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
                    { "Name": "分配执行", "Model": "执行者", "Mode": "PerItem", "From": ["制定计划@拆分"] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    private const string LoopWithInputNode = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "收话", "Output": "Input", "Outputs": ["回答"], "From": ["回话@回复"] },
            { "Name": "回话", "Output": "Text", "Model": "执行者", "Outputs": ["回复"], "From": ["收话@回答"] }
          ] }
        ] } ] }
        """;

    /// <summary>覆盖端口声明、系统指令、门控、次数上限、拆分配置与接线标记等结构字段，用于读写往返。</summary>
    private const string StructuralFlow = """
        {
          "Flows": [
            { "Name": "默认", "Description": "结构覆盖", "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Nodes": [ { "Name": "整体", "Nodes": [
                { "Name": "准备", "Output": "Text", "Model": "执行者", "Outputs": ["结论"], "SystemPrompt": ["背景"], "Gate": "Review", "MaxRuns": 2 },
                { "Name": "规划", "Output": "Plan", "Model": "执行者", "Prompt": "拆一下",
                  "Split": { "Items": [ { "Title": "甲", "Instruction": "做甲" } ], "ExtrasMax": 1 } },
                { "Name": "实施", "Output": "Text", "Model": "执行者", "Mode": "PerItem", "From": ["规划@拆分"] },
                { "Name": "收尾", "Output": "Text", "Model": "执行者",
                  "From": [ "准备@结论", { "Node": "准备@结论", "Signal": true }, { "Node": "准备@结论", "Or": "任选" } ] }
              ] } ] }
          ]
        }
        """;

    /// <summary>流程名两侧带空白，加载时去掉。</summary>
    private const string SurroundingSpacesFlowName = """
        { "Flows": [ { "Name": "  默认 ", "Models": [{ "Name": "执行者", "Model": "fake" }], "Nodes": [
          { "Name": "整体", "Nodes": [
            { "Name": "分析", "Output": "Text", "Model": "执行者" }
          ] }
        ] } ] }
        """;

    /// <summary>把文件形状的 JSON 装配成流程，取错误说明。</summary>
    private static IReadOnlyList<string> Errors(string json) =>
        [.. FlowAssembler.Assemble(JsonSerializer.Deserialize(json, FlowJson.Default.FlowFileDto)!).ErrorsOrEmptyList.Select(error => error.Description)];
}
