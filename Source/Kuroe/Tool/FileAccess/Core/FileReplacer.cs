using System.Text;
using ErrorOr;
using Kuroe.Tool.FileAccess.Shared;

namespace Kuroe.Tool.FileAccess;

/// <summary>根目录内的批量文本替换：展开路径范围，按后缀筛选文本文件，在每个文件内替换全部不重叠匹配。
/// 单个文件读取或写入失败时记录失败并继续处理其余文件。</summary>
/// <param name="scope">文件访问作用域。</param>
/// <param name="access">路径准入规则，null 表示不限制。</param>
public sealed class FileReplacer(FileScope scope, ToolPathAccess? access = null) : IFileReplacer
{
    private readonly FileScope _scope = scope;
    private readonly FileCandidates _candidates = new(scope, access);

    /// <summary>在路径范围内批量替换文本。dryRun 为真时不写入文件。</summary>
    /// <param name="paths">目录或文件范围，空集合表示路径基准目录。</param>
    /// <param name="replacement">替换条件与替换文本。</param>
    /// <param name="dryRun">是否只报告而不写入。</param>
    /// <param name="encodingOverride">写回编码，null 表示保留每个文件读取时的编码与 BOM。</param>
    public ErrorOr<TextReplaceResult> Replace(
        IReadOnlyList<string> paths,
        TextReplacement replacement,
        bool dryRun,
        Encoding? encodingOverride)
    {
        ErrorOr<IReadOnlyList<string>> candidates = _candidates.Expand(paths);
        if (candidates.IsError)
        {
            return candidates.ErrorsOrEmptyList;
        }

        List<TextReplaceFile> results = [];
        int scannedFiles = 0;

        foreach (string file in candidates.Value)
        {
            if (!replacement.Query.MatchesExtension(file))
            {
                continue;
            }

            scannedFiles++;
            try
            {
                if (TextFileProbe.Detect(_scope.Workspace, file) is not { } format)
                {
                    continue;
                }

                ErrorOr<TextFileContent> content = TextFileContent.Read(_scope.Workspace, file, format);
                if (content.IsError)
                {
                    results.Add(Failed(file, content.FirstError.Description));
                    continue;
                }

                TextFileReplacement change = content.Value.Replace(replacement);
                if (change.Replacements == 0 || change.Text == content.Value.Original)
                {
                    continue;
                }

                if (!dryRun)
                {
                    ErrorOr<Success> written = content.Value.Write(_scope.Workspace, file, change.Text, encodingOverride);
                    if (written.IsError)
                    {
                        results.Add(Failed(file, written.FirstError.Description));
                        continue;
                    }
                }

                results.Add(new TextReplaceFile(file, change.Replacements, change.Lines, null));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                results.Add(Failed(file, $"读取失败：{ex.Message}"));
            }
        }

        return new TextReplaceResult(results, scannedFiles);
    }

    /// <summary>失败文件的结果，替换处数为 0，没有匹配行。</summary>
    private static TextReplaceFile Failed(string file, string error) => new(file, 0, [], error);
}