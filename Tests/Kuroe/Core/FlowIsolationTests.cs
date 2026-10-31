using Kuroe.Flow.Compilation;
using Kuroe.Flow.Compilation.Description;
using Kuroe.Flow.Compilation.Shared;
using Kuroe.Flow.Json;
using Kuroe.Flow.NodeLibrary;
using Kuroe.Flow.NodeType.Container;
using Kuroe.Flow.Shared.StaticGraph.Flow;

using Kuroe.Node.Llm;
using Kuroe.Workflows.Flows;
using Kuroe.Workflows.Flows.Files;
using Xunit;

namespace Kuroe.Tests;

/// <summary>流程库按流程隔离编译：不支持的节点只让所在流程不可用。</summary>
public sealed class FlowIsolationTests
{
    [Fact]
    public void Tolerant_load_keeps_compilable_flows_and_reports_unavailable()
    {
        WithLibrary((store, compiler) =>
        {
            FlowService service = FlowService.Create(store, [], compiler, tolerateFailures: true).ThrowIfError();

            Assert.Contains(service.All(), flow => flow.Name.Value == "可运行");
            FlowFailure failure = Assert.Single(service.Failures);
            Assert.Equal("待输入", failure.Name.Value);
            Assert.Contains(failure.Errors, error => error.Description.Contains("未注册", StringComparison.Ordinal));
            Assert.True(service.FindCompiled(new FlowName("待输入")).IsError);
        });
    }

    [Fact]
    public void Strict_load_returns_errors_for_unavailable_flow()
    {
        WithLibrary((store, compiler) =>
        {
            var created = FlowService.Create(store, [], compiler, tolerateFailures: false);

            Assert.True(created.IsError);
            Assert.Contains(created.ErrorsOrEmptyList, error => error.Description.Contains("Input", StringComparison.Ordinal));
        });
    }

    private static void WithLibrary(Action<FlowStore, IFlowDescriptionCompiler> check)
    {
        string root = Path.Combine(Path.GetTempPath(), $"kuroe-isolation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string file = Path.Combine(root, "flows.json");
        File.WriteAllText(file, Mixed);
        try
        {
            NodeTypeDescriptionRegistry types = new();
            ContainerNodeTypeDescriptions.RegisterInto(types);
            LlmNodeTypeDescriptions.RegisterInto(types);
            FlowDescriptionCompiler compiler = new(types, new NodeLibraryExpander());
            JsonFlowFormat format = new();
            FlowStore store = new(file, root, format, format);

            check(store, compiler);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private const string Mixed = """
        {
          "Flows": [
            {
              "Name": "可运行",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] },
              "Nodes": [
                { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "Start@MODEL_NAME" }, "From": [ { "目标": "Start@目标" } ] }
              ]
            },
            {
              "Name": "待输入",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "收话",
                  "Kind": "Input",
                  "Question": "请补充背景",
                  "Outputs": [ { "Name": "回答", "Type": "Text" } ],
                  "From": [ { "目标": "Start@目标" } ]
                }
              ]
            }
          ]
        }
        """;
}