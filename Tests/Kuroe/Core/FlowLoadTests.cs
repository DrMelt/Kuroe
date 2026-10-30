using ErrorOr;
using Kuroe.Configuration.Files;
using Kuroe.Flow.Compilation.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Kuroe.Flow.Json;
using Kuroe.Flow.NodeLibrary;

using Kuroe.Flow.InputChannel;
using Kuroe.Flow.Compilation;
using Kuroe.Workflows.Flows;
using Kuroe.Workflows.Flows.Files;
using Xunit;

namespace Kuroe.Tests;

/// <summary>流程配置的载入：文件形状的转换与校验、读写往返、从文件加载与导入。</summary>
public sealed class FlowLoadTests
{
    /// <summary>解析与读写往返用的外部输入协议实例，这些用例不驱动节点执行。</summary>
    private static readonly InputRegistry Inputs = new();

    /// <summary>描述格式与编译管线，错误用例共用一个实现。</summary>
    private static readonly JsonFlowFormat Format = new();

    private static IFlowDescriptionCompiler Compiler =>
        new FlowDescriptionCompiler(TestTypeFiles.Registry(Inputs), new NodeLibraryExpander());

    [Fact]
    public void Flow_without_root_node_is_rejected() =>
        Assert.Equal(["流程 默认：流程缺少根节点。"], Errors(NoNodes));

    [Fact]
    public void Flow_with_multiple_root_nodes_is_rejected() =>
        Assert.Equal(["流程 默认：流程只能有一个根节点。"], Errors(MultipleRootNodes));

    [Fact]
    public void Approval_node_with_invalid_input_port_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 审阅：端口名不能为空或含 @。"], Errors(ApprovalInputPortWithoutName));

    [Fact]
    public void Container_with_invalid_max_executions_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 整体：MaxExecutions 必须是正整数。"], Errors(ContainerWithZeroMaxExecutions));

    [Fact]
    public void Unknown_node_field_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 制定计划：字段 Mode 不属于 Llm 节点。"], Errors(BadNodeMode));

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
        Assert.Equal(["流程 默认 的节点 实施：输入端口的 Type 应为已注册的端口类型名。"], Errors(InputTypeOutOfDomain));

    [Fact]
    public void Output_type_out_of_domain_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 准备：输出端口的 Type 应为已注册的端口类型名。"], Errors(OutputTypeOutOfDomain));

    [Fact]
    public void Referenced_container_port_missing_is_rejected() =>
        Assert.Equal(
        [
            "流程 默认 的节点 整体/复盘：节点 整体/交付 没有输出端口 不存在。",
        ], Errors(ContainerOutPortMissing));

    [Fact]
    public void Cross_container_member_reference_is_rejected() =>
        Assert.Equal(
            ["流程 默认 的节点 整体：根容器的端口 结论 引用的节点 交付.实施 不在流程作用域里。"],
            Errors(ContainerOutCrossContainer));

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
            FlowStore store = new(file, root, Format, Format);

            FlowDocument document = store.Read(file).ThrowIfError();
            Compiler.Compile(document, []).ThrowIfError();
            store.Save(document).ThrowIfError();
            string once = File.ReadAllText(file);

            Assert.Contains("\"Prompt\": \"写出计划\"", once);
            Assert.Contains("\"SystemPrompt\": [", once);
            Assert.DoesNotContain("\": null", once);

            store.Save(store.Read(file).ThrowIfError()).ThrowIfError();

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
        File.WriteAllText(file, TestFlows.ReviewFlow);
        try
        {
            FlowStore store = new(file, root, Format, Format);

            FlowDocument document = store.Read(file).ThrowIfError();
            Compiler.Compile(document, []).ThrowIfError();
            store.Save(document).ThrowIfError();
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

    /// <summary>端口说明随声明载入，保存后仍写在文件里。</summary>
    [Fact]
    public void Port_descriptions_survive_load_and_save()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        File.WriteAllText(Path.Combine(environment.Root, "ports.json"), PortDescriptions);

        environment.Flows.Import("ports.json").ThrowIfError();

        // 展开后的容器实例保留声明的传入端口、输出端口与成员端口，说明都随声明带过来
        NodeSpec container = environment.Flows.Find(new FlowName("端口说明")).ThrowIfError().RootNode.Nodes![0];
        Assert.Equal("待实施的条目拆分", container.Inputs.Single(input => input.Name.Value == "计划").Description);
        Assert.Equal("透出的结论", container.Outputs.Single(port => port.Name == new PortName("结论")).Description);
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
    public void Value_on_dynamic_node_output_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 干活：动态输出端口 结论 不能写 Value。"], Errors(NodeOutputWithValue));

    /// <summary>输入端口默认值随声明载入，保存后仍写在文件里。</summary>
    [Fact]
    public void Input_default_survives_load_and_save()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        File.WriteAllText(Path.Combine(environment.Root, "input-default.json"), InputDefaultFlow);

        environment.Flows.Import("input-default.json").ThrowIfError();

        NodeSpec implement = environment.Flows.Find(new FlowName("默认")).ThrowIfError().RootNode.Nodes![0];
        Assert.Equal("固定背景", implement.Inputs.Single(port => port.Name == new PortName("背景")).Default);

        string text = File.ReadAllText(KuroePaths.At(environment.Root).FlowsFile);
        Assert.Contains("\"Default\": \"固定背景\"", text);
    }

    [Fact]
    public void Value_on_node_input_is_rejected() =>
        Assert.Equal(
            ["流程 默认 的节点 干活：输入端口不认识键 Value，条目写 Name、Type、Description、Default、Static、Role 与 Trigger。"],
            Errors(NodeInputWithValue));

    /// <summary>定义条目写了标签，节点名与标签的写位置互斥。</summary>
    [Fact]
    public void Definition_entry_writing_label_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 实施：定义条目写节点名 Name，Label 只属于被使用的节点。"], Errors(DefinitionWithLabel));

    /// <summary>被使用的节点写了节点名，实例用标签区分。</summary>
    [Fact]
    public void Reference_entry_writing_name_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 副本：被使用的节点写 Label 区分，不写 Name。"], Errors(ReferenceWithName));

    /// <summary>库定义的节点名不能用流程起点的保留名。</summary>
    [Fact]
    public void Library_definition_named_like_start_is_rejected() =>
        Assert.Equal(["节点库定义 Start：节点名 Start 是流程起点的保留名。"], Errors(DefinitionNamedStart));

    /// <summary>同一容器内两次引用同一个定义给相同标签，标签的唯一性被破坏。</summary>
    [Fact]
    public void Same_label_twice_in_one_container_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 副本：同一容器内的节点标签重复。"], Errors(SameLabelTwice));

    /// <summary>被使用的节点必须写标签，缺标签时节点在图里没有标识段。</summary>
    [Fact]
    public void Reference_entry_without_label_is_rejected() =>
        Assert.Equal(["流程 默认 的节点 执行：被使用的节点必须写 Label，它是节点在图里的标识段。"], Errors(LabelLessReferenceFlow));

    private const string DefinitionWithLabel = """
        { "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] }, "Nodes": [
          { "Label": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "Start@MODEL_NAME" } }
        ] } ] }
        """;

    private const string ReferenceWithName = """
        { "Nodes": [ { "Name": "执行", "Kind": "Llm" } ],
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] }, "Nodes": [
            { "Name": "副本", "Use": "执行", "Static": { "MODEL_NAME": "Start@MODEL_NAME" }, "From": [ { "目标": "Start@目标" } ] }
          ] } ] }
        """;

    private const string DefinitionNamedStart = """
        { "Nodes": [ { "Name": "Start", "Kind": "Llm" } ] }
        """;

    private const string SameLabelTwice = """
        { "Nodes": [ { "Name": "执行", "Kind": "Llm" } ],
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Label": "副本", "Use": "执行", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] },
              { "Label": "副本", "Use": "执行", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] }
            ] }
          ] } ] }
        """;

    private const string LabelLessReferenceFlow = """
        { "Nodes": [ { "Name": "执行", "Kind": "Llm" } ],
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Use": "执行", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] }
            ] }
          ] } ] }
        """;

    /// <summary>执行节点的输入端口声明默认值，From 省略该端口。</summary>
    private const string InputDefaultFlow = """
        { "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
            { "Name": "干活", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "目标" }, { "Name": "背景", "Default": "固定背景" }], "From": [ { "目标": "@目标" } ] }
          ] }
        ] } ] }
        """;

    /// <summary>输入端口写了 Value，默认值只认 Default。</summary>
    private const string NodeInputWithValue = """
        { "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
            { "Name": "干活", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "目标", "Value": "固定" }], "From": [ { "目标": "@目标" } ] }
          ] }
        ] } ] }
        """;

    /// <summary>开始端口除目标外再声明一个带固定值的端口。</summary>
    private const string StartWithValueFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "背景", "Type": "Text", "Value": "固定背景" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] },
              "Nodes": [
                { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                  { "Name": "干活", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] }
                ] }
              ]
            }
          ]
        }
        """;

    /// <summary>执行节点的输出端口写了 Value，只属于开始端口。</summary>
    private const string NodeOutputWithValue = """
        { "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
            { "Name": "干活", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text", "Value": "固定" }], "From": [ { "目标": "@目标" } ] }
          ] }
        ] } ] }
        """;

    private const string NoNodes = """
        { "Flows": [ { "Name": "默认" } ] }
        """;

    private const string MultipleRootNodes = """
        { "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] }, "Nodes": [
          { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "Start@MODEL_NAME" } },
          { "Name": "收尾", "Kind": "Llm", "Static": { "MODEL_NAME": "Start@MODEL_NAME" }, "From": [ { "结论": "实施@结论" } ] }
        ] } ] }
        """;

    private const string ApprovalInputPortWithoutName = """
        { "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "审阅", "Kind": "Approval", "Inputs": [ { "Type": "Signal" } ], "From": [ { "目标": "Start@目标" } ] }
        ] } ] }
        """;

    private const string ContainerWithZeroMaxExecutions = """
        { "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "MaxExecutions": 0, "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
            { "Name": "干活", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "MaxExecutions": 5, "From": [ { "目标": "@目标" } ] }
          ] }
        ] } ] }
        """;

    private const string BadNodeMode = """
        { "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
          { "Name": "制定计划", "Kind": "Llm", "Mode": "PerItemm" }
          ] }
        ] } ] }
        """;

    private const string ContainerPrompt = """
        { "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "容器", "Kind": "Container", "Prompt": "不落地的提示", "Nodes": [ { "Name": "规划", "Kind": "Llm" } ] }
        ] } ] }
        """;

    private const string FieldOutsideKind = """
        { "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "规划", "Kind": "Llm", "备注": "随手加的" }
        ] } ] }
        """;

    private const string QuestionOnLlm = """
        { "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "规划", "Kind": "Llm", "Question": "你想问什么" }
        ] } ] }
        """;

    /// <summary>参数名按原文精确匹配，大小写变体按未知字段报出。</summary>
    private const string CaseSensitiveParameterName = """
        { "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "规划", "Kind": "Llm", "PROMPT": "写点什么" }
        ] } ] }
        """;

    /// <summary>Tools 参数写成文本，形状不符报出。</summary>
    private const string ToolsNotArray = """
        { "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "规划", "Kind": "Llm", "Tools": "GetLocalTime" }
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
        { "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "规划", "Kind": "Llm", "SystemPrompt": ["  "] }
        ] } ] }
        """;

    private const string NodeWithoutKind = """
        { "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "实施" }
          ] }
        ] } ] }
        """;

    private const string NodeWithUnknownKind = """
        { "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "实施", "Kind": "Bogus" }
          ] }
        ] } ] }
        """;

    private const string ReferenceNodeWithKind = """
        { "Nodes": [ { "Name": "执行", "Kind": "Llm" } ],
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Label": "实施", "Kind": "Llm", "Use": "执行" }
            ] }
          ] } ] }
        """;

    private const string ContainerWithoutChildren = """
        { "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container" }
        ] } ] }
        """;

    private const string ExecutableWithChildren = """
        { "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Llm", "Nodes": [
            { "Name": "实施", "Kind": "Llm" }
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
        { "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "准备", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" } },
            { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "In": { "计划": "准备" } }
          ] }
        ] } ] }
        """;

    private const string InputWithIn = """
        { "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "收集", "Kind": "Input", "In": { "计划": "准备" } },
            { "Name": "准备", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" } }
          ] }
        ] } ] }
        """;

    private const string ReferenceWithOutputs = """
        { "Nodes": [ { "Name": "分析库", "Kind": "Llm" } ],
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Nodes": [
              { "Label": "分析", "Use": "分析库", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }] }
            ] }
          ] } ] }
        """;

    private const string InputTypeOutOfDomain = """
        { "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "准备", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }] },
            { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "结论", "Type": "Bogus" }], "From": [ { "结论": "准备@结论" } ] }
          ] }
        ] } ] }
        """;

    private const string OutputTypeOutOfDomain = """
        { "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Nodes": [
            { "Name": "准备", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Bogus" }] },
            { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "结论": "准备@结论" } ] }
          ] }
        ] } ] }
        """;

    private const string ContainerOutPortMissing = """
        { "Nodes": [
            { "Name": "允许工具", "Kind": "Llm", "Inputs": [{ "Name": "目标" }], "Outputs": [{ "Name": "结论", "Type": "Text" }] },
            {
              "Name": "交付",
              "Kind": "Container",
              "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
              "Outputs": [{ "Name": "结论", "Type": "Text", "Ref": "实施@结论" }],
              "Nodes": [ { "Label": "实施", "Use": "允许工具", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] } ]
            }
          ],
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Label": "交付", "Use": "交付", "In": { "目标": "@目标", "MODEL_NAME": "@MODEL_NAME" } },
              { "Name": "复盘", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "不存在": "交付@不存在" } ] }
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
              "Inputs": [{ "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
              "Outputs": [{ "Name": "结论", "Ref": "实施@结论" }],
              "Nodes": [ { "Label": "实施", "Use": "允许工具", "Static": { "MODEL_NAME": "@MODEL_NAME" } } ]
            }
          ],
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "结论" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "结论": "交付.实施@结论", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Label": "交付", "Use": "交付", "In": { "MODEL_NAME": "@MODEL_NAME" } },
              { "Name": "复盘", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "结论": "@结论" } ] }
            ] }
          ] } ]
        }
        """;

    private const string ReferenceContainerFrom = """
        {
          "Nodes": [ { "Name": "组", "Kind": "Container", "Inputs": [{ "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "Nodes": [ { "Name": "做", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text", "Value": "固定", "Constant": true }] } ] } ],
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] }, "Nodes": [
            { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
              { "Label": "组实例", "Use": "组", "In": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "做": "做@结论" } ] }
            ] }
          ] } ]
        }
        """;

    private const string SingleFunnelFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
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
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
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
        { "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
            { "Name": "收话", "Kind": "Input", "Inputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "回复", "Type": "Signal" }], "Outputs": [{ "Name": "回答", "Type": "Text" }], "From": [ { "目标": "@目标", "回复": "回话@回复" } ] },
            { "Name": "回话", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "回复", "Type": "Signal" }], "From": [ { "回答": "收话@回答" } ] }
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
              "Inputs": [{ "Name": "计划", "Description": "待实施的条目拆分" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
              "Outputs": [{ "Name": "结论", "Ref": "分析@结论", "Description": "透出的结论" }],
              "Nodes": [
                { "Name": "分析", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text", "Description": "最终结论" }], "From": [ { "计划": "@计划" } ] }
              ]
            }
          ],
          "Flows": [
            {
              "Name": "端口说明",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] },
              "Nodes": [
                { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                  { "Label": "端口组", "Use": "端口组", "In": { "计划": "@目标", "MODEL_NAME": "@MODEL_NAME" } }
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
            { "Name": "默认", "Description": "结构覆盖", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] },
              "Nodes": [ { "Name": "整体", "Kind": "Container", "Outputs": [{ "Name": "结论", "Ref": "准备@结论" }], "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                { "Name": "准备", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "SystemPrompt": ["背景"], "MaxExecutions": 2, "From": [ { "目标": "@目标" } ] },
                { "Name": "规划", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Prompt": "写出计划", "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "计划": "规划@计划" } ] },
                { "Name": "收尾", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" },
                  "Inputs": [{ "Name": "结论" }, { "Name": "信号", "Type": "Signal" }],
                  "From": [ { "结论": "准备@结论", "信号": "准备@结论" } ] }
              ] } ] }
          ]
        }
        """;

    /// <summary>流程名两侧带空白，加载时去掉。</summary>
    private const string SurroundingSpacesFlowName = """
        { "Flows": [ { "Name": "  默认 ", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] }, "Nodes": [
          { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
            { "Name": "分析", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] }
          ] }
        ] } ] }
        """;

    [Fact]
    public void Tool_port_mapping_for_an_unknown_tool_is_rejected()
    {
        string[] errors = [.. Errors("""
            {
              "Flows": [
                {
                  "Name": "默认",
                  "Start": { "Outputs": [ { "Name": "目标", "Type": "Text" }, { "Name": "任务路径", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true } ] },
                  "Nodes": [
                    {
                      "Name": "实施",
                      "Kind": "Llm",
                      "Static": { "MODEL_NAME": "Start@MODEL_NAME" },
                      "Inputs": [ { "Name": "目标", "Type": "Text" }, { "Name": "任务路径", "Type": "Text" } ],
                      "Tools": [ "file" ],
                      "ToolPorts": { "missing": { "root": "任务路径" } },
                      "From": [ { "目标": "Start@目标", "任务路径": "Start@任务路径" } ]
                    }
                  ]
                }
              ]
            }
            """)];

        Assert.Contains(errors, error => error.Contains("工具路径 missing 没有匹配任何已注册工具", StringComparison.Ordinal));
    }

    [Fact]
    public void Whitelisted_tool_without_a_mapping_is_rejected()
    {
        string[] errors = [.. Errors("""
            {
              "Flows": [
                {
                  "Name": "默认",
                  "Start": { "Outputs": [ { "Name": "目标", "Type": "Text" }, { "Name": "任务路径", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true } ] },
                  "Nodes": [
                    {
                      "Name": "实施",
                      "Kind": "Llm",
                      "Static": { "MODEL_NAME": "Start@MODEL_NAME" },
                      "Inputs": [ { "Name": "目标", "Type": "Text" }, { "Name": "任务路径", "Type": "Text" } ],
                      "Tools": [ "file/read" ],
                      "From": [ { "目标": "Start@目标", "任务路径": "Start@任务路径" } ]
                    }
                  ]
                }
              ]
            }
            """)];

        Assert.Contains(errors, error => error.Contains("缺少路径变量 root 的映射", StringComparison.Ordinal));
    }

    /// <summary>容器成员不能直接引用开始端口，开始值经根容器的 In 进入。</summary>
    [Fact]
    public void Container_member_cannot_reference_start_directly() =>
        Assert.Equal(
            ["流程 默认 的节点 实施：From 引用的节点 Start 不在容器 整体 的作用域里。"],
            Errors("""
                {
                  "Flows": [
                    {
                      "Name": "默认",
                      "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] },
                      "Nodes": [
                        {
                          "Name": "整体",
                          "Kind": "Container",
                          "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
                          "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" },
                          "Nodes": [
                            { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [{ "目标": "Start@目标" }] }
                          ]
                        }
                      ]
                    }
                  ]
                }
                """));

    /// <summary>容器成员不能引用容器外的节点，跨边界传值经容器的 In 与 @端口。</summary>
    [Fact]
    public void Container_member_cannot_reference_a_node_outside_the_container() =>
        Assert.Equal(
            ["流程 默认 的节点 实施：From 引用的节点 准备 不在容器 内层 的作用域里。"],
            Errors("""
                {
                  "Flows": [
                    {
                      "Name": "默认",
                      "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] },
                      "Nodes": [
                        {
                          "Name": "整体",
                          "Kind": "Container",
                          "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
                          "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" },
                          "Nodes": [
                            { "Name": "准备", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [{ "目标": "@目标" }] },
                            {
                              "Name": "内层",
                              "Kind": "Container",
                              "Inputs": [{ "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
                              "In": { "MODEL_NAME": "@MODEL_NAME" },
                              "Nodes": [
                                { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [{ "结论": "准备@结论" }] }
                              ]
                            }
                          ]
                        }
                      ]
                    }
                  ]
                }
                """));

    /// <summary>容器成员的 In 在所属容器作用域里求值，不能直接取开始端口。</summary>
    [Fact]
    public void Container_member_in_cannot_reference_start_directly() =>
        Assert.Equal(
            ["流程 默认 的节点 内层：In 引用的节点 Start 不在容器 整体 的作用域里。"],
            Errors("""
                {
                  "Flows": [
                    {
                      "Name": "默认",
                      "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] },
                      "Nodes": [
                        {
                          "Name": "整体",
                          "Kind": "Container",
                          "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
                          "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" },
                          "Nodes": [
                            {
                              "Name": "内层",
                              "Kind": "Container",
                              "Inputs": [{ "Name": "计划" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
                              "In": { "计划": "Start@目标", "MODEL_NAME": "@MODEL_NAME" },
                              "Nodes": [
                                { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [{ "计划": "@计划" }] }
                              ]
                            }
                          ]
                        }
                      ]
                    }
                  ]
                }
                """));

    /// <summary>内联容器声明传入端口并由 In 绑定，嵌套容器经 @端口 透传外层传入值。</summary>
    [Fact]
    public void Nested_inline_container_passes_outer_port_through() =>
        Assert.Empty(Errors("""
            {
              "Flows": [
                {
                  "Name": "默认",
                  "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] },
                  "Nodes": [
                    {
                      "Name": "整体",
                      "Kind": "Container",
                      "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
                      "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" },
                      "Nodes": [
                        { "Name": "准备", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [{ "目标": "@目标" }] },
                        {
                          "Name": "内层",
                          "Kind": "Container",
                          "Inputs": [{ "Name": "计划" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
                          "In": { "计划": "准备@结论", "MODEL_NAME": "@MODEL_NAME" },
                          "Nodes": [
                            { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [{ "计划": "@计划" }] }
                          ]
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """));

    /// <summary>容器 In 的键必须是容器声明的传入端口。</summary>
    [Fact]
    public void Container_in_with_an_undeclared_port_is_rejected() =>
        Assert.Equal(
            ["流程 默认 的节点 整体：端口 风格 不在容器 整体 上。"],
            Errors("""
                {
                  "Flows": [
                    {
                      "Name": "默认",
                      "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "风格", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] },
                      "Nodes": [
                        {
                          "Name": "整体",
                          "Kind": "Container",
                          "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
                          "In": { "目标": "Start@目标", "风格": "Start@风格", "MODEL_NAME": "Start@MODEL_NAME" },
                          "Nodes": [
                            { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [{ "目标": "@目标" }] }
                          ]
                        }
                      ]
                    }
                  ]
                }
                """));

    /// <summary>容器输入端口名在同一节点内唯一。</summary>
    [Fact]
    public void Container_duplicate_input_port_is_rejected() =>
        Assert.Equal(
            ["流程 默认 的节点 整体：输入端口 目标 重复。"],
            Errors("""
                {
                  "Flows": [
                    {
                      "Name": "默认",
                      "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] },
                      "Nodes": [
                        {
                          "Name": "整体",
                          "Kind": "Container",
                          "Inputs": [{ "Name": "目标" }, { "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
                          "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" },
                          "Nodes": [
                            { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [{ "目标": "@目标" }] }
                          ]
                        }
                      ]
                    }
                  ]
                }
                """));

    /// <summary>容器声明的传入端口全部要给出绑定，未使用的端口也不例外。</summary>
    [Fact]
    public void Container_input_without_binding_is_rejected() =>
        Assert.Equal(
            [
                "流程 默认 的节点 整体：端口 目标 没有绑定。",
                "流程 默认 的节点 目标：端口 @目标 没有绑定来源。",
            ],
            Errors("""
                {
                  "Flows": [
                    {
                      "Name": "默认",
                      "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] },
                      "Nodes": [
                        {
                          "Name": "整体",
                          "Kind": "Container",
                          "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
                          "In": { "MODEL_NAME": "Start@MODEL_NAME" },
                          "Nodes": [
                            { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [{ "目标": "@目标" }] }
                          ]
                        }
                      ]
                    }
                  ]
                }
                """));

    /// <summary>流程作用域里只有开始节点，根容器的传入端口只能绑定开始端口。</summary>
    [Fact]
    public void Root_container_in_cannot_reference_a_non_start_node() =>
        Assert.Equal(
            ["流程 默认 的节点 整体：根容器的端口 计划 引用的节点 自造 不在流程作用域里。"],
            Errors("""
                {
                  "Flows": [
                    {
                      "Name": "默认",
                      "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] },
                      "Nodes": [
                        {
                          "Name": "整体",
                          "Kind": "Container",
                          "Inputs": [{ "Name": "计划" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
                          "In": { "计划": "自造@计划", "MODEL_NAME": "Start@MODEL_NAME" },
                          "Nodes": [
                            { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" } }
                          ]
                        }
                      ]
                    }
                  ]
                }
                """));

    /// <summary>根容器没有更外层作用域，不能把传入端口再绑到更外层端口。</summary>
    [Fact]
    public void Root_container_in_cannot_reference_an_outer_port() =>
        Assert.Equal(
            ["流程 默认 的节点 整体：根容器的端口 计划 不能引用更外层端口。"],
            Errors("""
                {
                  "Flows": [
                    {
                      "Name": "默认",
                      "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] },
                      "Nodes": [
                        {
                          "Name": "整体",
                          "Kind": "Container",
                          "Inputs": [{ "Name": "计划" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
                          "In": { "计划": "@计划", "MODEL_NAME": "Start@MODEL_NAME" },
                          "Nodes": [
                            { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" } }
                          ]
                        }
                      ]
                    }
                  ]
                }
                """));

    /// <summary>把文件形状的 JSON 装配成流程，取错误说明。</summary>
    private static IReadOnlyList<string> Errors(string json)
    {
        ErrorOr<FlowDocument> content = Format.Read(json);
        if (content.IsError)
        {
            return [.. content.ErrorsOrEmptyList.Select(error => error.Description)];
        }

        return [.. Compiler.Compile(content.Value, []).ErrorsOrEmptyList.Select(error => error.Description)];
    }
}
