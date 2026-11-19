using System.Text.Json;
using ErrorOr;
using Kuroe.FlowDescription.Compilation.Description;
using Kuroe.FlowDescription.Shared.Compilation;
using Kuroe.FlowDescription.Shared;
using Kuroe.FlowDescription.Shared.StaticGraph.Flow;
using Kuroe.Flow.NodeType.Container;
using Kuroe.Node.Approval;
using Kuroe.Node.File;
using Kuroe.Node.Input;
using Kuroe.Node.Llm;
using Kuroe.TestSupport;
using Xunit;

namespace Kuroe.FlowDescription.Compilation.Integration.Tests;

/// <summary>承载完整流程示例的文档页面必须能按当前格式读取并通过编译。</summary>
public sealed class DocFlowExamplesTests
{
    /// <summary>按当前注册的节点类型编译一份文档。</summary>
    private static ErrorOr<IReadOnlyList<FlowGraph>> Compile(FlowDescriptionSet document)
    {
        NodeTypeDescriptionRegistry files = new();
        ContainerNodeTypeDescriptions.RegisterInto(files);
        LlmNodeTypeDescriptions.RegisterInto(files, TestTypeFiles.Runners, TestTypeFiles.Contracts);
        InputNodeTypeDescriptions.RegisterInto(files, TestTypeFiles.Runners);
        ApprovalNodeTypeDescriptions.RegisterInto(files, TestTypeFiles.Runners);
        FileNodeTypeDescriptions.RegisterInto(files, Path.GetTempPath(), TestTypeFiles.Runners, TestTypeFiles.Runners);

        IFlowDescriptionCompiler compiler = new FlowDescriptionCompiler(files, new NodeLibraryExpander());
        return compiler.Compile(document, []);
    }

    private const string Page = "docs/user/reference/流程示例.md";

    /// <summary>文档页面的流程示例，每个 Flows 代码块一条。</summary>
    public static TheoryData<string, string, string> Examples() => ExamplesOf(Page);

    [Theory]
    [MemberData(nameof(Examples))]
    public void Documented_flow_example_reads_and_compiles(string heading, string nodes, string flows)
    {
        string json = $"{{\"Nodes\":{nodes},\"Flows\":{flows}}}";

        ErrorOr<FlowDescriptionSet> read = FlowDescriptionSetSource.Read(json);
        Assert.False(read.IsError, $"{Page} 的「{heading}」读取失败：{Describe(read.ErrorsOrEmptyList)}");

        ErrorOr<IReadOnlyList<FlowGraph>> compiled = Compile(read.Value);
        Assert.False(compiled.IsError, $"{Page} 的「{heading}」编译失败：{Describe(compiled.ErrorsOrEmptyList)}");
    }

    /// <summary>从文档页面取出每个 Flows 代码块，与所在小节的 Nodes 代码块配对。格式不合法时直接失败。</summary>
    private static TheoryData<string, string, string> ExamplesOf(string relative)
    {
        TheoryData<string, string, string> examples = [];
        int count = 0;
        foreach (Section section in Sections(relative, File.ReadAllLines(Path.Combine(RepositoryRoot(), relative))))
        {
            string? nodes = section.Blocks
                .Select(block => Property(relative, section.Heading, block, "Nodes"))
                .OfType<string>()
                .FirstOrDefault();
            foreach (string flows in section.Blocks
                .Select(block => Property(relative, section.Heading, block, "Flows"))
                .OfType<string>())
            {
                examples.Add(section.Heading, nodes ?? "[]", flows);
                count++;
            }
        }

        if (count == 0)
        {
            throw new InvalidOperationException($"{relative} 没有可编译的流程示例。");
        }

        return examples;
    }

    /// <summary>Markdown 的一节：二级标题与其下的 json 代码块。</summary>
    private sealed record Section(string Heading, List<string> Blocks);

    /// <summary>按二级标题分组提取 json 代码块。</summary>
    private static List<Section> Sections(string relative, string[] lines)
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

            if (end >= lines.Length)
            {
                throw new InvalidOperationException($"{relative} 的「{current.Heading}」有未闭合的 json 代码块。");
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

    /// <summary>取 json 对象的顶层字段原文，不是对象或没有该字段时为 null，json 不合法时失败。</summary>
    private static string? Property(string relative, string heading, string json, string name)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"{relative} 的「{heading}」json 代码块不是合法 JSON：{ex.Message}", ex);
        }

        using (document)
        {
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

        return null;
    }

    private static string Describe(IReadOnlyList<Error> errors) =>
        string.Join("；", errors.Select(error => error.Description));

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
