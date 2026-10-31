using ErrorOr;
using Kuroe.Flow.Compilation;
using Kuroe.Flow.Compilation.Description;
using Kuroe.Flow.Compilation.Shared;
using Kuroe.Flow.InputChannel;
using Kuroe.Flow.Json;
using Kuroe.Flow.NodeLibrary;
using Kuroe.Flow.NodeType.Container;
using Kuroe.Node.Approval;
using Kuroe.Node.File;
using Kuroe.Node.Input;
using Kuroe.Node.Llm;
using Xunit;

namespace Kuroe.Tests;

/// <summary>文档里的完整流程示例必须能按当前格式读取并通过编译。</summary>
public sealed class DocFlowExamplesTests
{
    private static readonly JsonFlowFormat Format = new();

    /// <summary>按当前注册的节点类型编译一份文档。</summary>
    private static ErrorOr<CompiledDocument> Compile(FlowDocument document)
    {
        var inputs = new InputRegistry();
        NodeTypeDescriptionRegistry files = new();
        ContainerNodeTypeDescriptions.RegisterInto(files);
        LlmNodeTypeDescriptions.RegisterInto(files, TestTypeFiles.Contracts);
        InputNodeTypeDescriptions.RegisterInto(files, inputs);
        ApprovalNodeTypeDescriptions.RegisterInto(files, inputs);
        FileNodeTypeDescriptions.RegisterInto(files, Path.GetTempPath());

        IFlowDescriptionCompiler compiler = new FlowDescriptionCompiler(files, new NodeLibraryExpander());
        return compiler.Compile(document, []);
    }

    [Theory]
    [InlineData("docs/reference/流程示例.md")]
    [InlineData("docs/快速开始.md")]
    public void Documented_flow_examples_compile(string relative)
    {
        string path = Path.Combine(RepositoryRoot(), relative);
        List<string> blocks = JsonBlocks(File.ReadAllLines(path));
        Assert.NotEmpty(blocks);
        for (int index = 0; index < blocks.Count; index++)
        {
            ErrorOr<FlowDocument> read = Format.Read(blocks[index]);
            Assert.False(read.IsError,
                $"{relative} 第 {index + 1} 段读取失败：{string.Join("；", read.ErrorsOrEmptyList.Select(error => error.Description))}");
            ErrorOr<CompiledDocument> compiled = Compile(read.Value);
            Assert.False(compiled.IsError,
                $"{relative} 第 {index + 1} 段编译失败：{string.Join("；", compiled.ErrorsOrEmptyList.Select(error => error.Description))}");
        }
    }

    /// <summary>仓库根目录，按解决方案文件向上查找。</summary>
    private static string RepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Kuroe.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("找不到仓库根目录。");
    }

    /// <summary>提取 markdown 里包含 Flows 的 json 代码块。</summary>
    private static List<string> JsonBlocks(string[] lines)
    {
        List<string> blocks = [];
        int index = 0;
        while (index < lines.Length)
        {
            if (!lines[index].TrimStart().StartsWith("```json", StringComparison.Ordinal))
            {
                index++;
                continue;
            }

            int start = index + 1;
            int end = start;
            while (end < lines.Length && !lines[end].TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                end++;
            }

            string text = string.Join('\n', lines.Skip(start).Take(end - start));
            if (text.Contains("\"Flows\"", StringComparison.Ordinal))
            {
                blocks.Add(text);
            }

            index = end + 1;
        }

        return blocks;
    }
}
