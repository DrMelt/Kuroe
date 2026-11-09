using ErrorOr;

namespace Kuroe.Tool.FileAccess.Shared;

/// <summary>根目录内的文件访问：路径解析与越界拒绝，列目录与按行号范围读取。</summary>
public interface IFileScope
{
    /// <summary>根目录的绝对路径。</summary>
    string Root { get; }

    /// <summary>路径参数的解析范围，解析基准是根目录，边界是边界目录。</summary>
    ToolPathScope Paths { get; }

    /// <summary>路径的展示。空路径即路径基准目录。</summary>
    string Display(string path);

    /// <summary>把相对路径解析为根目录内的绝对路径，越界或不可解析时给出拒绝错误。</summary>
    ErrorOr<string> Resolve(string path);

    /// <summary>解析到根目录内已存在的文件。目标是目录或不存在时给出拒绝错误。</summary>
    /// <param name="path">相对路径基准目录的路径。</param>
    /// <param name="listFunction">提示里指出的列目录函数名。</param>
    ErrorOr<string> ResolveFile(string path, string listFunction);

    /// <summary>列出根目录内目录的直接子项，按名字序数排序。目标是文件或不存在时给出拒绝错误。</summary>
    /// <param name="path">相对路径基准目录的目录路径。</param>
    /// <param name="fileFunction">提示里指出的读取函数名。</param>
    ErrorOr<IReadOnlyList<DirectoryEntry>> ListEntries(string path, string fileFunction);

    /// <summary>读取根目录内文件指定行号范围的内容，保留行尾终止符。</summary>
    /// <param name="path">相对路径基准目录的文件路径。</param>
    /// <param name="from">起始行号，从 1 起。</param>
    /// <param name="to">结束行号，不小于起始行号。</param>
    /// <param name="characterBudget">保留的行内容与终止符的字符预算。</param>
    /// <param name="lineCharacterBudget">单行行长上限，含终止符。</param>
    /// <param name="listFunction">提示里指出的列目录函数名。</param>
    ErrorOr<SourceLineReadResult> ReadRange(
        string path,
        int from,
        int to,
        int characterBudget,
        int lineCharacterBudget,
        string listFunction);
}