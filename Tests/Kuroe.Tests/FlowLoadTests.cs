using Kuroe.Configuration.Files;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.Json;
using Kuroe.Flow.NodeLibrary;
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
        Assert.Equal(["流程 默认 的节点 容器：字段 Prompt 不属于 Container 节点。"], Errors(ContainerPrompt));

    [Fact]
    public void Llm_question_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 规划：字段 Question 不属于 Llm 节点。"], Errors(QuestionOnLlm));

    [Fact]
    public void Field_outside_the_kind_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 规划：字段 备注 不属于 Llm 节点。"], Errors(FieldOutsideKind));

    [Fact]
    public void Parameter_name_is_case_sensitive() =>
        Assert.Equal(["流程 默认 的节点 规划：字段 PROMPT 不属于 Llm 节点。"], Errors(CaseSensitiveParameterName));

    [Fact]
    public void Tools_parameter_must_be_a_text_array() =>
        Assert.Equal(["流程 默认 的节点 规划：字段 Tools 必须是文本数组。"], Errors(ToolsNotArray));

    [Fact]
    public void Input_question_parameter_must_be_text() =>
        Assert.Equal(["流程 默认 的节点 收集：字段 Question 必须是文本。"], Errors(InputQuestionNotText));

    [Fact]
    public void Llm_system_prompt_block_must_not_be_blank() =>
        Assert.Equal(["流程 默认 的节点 规划：系统指令块不能为空。"], Errors(BlankSystemPromptBlock));

    [Fact]
    public void Container_mode_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 实施：容器不支持 Mode，收到 Parallel。"], Errors(ContainerModeRejected));

    [Fact]
    public void Node_without_kind_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 实施：节点必须声明 Kind，可用类型名：Container、Llm、Input、Approval。"], Errors(NodeWithoutKind));

    [Fact]
    public void Node_with_unregistered_kind_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 实施：节点类型名 Bogus 未注册，可用类型名：Container、Llm、Input、Approval。"], Errors(NodeWithUnknownKind));

    [Fact]
    public void Reference_node_with_kind_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 实施：引用节点的类型由被引用的库定义决定，不写 Kind。"], Errors(ReferenceNodeWithKind));

    [Fact]
    public void Container_without_children_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 整体：容器节点必须声明子节点。"], Errors(ContainerWithoutChildren));

    [Fact]
    public void Executable_with_children_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 整体：Llm 节点不能声明子节点，子节点只属于容器。"], Errors(ExecutableWithChildren));

    [Fact]
    public void Input_node_with_non_data_port_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 收集：输出端口 背景 的类型只能是 Text。"], Errors(InputWithContextPort));

    [Fact]
    public void Execution_in_binding_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 实施：执行节点不写输入端口绑定，接线一律经 From。"], Errors(ExecutionWithIn));

    [Fact]
    public void Input_in_binding_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 收集：执行节点不写输入端口绑定，接线一律经 From。"], Errors(InputWithIn));

    [Fact]
    public void Reference_node_with_outputs_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 分析：引用节点不能声明输出端口。"], Errors(ReferenceWithOutputs));

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
    public void Structural_fields_survive_a_write_read_round_trip()
    {
        string root = Path.Combine(Path.GetTempPath(), $"kuroe-map-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string file = Path.Combine(root, "flows.json");
        File.WriteAllText(file, StructuralFlow);
        try
        {
            FlowStore store = new(file, root);

            var (nodes, flows) = FlowFile.Read(FlowStore.ReadDto(file).ThrowIfError(), TestTypeFiles.Registry()).ThrowIfError();
            FlowAssembler.Assemble(nodes, flows, new NodeLibrary()).ThrowIfError();
            store.Save(FlowFile.Write(nodes, flows, TestTypeFiles.Registry())).ThrowIfError();
            string once = File.ReadAllText(file);

            Assert.Contains("\"Prompt\": \"拆一下\"", once);
            Assert.Contains("\"SystemPrompt\": [", once);

            var (Nodes, Flows) = FlowFile.Read(FlowStore.ReadDto(file).ThrowIfError(), TestTypeFiles.Registry()).ThrowIfError();
            store.Save(FlowFile.Write(Nodes, Flows, TestTypeFiles.Registry())).ThrowIfError();

            Assert.Equal(once, File.ReadAllText(file));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>类型参数段的字段随写出回到文件：能力白名单与等待型节点的提问。</summary>
    [Fact]
    public void Parameters_survive_a_write_read_round_trip()
    {
        string root = Path.Combine(Path.GetTempPath(), $"kuroe-params-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string file = Path.Combine(root, "flows.json");
        File.WriteAllText(file, TestFlows.ReviewPerItemFlow);
        try
        {
            FlowStore store = new(file, root);

            var (nodes, flows) = FlowFile.Read(FlowStore.ReadDto(file).ThrowIfError(), TestTypeFiles.Registry()).ThrowIfError();
            FlowAssembler.Assemble(nodes, flows, new NodeLibrary()).ThrowIfError();
            store.Save(FlowFile.Write(nodes, flows, TestTypeFiles.Registry())).ThrowIfError();
            string text = File.ReadAllText(file);

            Assert.Contains("\"Tools\": [", text);
            Assert.Contains("\"Question\": \"确认计划\"", text);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

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

    [Fact]
    public void Loop_with_input_node_loads()
    {
        using TestEnvironment environment = TestEnvironment.Create(LoopWithInputNode);

        Assert.Contains(environment.Flows.All(), flow => flow.Name == new FlowName("默认"));
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

    /// <summary>端口说明随声明载入，保存后仍写在文件里。</summary>
    [Fact]
    public void Port_descriptions_survive_load_and_save()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        File.WriteAllText(Path.Combine(environment.Root, "ports.json"), PortDescriptions);

        environment.Flows.Import("ports.json").ThrowIfError();

        // 展开后的容器实例保留声明的传入端口、输出端口与成员端口，说明都随声明带过来
        NodeSpec container = environment.Flows.Find(new FlowName("端口说明")).ThrowIfError().RootNode.Nodes![0];
        Assert.Equal("待实施的条目拆分", container.Inputs.Single().Description);
        Assert.Equal("透出的结论", container.ContainerOutputs![new PortName("结论")].Description);
        Assert.Equal("最终结论", container.Nodes![0].Outputs.Single().Description);

        string text = File.ReadAllText(KuroePaths.At(environment.Root).FlowsFile);
        Assert.Contains("\"Description\": \"待实施的条目拆分\"", text);
        Assert.Contains("\"Description\": \"透出的结论\"", text);
        Assert.Contains("\"Description\": \"最终结论\"", text);
    }

    /// <summary>开始端口带固定值：固定值随声明载入，保存后仍写在文件里。</summary>
    [Fact]
    public void Start_port_value_survives_load_and_save()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        File.WriteAllText(Path.Combine(environment.Root, "start.json"), StartWithValueFlow);

        environment.Flows.Import("start.json").ThrowIfError();

        FlowDefinition flow = environment.Flows.Find(new FlowName("默认")).ThrowIfError();
        Port background = Assert.Single(flow.StartOutputs, port => port.Name == new PortName("背景"));
        Assert.Equal("固定背景", background.Value);

        string text = File.ReadAllText(KuroePaths.At(environment.Root).FlowsFile);
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
                  { "Name": "干活", "Kind": "Llm", "Model": "执行者", "From": [ { "目标": "Start@目标" } ] }
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
            { "Name": "干活", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text", "Value": "固定" }], "From": [ { "目标": "Start@目标" } ] }
          ] }
        ] } ] }
        """;

    private const string NoNodes = """
        { "Flows": [ { "Name": "默认", "Models": [] } ] }
        """;

    private const string MultipleRootNodes = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Nodes": [
          { "Name": "实施", "Kind": "Llm", "Model": "执行者" },
          { "Name": "收尾", "Kind": "Llm", "Model": "执行者", "From": [ { "结论": "实施@结论" } ] }
        ] } ] }
        """;

    private const string BadNodeMode = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
          { "Name": "制定计划", "Kind": "Llm", "Model": "规划者", "Mode": "PerItemm" }
          ] }
        ] } ] }
        """;

    private const string ContainerPrompt = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "容器", "Kind": "Container", "Prompt": "不落地的提示", "Nodes": [ { "Name": "规划", "Kind": "Llm", "Model": "规划者" } ] }
        ] } ] }
        """;

    private const string FieldOutsideKind = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "规划", "Kind": "Llm", "Model": "规划者", "备注": "随手加的" }
        ] } ] }
        """;

    private const string QuestionOnLlm = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "规划", "Kind": "Llm", "Model": "规划者", "Question": "你想问什么" }
        ] } ] }
        """;

    /// <summary>参数名按原文精确匹配，大小写变体按未知字段报出。</summary>
    private const string CaseSensitiveParameterName = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "规划", "Kind": "Llm", "Model": "规划者", "PROMPT": "写点什么" }
        ] } ] }
        """;

    /// <summary>Tools 参数写成文本，形状不符报出。</summary>
    private const string ToolsNotArray = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "规划", "Kind": "Llm", "Model": "规划者", "Tools": "GetLocalTime" }
        ] } ] }
        """;

    /// <summary>Question 参数写成数字，形状不符报出。</summary>
    private const string InputQuestionNotText = """
        { "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "收集", "Kind": "Input", "Question": 3 }
          ] }
        ] } ] }
        """;

    /// <summary>SystemPrompt 参数含空白块，报出空块。</summary>
    private const string BlankSystemPromptBlock = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "规划者" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "规划", "Kind": "Llm", "Model": "规划者", "SystemPrompt": ["  "] }
        ] } ] }
        """;

    private const string ContainerModeRejected = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "实施", "Kind": "Container", "Mode": "Parallel",
            "Nodes": [ { "Name": "撰写", "Kind": "Llm", "Model": "执行者" } ] }
        ] } ] }
        """;

    private const string NodeWithoutKind = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "实施", "Model": "执行者" }
          ] }
        ] } ] }
        """;

    private const string NodeWithUnknownKind = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "实施", "Kind": "Bogus", "Model": "执行者" }
          ] }
        ] } ] }
        """;

    private const string ReferenceNodeWithKind = """
        { "Nodes": [ { "Name": "执行", "Kind": "Llm" } ],
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "实施", "Kind": "Llm", "Use": "执行", "Model": "执行者" }
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
          { "Name": "整体", "Kind": "Llm", "Nodes": [
            { "Name": "实施", "Kind": "Llm", "Model": "执行者" }
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
            { "Name": "准备", "Kind": "Llm", "Model": "执行者" },
            { "Name": "实施", "Kind": "Llm", "Model": "执行者", "In": { "计划": "准备" } }
          ] }
        ] } ] }
        """;

    private const string InputWithIn = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "收集", "Kind": "Input", "In": { "计划": "准备" } },
            { "Name": "准备", "Kind": "Llm", "Model": "执行者" }
          ] }
        ] } ] }
        """;

    private const string ReferenceWithOutputs = """
        { "Nodes": [ { "Name": "分析库", "Kind": "Llm" } ],
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "分析", "Use": "分析库", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }] }
            ] }
          ] } ] }
        """;

    private const string InputTypeOutOfDomain = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "准备", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }] },
            { "Name": "实施", "Kind": "Llm", "Model": "执行者", "Inputs": [{ "Name": "结论", "Type": "Bogus" }], "From": [ { "结论": "准备@结论" } ] }
          ] }
        ] } ] }
        """;

    private const string OutputTypeOutOfDomain = """
        { "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者", "Model": "fake" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "准备", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Bogus" }] },
            { "Name": "实施", "Kind": "Llm", "Model": "执行者", "From": [ { "结论": "准备@结论" } ] }
          ] }
        ] } ] }
        """;

    private const string ContainerOutPortMissing = """
        { "Nodes": [
            { "Name": "允许工具", "Kind": "Llm", "Outputs": [{ "Name": "结论", "Type": "Text" }] },
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
              { "Name": "复盘", "Kind": "Llm", "Model": "执行者", "From": [ { "不存在": "交付@不存在" } ] }
            ] }
          ] } ]
        }
        """;

    private const string ContainerOutCrossContainer = """
        { "Nodes": [
            { "Name": "允许工具", "Kind": "Llm", "Outputs": [{ "Name": "结论", "Type": "Text" }] },
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
              { "Name": "复盘", "Kind": "Llm", "Model": "执行者", "From": [ { "结论": "交付.实施@结论" } ] }
            ] }
          ] } ]
        }
        """;

    private const string ReferenceContainerFrom = """
        {
          "Nodes": [ { "Name": "组", "Kind": "Container", "Models": ["执行者"], "Nodes": [ { "Name": "做", "Kind": "Llm", "Model": "执行者" } ] } ],
          "Flows": [ { "Name": "默认", "Models": [{ "Name": "执行者" }], "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Name": "组实例", "Use": "组", "Models": { "执行者": "执行者" }, "From": [ { "做": "做@结论" } ] }
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
                { "Name": "执行者" }
              ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Name": "制定计划", "Kind": "Llm", "Model": "规划者", "Prompt": "拆分条目。", "Outputs": [{ "Name": "拆分", "Type": "WorkItems" }], "From": [ { "目标": "Start@目标" } ] },
                    { "Name": "分配执行", "Kind": "Llm", "Model": "执行者", "Mode": "PerItem", "From": [ { "拆分": "制定计划@拆分" } ] }
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
                    { "Name": "制定计划", "Kind": "Llm", "Model": "规划者", "Outputs": [{ "Name": "拆分", "Type": "WorkItems" }], "From": [ { "目标": "Start@目标" } ] },
                    { "Name": "分配执行", "Kind": "Llm", "Model": "执行者", "Mode": "PerItem", "From": [ { "拆分": "制定计划@拆分" } ] }
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
            { "Name": "回话", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "回复", "Type": "Text" }], "From": [ { "回答": "收话@回答" } ] }
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
                { "Name": "分析", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text", "Description": "最终结论" }], "From": [ { "计划": "@计划" } ] }
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

    /// <summary>覆盖端口声明、系统指令、次数上限与接线标记等结构字段，用于读写往返。</summary>
    private const string StructuralFlow = """
        {
          "Flows": [
            { "Name": "默认", "Description": "结构覆盖", "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [ { "Name": "整体", "Kind": "Container", "Outputs": [{ "Name": "结论", "Ref": "准备@结论" }], "Nodes": [
                { "Name": "准备", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "SystemPrompt": ["背景"], "MaxRuns": 2, "From": [ { "目标": "Start@目标" } ] },
                { "Name": "规划", "Kind": "Llm", "Model": "执行者", "Prompt": "拆一下", "Outputs": [{ "Name": "拆分", "Type": "WorkItems" }], "From": [ { "目标": "Start@目标" } ] },
                { "Name": "实施", "Kind": "Llm", "Model": "执行者", "Mode": "PerItem", "From": [ { "拆分": "规划@拆分" } ] },
                { "Name": "收尾", "Kind": "Llm", "Model": "执行者",
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
            { "Name": "分析", "Kind": "Llm", "Model": "执行者", "From": [ { "目标": "Start@目标" } ] }
          ] }
        ] } ] }
        """;

    /// <summary>把文件形状的 JSON 装配成流程，取错误说明。</summary>
    private static IReadOnlyList<string> Errors(string json) =>
        [.. FlowFile.Assemble(FlowFileJson.Parse(json)!, new NodeLibrary(), TestTypeFiles.Registry()).ErrorsOrEmptyList.Select(error => error.Description)];
}
