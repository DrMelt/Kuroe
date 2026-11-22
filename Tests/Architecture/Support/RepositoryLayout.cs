using System.Xml.Linq;

namespace Kuroe.Architecture.Tests.Support;

/// <summary>仓库根、源码与测试目录，以及工程文件与源码文件的读取。</summary>
internal static class RepositoryLayout
{
    internal static readonly string Root = FindRoot();
    internal static readonly string SourceRoot = Path.Combine(Root, "Source");
    internal static readonly string TestsRoot = Path.Combine(Root, "Tests");
    internal static readonly string KernelRoot = Path.Combine(SourceRoot, "Kuroe.Kernel");

    internal static IReadOnlyList<string> InternalsVisibleTo(string project) =>
        [.. XDocument.Load(project)
            .Descendants("InternalsVisibleTo")
            .Select(element => element.Attribute("Include")?.Value)
            .OfType<string>()];

    internal static IReadOnlyList<string> ProjectReferences(string project) =>
        [.. XDocument.Load(project)
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .OfType<string>()
            // csproj 中的 ProjectReference 固定用反斜杠
            .Select(include => Path.GetFullPath(Path.Combine(
                DirectoryOf(project),
                include.Replace('\\', Path.DirectorySeparatorChar))))];

    /// <summary>项目文件的所在目录，项目路径总是带目录。</summary>
    internal static string DirectoryOf(string project) =>
        Path.GetDirectoryName(project) ?? throw new InvalidOperationException($"{project} 没有所在目录。");

    internal static IEnumerable<string> ProjectFiles(string root) =>
        Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path));

    /// <summary>目录下的源码文件，排除构建产物与源生成目录。</summary>
    internal static IEnumerable<string> SourceFiles(string root) =>
        Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path) && !IsGenerated(path));

    internal static bool IsGenerated(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}generated{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    internal static bool IsUnder(string path, string directory)
    {
        string relative = Path.GetRelativePath(directory, path);

        return relative != ".."
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }

    internal static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string FindRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Kuroe.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("找不到仓库根 Kuroe.slnx。");
    }
}
