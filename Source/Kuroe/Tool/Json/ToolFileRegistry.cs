namespace Kuroe.Tool.Json;

/// <summary>工具文件形状的注册表：文件名到实现的唯一入口。注册在装配前完成，之后只读。注册必须显式，不做程序集扫描。</summary>
public sealed class ToolFileRegistry
{
    private readonly Dictionary<string, IToolFile> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IToolFile> _ordered = [];

    /// <summary>注册一个文件形状，同名覆盖。同名注册在原位替换，次序不变。</summary>
    public void Register(IToolFile file)
    {
        if (_byName.TryGetValue(file.FileName, out IToolFile? existing))
        {
            _ordered[_ordered.IndexOf(existing)] = file;
        }
        else
        {
            _ordered.Add(file);
        }

        _byName[file.FileName] = file;
    }

    /// <summary>已注册的文件形状，按注册序。</summary>
    public IReadOnlyList<IToolFile> Files => _ordered;
}