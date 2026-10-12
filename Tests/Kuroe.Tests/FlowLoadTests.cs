using System.Text.Json;
using Kuroe.Configuration;
using Kuroe.Configuration.Files;
using Kuroe.Nodes.Library;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.TestSupport;
using Kuroe.Workflows.Flows;
using Kuroe.Workflows.Flows.Files;
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
    public void Node_without_kind_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 实施：节点必须声明 Kind，取值 Container、Executable 或 Input。"], Errors(NodeWithoutKind));

    [Fact]
    public void Reference_node_with_kind_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 实施：引用节点的类型由被引用的库定义决定，不写 Kind。"], Errors(ReferenceNodeWithKind));

    [Fact]
    public void Container_without_children_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 整体：容器节点必须声明子节点。"], Errors(ContainerWithoutChildren));

    [Fact]
    public void Executable_with_children_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 整体：Executable 节点不能声明子节点，子节点只属于容器。"], Errors(ExecutableWithChildren));

    [Fact]
    public void Input_node_with_non_data_port_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 收集：输入节点的输出端口只能是 Text 端口，回答写入该端口。"], Errors(InputWithContextPort));

    [Fact]
    public void Execution_in_binding_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 实施：执行节点不写输入端口绑定，接线一律经 From。"], Errors(ExecutionWithIn));

    [Fact]
    public void Input_in_binding_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 收集：执行节点不写输入端口绑定，接线一律经 From。"], Errors(InputWithIn));

    [Fact]
    public void Reference_node_with_outputs_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 分析：引用节点不能声明输出端口与系统指令。"], Errors(ReferenceWithOutputs));

    [Fact]
    public void Input_type_out_of_domain_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 实施：输入端口的 Type 应为 Text、Context 或 Signal。"], Errors(InputTypeOutOfDomain));

    [Fact]
    public void Output_type_out_of_domain_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 准备：输出端口的 Type 应为 Text、WorkItems 或 Context。"], Errors(OutputTypeOutOfDomain));

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
    public void Source_port_repeated_across_groups_is_rejected() =>
        Assert.Equal(
            ["流程 默认 的节点 交付.实施：From 的来源端口 Start@目标 出现在多个组里。"],
            Errors(RepeatedSourcePortAcrossGroups));

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

            var (nodes, flows) = FlowAssembler.Read(file).ThrowIfError();
            FlowAssembler.Assemble(nodes, flows, new NodeLibrary()).ThrowIfError();
            FlowAssembler.Save(store, nodes, flows).ThrowIfError();
            string once = File.ReadAllText(file);

            var (Nodes, Flows) = FlowAssembler.Read(file).ThrowIfError();
            FlowAssembler.Save(store, Nodes, Flows).ThrowIfError();

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
        Assert.Contains("\"Kind\": \"Executable\"", text);
        Assert.Contains("制定计划", text);
        Assert.Contains("分配执行", text);
    }

    /// <summary>端口说明随声明载入，保存后仍写在文件里。</summary>
    [Fact]
    public void Port_descriptions_survive_load_and_save()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        File.WriteAllText(Path.Combine(harness.Root, "ports.json"), PortDescriptions);

        harness.Flows.Import("ports.json").ThrowIfError();

        // 展开后的容器实例保留声明的传入端口、输出端口与成员端口，说明都随声明带过来
        NodeSpec container = harness.Flows.Find(new FlowName("端口说明")).ThrowIfError().RootNode.Nodes![0];
        Assert.Equal("待实施的条目拆分", container.Inputs.Single().Description);
        Assert.Equal("透出的结论", container.ContainerOutputs![new PortName("结论")].Description);
        Assert.Equal("最终结论", container.Nodes![0].Outputs.Single().Description);

        string text = File.ReadAllText(KuroePaths.At(harness.Root).FlowsFile);
        Assert.Contains("\"Description\": \"待实施的条目拆分\"", text);
        Assert.Contains("\"Description\": \"透出的结论\"", text);
        Assert.Contains("\"Description\": \"最终结论\"", text);
    }

    /// <summary>开始端口带固定值：固定值随声明载入，保存后仍写在文件里。</summary>
    [Fact]
    public void Start_port_value_survives_load_and_save()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        File.WriteAllText(Path.Combine(harness.Root, "start.json"), StartWithValueFlow);

        harness.Flows.Import("start.json").ThrowIfError();

        FlowDefinition flow = harness.Flows.Find(new FlowName("默认")).ThrowIfError();
        Port background = Assert.Single(flow.StartOutputs, port => port.Name == new PortName("背景"));
        Assert.Equal("固定背景", background.Value);

        string text = File.ReadAllText(KuroePaths.At(harness.Root).FlowsFile);
        Assert.Contains("\"Value\": \"固定背景\"", text);
    }

    [Fact]
    public void Value_on_node_output_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 干活：输出端口不认识键 Value，Value 只属于开始端口。"], Errors(NodeOutputWithValue));

    /// <summary>开始端口除目标外再声明一个带固定值的端口。</summary>
    private const string StartWithValueFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "背景", "Type": "Text", "Value": "固定背景" }] },
              "Nodes": [
                { "Name": "整体", "Kind": "Container", "Nodes": [
                  { "Name": "干活", "Kind": "Executable", "Model": "执行者", "From": [ { "目标": "Start@目标" } ] }
                ] }
              ]
            }
          ]
        }
        """;

    /// <summary>执行节点的输出端口写了 Value，只属于开始端口。</summary>
    private const string NodeOutputWithValue = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "干活", "Kind": "Executable", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text", "Value": "固定" }], "From": [ { "目标": "Start@目标" } ] }
          ] }
        ] } ] }
        """;

    private const string NoNodes = """
        { "Flows": [ { "Name": "默认", "Models": [] } ] }
        """;

    private const string MultipleRootNodes = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "实施", "Kind": "Executable", "Model": "执行者" },
          { "Name": "收尾", "Kind": "Executable", "Model": "执行者", "From": [ { "结论": "实施@结论" } ] }
        ] } ] }
        """;

    private const string BadNodeMode = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
          { "Name": "制定计划", "Kind": "Executable", "Model": "规划者", "Mode": "PerItemm" }
          ] }
        ] } ] }
        """;

    private const string ContainerPrompt = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "容器", "Kind": "Container", "Prompt": "不落地的提示", "Nodes": [ { "Name": "规划", "Kind": "Executable", "Model": "规划者" } ] }
        ] } ] }
        """;

    private const string ContainerModeRejected = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "实施", "Kind": "Container", "Mode": "Parallel",
            "Nodes": [ { "Name": "撰写", "Kind": "Executable", "Model": "执行者" } ] }
        ] } ] }
        """;

    private const string NodeWithoutKind = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "实施", "Model": "执行者" }
          ] }
        ] } ] }
        """;

    private const string ReferenceNodeWithKind = """
        { "Nodes": [ { "Name": "执行", "Kind": "Executable" } ],
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "实施", "Kind": "Executable", "Use": "执行", "Model": "执行者" }
            ] }
          ] } ] }
        """;

    private const string ContainerWithoutChildren = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container" }
        ] } ] }
        """;

    private const string ExecutableWithChildren = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Executable", "Nodes": [
            { "Name": "实施", "Kind": "Executable", "Model": "执行者" }
          ] }
        ] } ] }
        """;

    private const string InputWithContextPort = """
        { "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "收集", "Kind": "Input", "Outputs": [{ "Name": "背景", "Type": "Context" }] }
          ] }
        ] } ] }
        """;

    private const string ExecutionWithIn = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "准备", "Kind": "Executable", "Model": "执行者" },
            { "Name": "实施", "Kind": "Executable", "Model": "执行者", "In": { "计划": "准备" } }
          ] }
        ] } ] }
        """;

    private const string InputWithIn = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "收集", "Kind": "Input", "In": { "计划": "准备" } },
            { "Name": "准备", "Kind": "Executable", "Model": "执行者" }
          ] }
        ] } ] }
        """;

    private const string ReferenceWithOutputs = """
        { "Nodes": [ { "Name": "分析库", "Kind": "Executable" } ],
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "分析", "Use": "分析库", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }] }
            ] }
          ] } ] }
        """;

    private const string InputTypeOutOfDomain = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "准备", "Kind": "Executable", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }] },
            { "Name": "实施", "Kind": "Executable", "Model": "执行者", "Inputs": [{ "Name": "结论", "Type": "Bogus" }], "From": [ { "结论": "准备@结论" } ] }
          ] }
        ] } ] }
        """;

    private const string OutputTypeOutOfDomain = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "准备", "Kind": "Executable", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Bogus" }] },
            { "Name": "实施", "Kind": "Executable", "Model": "执行者", "From": [ { "结论": "准备@结论" } ] }
          ] }
        ] } ] }
        """;

    private const string ContainerOutPortMissing = """
        { "Nodes": [
            { "Name": "允许工具", "Kind": "Executable", "Outputs": [{ "Name": "结论", "Type": "Text" }] },
            {
              "Name": "交付",
              "Kind": "Container",
              "Models": ["执行者"],
              "Outputs": [{ "Name": "结论", "Ref": "实施@结论" }],
              "Nodes": [ { "Name": "实施", "Use": "允许工具", "Model": "执行者" } ]
            }
          ],
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "交付", "Use": "交付", "Models": { "执行者": "执行者" } },
              { "Name": "复盘", "Kind": "Executable", "Model": "执行者", "From": [ { "不存在": "交付@不存在" } ] }
            ] }
          ] } ]
        }
        """;

    private const string ContainerOutCrossContainer = """
        { "Nodes": [
            { "Name": "允许工具", "Kind": "Executable", "Outputs": [{ "Name": "结论", "Type": "Text" }] },
            {
              "Name": "交付",
              "Kind": "Container",
              "Models": ["执行者"],
              "Outputs": [{ "Name": "结论", "Ref": "实施@结论" }],
              "Nodes": [ { "Name": "实施", "Use": "允许工具", "Model": "执行者" } ]
            }
          ],
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "交付", "Use": "交付", "Models": { "执行者": "执行者" } },
              { "Name": "复盘", "Kind": "Executable", "Model": "执行者", "From": [ { "结论": "交付.实施@结论" } ] }
            ] }
          ] } ]
        }
        """;

    private const string ReferenceContainerFrom = """
        {
          "Nodes": [ { "Name": "组", "Kind": "Container", "Models": ["执行者"], "Nodes": [ { "Name": "做", "Kind": "Executable", "Model": "执行者" } ] } ],
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "组实例", "Use": "组", "Models": { "执行者": "执行者" }, "From": [ { "做": "做@结论" } ] }
            ] }
          ] } ]
        }
        """;

    private const string RepeatedSourcePortAcrossGroups = """
        {
          "Nodes": [
            {
              "Name": "交付",
              "Kind": "Container",
              "Models": ["执行者"],
              "Inputs": [{ "Name": "计划" }, { "Name": "补充" }],
              "Nodes": [
                { "Name": "实施", "Kind": "Executable", "Model": "执行者", "From": [ { "任务": "@计划" }, { "任务": "@计划", "依据": "@补充" } ] }
              ]
            }
          ],
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "交付", "Use": "交付", "In": { "计划": "Start@目标", "补充": "Start@目标" }, "Models": { "执行者": "执行者" } }
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
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Name": "制定计划", "Kind": "Executable", "Model": "规划者", "Prompt": "拆分条目。", "Outputs": [{ "Name": "拆分", "Type": "WorkItems" }], "From": [ { "目标": "Start@目标" } ] },
                    { "Name": "分配执行", "Kind": "Executable", "Model": "执行者", "Mode": "PerItem", "From": [ { "拆分": "制定计划@拆分" } ] }
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
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Name": "制定计划", "Kind": "Executable", "Model": "规划者", "Outputs": [{ "Name": "拆分", "Type": "WorkItems" }], "From": [ { "目标": "Start@目标" } ] },
                    { "Name": "分配执行", "Kind": "Executable", "Model": "执行者", "Mode": "PerItem", "From": [ { "拆分": "制定计划@拆分" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    private const string LoopWithInputNode = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "收话", "Kind": "Input", "Outputs": [{ "Name": "回答", "Type": "Text" }], "From": [ { "目标": "Start@目标", "回复": "回话@回复" } ] },
            { "Name": "回话", "Kind": "Executable", "Model": "执行者", "Outputs": [{ "Name": "回复", "Type": "Text" }], "From": [ { "回答": "收话@回答" } ] }
          ] }
        ] } ] }
        """;

    /// <summary>三类端口声明都带说明，用于验证说明随声明载入并写回文件。</summary>
    private const string PortDescriptions = """
        {
          "Nodes": [
            {
              "Name": "端口组",
              "Kind": "Container",
              "Models": ["执行者"],
              "Inputs": [{ "Name": "计划", "Description": "待实施的条目拆分" }],
              "Outputs": [{ "Name": "结论", "Ref": "分析@结论", "Description": "透出的结论" }],
              "Nodes": [
                { "Name": "分析", "Kind": "Executable", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text", "Description": "最终结论" }], "From": [ { "计划": "@计划" } ] }
              ]
            }
          ],
          "Flows": [
            {
              "Name": "端口说明",
              "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                { "Name": "整体", "Kind": "Container", "Nodes": [
                  { "Name": "端口组", "Use": "端口组", "In": { "计划": "Start@目标" }, "Models": { "执行者": "执行者" } }
                ] }
              ]
            }
          ]
        }
        """;

    /// <summary>覆盖端口声明、系统指令、门控、次数上限与接线标记等结构字段，用于读写往返。</summary>
    private const string StructuralFlow = """
        {
          "Flows": [
            { "Name": "默认", "Description": "结构覆盖", "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [ { "Name": "整体", "Kind": "Container", "Outputs": [{ "Name": "结论", "Ref": "准备@结论" }], "Nodes": [
                { "Name": "准备", "Kind": "Executable", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "SystemPrompt": ["背景"], "Gate": "Review", "MaxRuns": 2, "From": [ { "目标": "Start@目标" } ] },
                { "Name": "规划", "Kind": "Executable", "Model": "执行者", "Prompt": "拆一下", "Outputs": [{ "Name": "拆分", "Type": "WorkItems" }], "From": [ { "目标": "Start@目标" } ] },
                { "Name": "实施", "Kind": "Executable", "Model": "执行者", "Mode": "PerItem", "From": [ { "拆分": "规划@拆分" } ] },
                { "Name": "收尾", "Kind": "Executable", "Model": "执行者",
                  "Inputs": [{ "Name": "结论" }, { "Name": "信号", "Type": "Signal" }],
                  "From": [ { "结论": "准备@结论", "信号": "准备@结论" } ] }
              ] } ] }
          ]
        }
        """;

    /// <summary>流程名两侧带空白，加载时去掉。</summary>
    private const string SurroundingSpacesFlowName = """
        { "Flows": [ { "Name": "  默认 ", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "分析", "Kind": "Executable", "Model": "执行者", "From": [ { "目标": "Start@目标" } ] }
          ] }
        ] } ] }
        """;

    /// <summary>把文件形状的 JSON 装配成流程，取错误说明。</summary>
    private static IReadOnlyList<string> Errors(string json) =>
        [.. FlowAssembler.Assemble(JsonSerializer.Deserialize(json, FlowJson.Default.FlowFileDto)!, new NodeLibrary()).ErrorsOrEmptyList.Select(error => error.Description)];
}
