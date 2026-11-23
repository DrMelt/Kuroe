using Kuroe.Flow.Compilation;
using Kuroe.FlowDescription.Compilation;
using Kuroe.FlowDescription.Compilation.NodeTypes;
using Kuroe.FlowDescription.Json;
using Kuroe.Flow.NodeType.Container;

using Kuroe.Kernel.Tasks.Shared;
using Kuroe.Node.Llm;
using Kuroe.Composition.Tests.Support;
using Kuroe.Flow.Library;
using Kuroe.Flow.Library.Files;
using Xunit;
using Kuroe.FlowDescription.Compilation.Library;

namespace Kuroe.Composition.Tests.Workflows.Flows;

/// <summary>流程库按流程隔离编译：不支持的节点只让所在流程不可用。</summary>
public sealed class FlowLibraryTests
{
    [Fact(DisplayName = "宽容加载保留可编译流程并报告不可用")]
    public void Tolerant_load_keeps_compilable_flows_and_reports_unavailable()
    {
        WithLibrary((store, compiler) =>
        {
            FlowLibrary library = FlowLibrary.Create(store, compiler, tolerateFailures: true).ThrowIfError();
            FlowCatalog catalog = new(library);

            Assert.Contains(catalog.All(), flow => flow.Name.Value == "可运行");
            FlowFailure failure = Assert.Single(library.Failures);
            Assert.Equal("待输入", failure.Name.Value);
            Assert.Contains(failure.Errors, error => error.Description.Contains("未注册", StringComparison.Ordinal));
            Assert.True(catalog.FindCompiled(new FlowName("待输入")).IsError);
        });
    }

    [Fact(DisplayName = "严格加载对不可用流程返回错误")]
    public void Strict_load_returns_errors_for_unavailable_flow()
    {
        WithLibrary((store, compiler) =>
        {
            var created = FlowLibrary.Create(store, compiler, tolerateFailures: false);

            Assert.True(created.IsError);
            Assert.Contains(created.ErrorsOrEmptyList, error => error.Description.Contains("Input", StringComparison.Ordinal));
        });
    }

    private static void WithLibrary(Action<FlowStore, IFlowCompiler> check)
    {
        string root = Path.Combine(Path.GetTempPath(), $"kuroe-isolation-{Guid.NewGuid():N}");
        string nodesFile = Path.Combine(root, "nodes.json");
        string flowsFile = Path.Combine(root, "flows.json");
        TestSupport.InMemoryWorkspaceFiles files = new();
        files.AddFile(flowsFile, Mixed);

        NodeTypeDescriptionRegistry types = new();
        ContainerNodeTypeDescriptions.RegisterInto(types);
        LlmNodeTypeDescriptions.RegisterInto(types);
        FlowCompilation compiler = new(
            new FlowDescriptionCompiler(types, new NodeLibraryExpander()),
            new FlowGraphCompiler());
        JsonNodeFormat nodeFormat = new();
        JsonFlowFormat flowFormat = new();
        JsonFlowImportReader importFormat = new(nodeFormat, flowFormat);
        FlowStore store = new(
            nodesFile,
            flowsFile,
            root,
            nodeFormat,
            nodeFormat,
            flowFormat,
            flowFormat,
            importFormat,
            files);

        check(store, compiler);
    }

    private const string Mixed = """
        {
          "Flows": [
            {
              "Name": "可运行",
              "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }],
              "Root": { "Name": "实施", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] }
            },
            {
              "Name": "待输入",
              "Ports": [{ "Name": "目标", "Type": "Text" }],
              "Root": {
                "Name": "收话",
                "Kind": "Input",
                "Parameters": { "Question": "请补充背景" },
                "Outputs": [ { "Name": "回答", "Type": "Text" } ],
                "From": [ { "目标": "@目标" } ]
              }
            }
          ]
        }
        """;
}
