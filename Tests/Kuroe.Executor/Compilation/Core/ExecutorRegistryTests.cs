using Kuroe.Executor.Compilation;
using Kuroe.Executor.Shared.Nodes;
using Kuroe.Executor.Shared.Parameters;
using Xunit;

namespace Kuroe.Executor.Compilation.Tests;

/// <summary>执行器描述注册表：同名覆盖、查找与注册序。</summary>
public sealed class ExecutorRegistryTests
{
    [Fact(DisplayName = "注册保持首次顺序并替换说明")]
    public void Register_keeps_the_first_order_and_replaces_the_description()
    {
        ExecutorRegistry registry = new();
        StubDescription first = new("甲");
        StubDescription replacement = new("甲");
        StubDescription second = new("乙");

        registry.Register(first);
        registry.Register(second);
        registry.Register(replacement);

        Assert.Equal([new ExecutorKind("甲"), new ExecutorKind("乙")], registry.Kinds);
        Assert.Same(replacement, registry.Find(new ExecutorKind("甲")));
    }

    [Fact(DisplayName = "按文本查找忽略大小写未找到返回空")]
    public void Find_by_text_ignores_case_and_missing_kinds_return_null()
    {
        ExecutorRegistry registry = new();
        StubDescription description = new("Executable");
        registry.Register(description);

        Assert.Same(description, registry.Find("executable"));
        Assert.Null(registry.Find("Unknown"));
    }

    private sealed class StubDescription(string kind) : IExecutorDescription
    {
        public ExecutorKind Kind { get; } = new(kind);

        public ExecutorShape Shape { get; } = new();

        public IReadOnlyList<ParameterField> Fields => [];

        public IExecutor Read(IParameterReader values, ExecutorMessageHandler fail) =>
            throw new NotSupportedException();
    }
}