using System.Text.Json;
using ErrorOr;
using Kuroe.Flow.Compilation.Description;
using Kuroe.Flow.Compilation.Shared;
using Kuroe.Flow.InputChannel;
using Kuroe.Flow.NodeLibrary;
using Kuroe.Flow.NodeType.Container;
using Kuroe.Node.Approval;
using Kuroe.Node.File;
using Kuroe.Node.Input;
using Kuroe.Node.Llm;
using Kuroe.TestSupport;
using Xunit;

namespace Kuroe.Flow.Compilation.Integration.Tests;

/// <summary>文档里的完整流程示例必须能按当前格式读取并通过编译。</summary>
public sealed class DocFlowExamplesTests
{
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
        List<Section> sections = Sections(File.ReadAllLines(path));
        int examples = 0;
        foreach (Section section in sections)
        {
            List<string> nodeBlocks =
                [.. section.Blocks.Select(block => Property(block, "Nodes")).OfType<string>()];
            string nodes = nodeBlocks.FirstOrDefault() ?? "[]";

            foreach (string flows in section.Blocks.Select(block => Property(block, "Flows")).OfType<string>())
            {
                examples++;
                string json = $"{{\"Nodes\":{nodes},\"Flows\":{flows}}}";
                ErrorOr<FlowDocument> read = FlowDocumentSource.Read(json);
                Assert.False(read.IsError,
                    $"{relative} 的「{section.Heading}」读取失败：{string.Join("；", read.ErrorsOrEmptyList.Select(error => error.Description))}");
                ErrorOr<CompiledDocument> compiled = Compile(read.Value);
                Assert.False(compiled.IsError,
                    $"{relative} 的「{section.Heading}」编译失败：{string.Join("；", compiled.ErrorsOrEmptyList.Select(error => error.Description))}");
            }
        }

        Assert.True(examples > 0, $"{relative} 没有可编译的流程示例。");
    }

    /// <summary>Markdown 的一节：二级标题与其下的 json 代码块。</summary>
    private sealed record Section(string Heading, List<string> Blocks);

    /// <summary>按二级标题分组提取 json 代码块。</summary>
    private static List<Section> Sections(string[] lines)
    {
        List<Section> sections = [];
        Section current = new(string.Empty, []);
        int index = 0;
        while (index < lines.Length)
        {
            string line = lines[index];
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                if (current.Blocks.Count > 0)
                {
                    sections.Add(current);
                }

                current = new Section(line[3..].Trim(), []);
                index++;
                continue;
            }

            if (!line.TrimStart().StartsWith("```json", StringComparison.Ordinal))
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

            current.Blocks.Add(string.Join('\n', lines.Skip(start).Take(end - start)));
            index = end + 1;
        }

        if (current.Blocks.Count > 0)
        {
            sections.Add(current);
        }

        return sections;
    }

    /// <summary>取 json 对象的顶层字段原文，没有该字段或不是对象时为 null。</summary>
    private static string? Property(string json, string name)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return property.Value.GetRawText();
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
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

}
