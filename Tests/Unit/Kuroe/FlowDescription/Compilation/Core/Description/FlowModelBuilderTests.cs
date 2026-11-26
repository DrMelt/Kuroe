using ErrorOr;
using Kuroe.FlowDescription.Description.Shared;
using Kuroe.FlowDescription.Compilation.NodeTypes;
using Kuroe.FlowDescription.Compilation.Shared;
using Kuroe.FlowDescription.Definition.Shared.Flow;
using Kuroe.FlowDescription.Semantics;
using Kuroe.Host.Ability.Node.Container;
using Xunit;
using Kuroe.FlowDescription.Compilation.Library;

namespace Kuroe.FlowDescription.Compilation.Tests.Description;

/// <summary>流程描述到图模型的构造：端口类型名按注册表解析。</summary>
public sealed class FlowModelBuilderTests
{
    [Fact(DisplayName = "未注册的端口类型在编译时被拒绝")]
    public void Unregistered_port_type_is_rejected()
    {
        NodeTypeDescriptionRegistry types = new();
        ContainerNodeTypeDescriptions.RegisterInto(types);
        IFlowDescriptionCompiler compiler = new FlowDescriptionCompiler(types, new NodeLibraryExpander(), new FlowSemanticsAnalyzer());
        FlowDescriptionSet document = new(
            [],
            [
                new FlowEntry(
                    "默认",
                    null,
                    [new PortDescription("目标", "Bogus")],
                    null),
            ]);

        ErrorOr<IReadOnlyList<FlowDefinition>> compiled = compiler.Compile(document);

        Assert.True(compiled.IsError);
        Assert.Contains(
            compiled.ErrorsOrEmptyList,
            error => error.Description.Contains("端口类型 Bogus 未注册。", StringComparison.Ordinal));
    }
}
