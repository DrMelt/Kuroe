using System.Text;
using Kuroe.Files.Shared;

namespace Kuroe.TestSupport;

/// <summary>内存文件访问：按完整路径保存文件字节与目录集合，供核心逻辑测试替代真实磁盘。</summary>
public sealed class InMemoryWorkspaceFiles : IWorkspaceFiles
{
    private static readonly StringComparer Comparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private readonly Lock _gate = new();
    private readonly HashSet<string> _directories = new(Comparer);
    private readonly Dictionary<string, byte[]> _files = new(Comparer);
    private readonly Dictionary<string, FileFacts> _facts = new(Comparer);

    /// <summary>建立内存文件访问，当前目录视为已存在，与真实文件系统一致。</summary>
    public InMemoryWorkspaceFiles() => AddDirectoryCore(Directory.GetCurrentDirectory());

    /// <summary>加入一个目录，父目录一并加入。</summary>
    public InMemoryWorkspaceFiles AddDirectory(string path)
    {
        lock (_gate)
        {
            AddDirectoryCore(Full(path));
        }

        return this;
    }

    /// <summary>按编码加入一个文本文件，编码的前导字节一并写入。</summary>
    public InMemoryWorkspaceFiles AddFile(string path, string text, Encoding? encoding = null)
    {
        Encoding used = encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        return AddBytes(path, [.. used.GetPreamble(), .. used.GetBytes(text)]);
    }

    /// <summary>加入一个文件并给出它的原始字节。</summary>
    public InMemoryWorkspaceFiles AddBytes(string path, byte[] bytes)
    {
        string file = Full(path);
        lock (_gate)
        {
            AddParentDirectories(file);
            _facts.Remove(file);
            _files[file] = [.. bytes];
        }

        return this;
    }

    /// <summary>固定一个文件的文件系统事实，未固定时按字节数与默认时间给出。</summary>
    public InMemoryWorkspaceFiles SetFacts(string path, FileFacts facts)
    {
        lock (_gate)
        {
            _facts[Full(path)] = facts;
        }

        return this;
    }

    /// <summary>读取文本，按 BOM 识别编码。</summary>
    public string ReadText(string path)
    {
        using StreamReader reader = new(
            new MemoryStream(ReadBytes(path)),
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);

        return reader.ReadToEnd();
    }

    /// <inheritdoc/>
    public bool FileExists(string path)
    {
        lock (_gate)
        {
            return _files.ContainsKey(Full(path));
        }
    }

    /// <inheritdoc/>
    public bool DirectoryExists(string path)
    {
        lock (_gate)
        {
            return _directories.Contains(Full(path));
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Entries(string directory) => Children(directory, _files.Keys, _directories);

    /// <inheritdoc/>
    public IReadOnlyList<string> Files(string directory) => Children(directory, _files.Keys, []);

    /// <inheritdoc/>
    public IReadOnlyList<string> Directories(string directory) => Children(directory, [], _directories);

    /// <inheritdoc/>
    public FileFacts Inspect(string file)
    {
        string path = Full(file);
        lock (_gate)
        {
            if (_facts.TryGetValue(path, out FileFacts? facts))
            {
                return facts;
            }

            if (!_files.TryGetValue(path, out byte[]? bytes))
            {
                throw new FileNotFoundException($"文件不存在：{file}", file);
            }

            return new FileFacts(
                bytes.Length,
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch,
                FileAttributes.Archive);
        }
    }

    /// <inheritdoc/>
    public Stream OpenRead(string file) => new MemoryStream(ReadBytes(file), writable: false);

    /// <inheritdoc/>
    public byte[] ReadBytes(string file)
    {
        string path = Full(file);
        lock (_gate)
        {
            return _files.TryGetValue(path, out byte[]? bytes)
                ? [.. bytes]
                : throw new FileNotFoundException($"文件不存在：{file}", file);
        }
    }

    /// <inheritdoc/>
    public void WriteText(string path, string text, Encoding encoding)
    {
        string file = Full(path);
        lock (_gate)
        {
            if (_directories.Contains(file))
            {
                throw new IOException($"目标是目录：{path}");
            }

            RequireParent(file);
            _facts.Remove(file);
            _files[file] = [.. encoding.GetPreamble(), .. encoding.GetBytes(text)];
        }
    }

    /// <inheritdoc/>
    public void CreateDirectory(string path)
    {
        string directory = Full(path);
        lock (_gate)
        {
            if (_files.ContainsKey(directory))
            {
                throw new IOException($"目标是文件：{path}");
            }

            AddDirectoryCore(directory);
        }
    }

    /// <inheritdoc/>
    public void DeleteFile(string path)
    {
        string file = Full(path);
        lock (_gate)
        {
            _files.Remove(file);
            _facts.Remove(file);
        }
    }

    /// <inheritdoc/>
    public void DeleteDirectory(string path, bool recursive)
    {
        string directory = Full(path);
        lock (_gate)
        {
            if (!_directories.Contains(directory))
            {
                throw new DirectoryNotFoundException($"目录不存在：{path}");
            }

            List<string> files = [.. _files.Keys.Where(file => IsWithin(file, directory))];
            List<string> directories = [.. _directories.Where(item => IsWithin(item, directory))];
            if (!recursive && files.Count + directories.Count > 1)
            {
                throw new IOException($"目录不为空：{path}");
            }

            foreach (string file in files)
            {
                _files.Remove(file);
                _facts.Remove(file);
            }

            foreach (string item in directories)
            {
                _directories.Remove(item);
            }
        }
    }

    /// <inheritdoc/>
    public void MoveFile(string source, string target)
    {
        string from = Full(source);
        string to = Full(target);
        lock (_gate)
        {
            if (!_files.TryGetValue(from, out byte[]? bytes) || _files.ContainsKey(to) || _directories.Contains(to))
            {
                throw new IOException($"无法移动文件：{source} -> {target}");
            }

            RequireParent(to);
            _files[to] = bytes;
            _files.Remove(from);
            if (_facts.Remove(from, out FileFacts? facts))
            {
                _facts[to] = facts;
            }
        }
    }

    /// <inheritdoc/>
    public void MoveDirectory(string source, string target)
    {
        string from = Full(source);
        string to = Full(target);
        lock (_gate)
        {
            if (!_directories.Contains(from) || _directories.Contains(to) || _files.ContainsKey(to))
            {
                throw new IOException($"无法移动目录：{source} -> {target}");
            }

            RequireParent(to);
            AddDirectoryCore(to);
            foreach (string directory in _directories.Where(item => IsWithin(item, from)).ToArray())
            {
                _directories.Remove(directory);
                _directories.Add(to + directory[from.Length..]);
            }

            foreach (string file in _files.Keys.Where(item => IsWithin(item, from)).ToArray())
            {
                byte[] bytes = _files[file];
                string moved = to + file[from.Length..];
                _files.Remove(file);
                _files[moved] = bytes;
                if (_facts.Remove(file, out FileFacts? facts))
                {
                    _facts[moved] = facts;
                }
            }
        }
    }

    /// <inheritdoc/>
    public bool IsReparsePoint(string directory) => false;

    private static string Full(string path) => Path.GetFullPath(path);

    /// <summary>路径是否在目录内，目录本身与后代都算在内。</summary>
    private static bool IsWithin(string path, string directory)
    {
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return Comparer.Equals(path, directory)
            || path.StartsWith(directory + Path.DirectorySeparatorChar, comparison);
    }

    /// <summary>目录的直接子项，文件与目录按完整路径返回。</summary>
    private IReadOnlyList<string> Children(
        string directory,
        IEnumerable<string> files,
        IEnumerable<string> directories)
    {
        string root = Full(directory);
        lock (_gate)
        {
            if (!_directories.Contains(root))
            {
                throw new DirectoryNotFoundException($"目录不存在：{directory}");
            }

            return
            [
                .. files.Where(file => Comparer.Equals(Path.GetDirectoryName(file), root)),
                .. directories.Where(item => Comparer.Equals(Path.GetDirectoryName(item), root)),
            ];
        }
    }

    private void AddDirectoryCore(string directory)
    {
        string? current = directory;
        while (!string.IsNullOrEmpty(current))
        {
            _directories.Add(current);
            string? parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || Comparer.Equals(parent, current))
            {
                break;
            }

            current = parent;
        }
    }

    /// <summary>要求路径的父目录已存在，父目录缺失时失败。调用方持有锁。</summary>
    private void RequireParent(string path)
    {
        string? parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent) && !_directories.Contains(parent))
        {
            throw new DirectoryNotFoundException($"目录不存在：{parent}");
        }
    }

    private void AddParentDirectories(string file)
    {
        string? directory = Path.GetDirectoryName(file);
        if (!string.IsNullOrEmpty(directory))
        {
            AddDirectoryCore(directory);
        }
    }
}