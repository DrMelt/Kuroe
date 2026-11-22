using System.Text.RegularExpressions;
using Xunit;
using static Kuroe.Architecture.Tests.Support.RepositoryLayout;

namespace Kuroe.Architecture.Tests;

/// <summary>全局状态边界：静态成员只允许不可变常量与纯函数，服务实例经容器取得，不暴露静态共享实例。</summary>
public sealed partial class GlobalStateTests
{
    /// <summary>静态成员声明的公共前缀，排除不可变字段与类型声明。</summary>
    private const string StaticMember =
        @"\bstatic\s+(?!readonly\b|const\b|class\b|partial\b|async\b|void\b|extern\b|unsafe\b)[\w<>,?\[\]\.]+\s+\w+\s*";

    /// <summary>不可变值对象的共享实例，按源码相对路径豁免。</summary>
    private static readonly string[] InstanceAllowlist =
    [
        "Kuroe.FlowDescription/Definition/Shared/Nodes/OutputDeclaration.cs",
        "Kuroe.Kernel/Execution/Shared/Request/EmptyExecutionParameters.cs",
        "Kuroe.Maf/Definition/Core/MafUpstreamInputs.cs",
    ];

    /// <summary>系统适配器与组合根，允许读取时钟、随机源与环境。</summary>
    private static readonly string[] AmbientAllowlist =
    [
        "Kuroe/Composition/Core/ServiceCollectionExtensions.cs",
        "Kuroe/GraphFlow/Core/GraphFlowServiceCollectionExtensions.cs",
        "Kuroe/Composition/Core/Configuration/Files/KuroePaths.cs",
        "Kuroe.Cli/CliCommandLine.cs",
        "Kuroe.Files/Core/SystemWorkspaceFiles.cs",
        "Kuroe.Kernel/Tasks/Shared/TaskId.cs",
        "Kuroe.Maf/Control/Core/GuidCheckpointIdSource.cs",
    ];

    [Fact(DisplayName = "源码不含可变的静态字段")]
    public void Source_has_no_mutable_static_fields() =>
        AssertAbsent(StaticField(), [], "声明了非 readonly 的静态字段");

    [Fact(DisplayName = "静态属性不带写入器")]
    public void Static_properties_have_no_setter() =>
        AssertAbsent(StaticSetter(), [], "静态属性带写入器");

    [Fact(DisplayName = "服务实例不暴露静态 Instance，不可变值对象除外")]
    public void Services_do_not_expose_static_instance() =>
        AssertAbsent(StaticInstance(), InstanceAllowlist, "暴露了静态 Instance");

    [Fact(DisplayName = "时钟随机源与环境只在系统适配器与组合根读取")]
    public void Ambient_state_is_read_only_in_adapters_and_composition() =>
        AssertAbsent(AmbientAccess(), AmbientAllowlist, "在适配器与组合根之外读取环境状态");

    private static void AssertAbsent(Regex pattern, string[] allowlist, string message)
    {
        foreach ((string file, string text) in CodeTexts())
        {
            if (allowlist.Contains(Path.GetRelativePath(SourceRoot, file).Replace('\\', '/')))
            {
                continue;
            }

            Match match = pattern.Match(text);
            int line = match.Success ? text[..match.Index].Count(ch => ch == '\n') + 1 : 0;
            Assert.False(match.Success, $"{file}:{line} {message}：{match.Value.Trim()}");
        }
    }

    /// <summary>注释行置空后的源码全文，行号与源文件一致，跨行声明可被匹配。</summary>
    private static IEnumerable<(string File, string Text)> CodeTexts()
    {
        foreach (string file in SourceFiles(SourceRoot))
        {
            IEnumerable<string> lines = File.ReadLines(file)
                .Select(line => line.TrimStart().StartsWith('/') || line.TrimStart().StartsWith('*')
                    ? string.Empty
                    : line);
            yield return (file, string.Join('\n', lines));
        }
    }

    [GeneratedRegex(StaticMember + @"(?:=(?![=>])[^;{]*)?;", RegexOptions.Compiled)]
    private static partial Regex StaticField();

    [GeneratedRegex(StaticMember + @"\{[^}]*\bset\b", RegexOptions.Compiled)]
    private static partial Regex StaticSetter();

    [GeneratedRegex(@"\bstatic\s+[\w<>,?\[\]\.]+\s+Instance\b", RegexOptions.Compiled)]
    private static partial Regex StaticInstance();

    [GeneratedRegex(@"\b(?:Guid\.(?:NewGuid|CreateVersion7)|Random\.Shared|Environment\.(?:GetEnvironmentVariable|CurrentDirectory|ProcessId)|Directory\.GetCurrentDirectory|TimeProvider\.System|DateTime(?:Offset)?\.(?:Now|UtcNow))\b", RegexOptions.Compiled)]
    private static partial Regex AmbientAccess();
}
