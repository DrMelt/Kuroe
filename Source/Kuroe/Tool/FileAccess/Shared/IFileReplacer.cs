using System.Text;
using ErrorOr;

namespace Kuroe.Tool.FileAccess.Shared;

/// <summary>根目录内的批量文本替换：展开路径范围，按后缀筛选文本文件，在每个文件内替换全部不重叠匹配。</summary>
public interface IFileReplacer
{
    /// <summary>在路径范围内批量替换文本。单个文件读取或写入失败时记录失败并继续处理其余文件。</summary>
    /// <param name="paths">目录或文件范围，空集合表示路径基准目录。</param>
    /// <param name="replacement">替换条件与替换文本。</param>
    /// <param name="dryRun">是否只报告而不写入。</param>
    /// <param name="encodingOverride">写回编码，null 表示保留每个文件读取时的编码与 BOM。</param>
    ErrorOr<TextReplaceResult> Replace(
        IReadOnlyList<string> paths,
        TextReplacement replacement,
        bool dryRun,
        Encoding? encodingOverride);
}