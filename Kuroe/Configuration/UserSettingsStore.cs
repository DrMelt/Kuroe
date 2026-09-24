using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kuroe.Storage;

namespace Kuroe.Configuration;

/// <summary>用户层配置文件，内容是用户设置过的项构成的节点树。</summary>
sealed class UserSettingsStore
{
    /// <summary>用户层结构版本。</summary>
    private const int CurrentVersion = 1;

    private const string VersionKey = "Version";

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonDocumentOptions ReadOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private readonly string _file;
    private JsonObject _root;

    public UserSettingsStore(string file)
    {
        _file = file;
        _root = Load();
    }

    /// <summary>按路径写入节点，中间节点不存在时创建。</summary>
    internal static void SetValue(JsonObject root, string path, JsonNode value)
    {
        string[] segments = path.Split(':');
        JsonObject current = root;
        for (int i = 0; i < segments.Length - 1; i++)
        {
            string segment = segments[i];
            if (current[segment] is null)
            {
                JsonObject child = [];
                current[segment] = child;
                current = child;
            }
            else if (current[segment] is JsonObject existing)
            {
                current = existing;
            }
            else
            {
                throw new InvalidOperationException($"{string.Join(':', segments[..(i + 1)])} 已存在且不是对象。");
            }
        }

        current[segments[^1]] = value;
    }

    /// <summary>按路径删除节点，父节点清空后一并删除。</summary>
    internal static void RemoveValue(JsonObject root, string path) => Remove(root, path.Split(':'), 0);

    private static string Serialize(JsonObject root) => root.ToJsonString(WriteOptions);

    /// <summary>读取路径上的标量文本，路径不存在或指向对象时返回 null。</summary>
    public string? TryGetValue(string path) => Walk(_root, path) is JsonValue value ? value.ToString() : null;

    /// <summary>节点树副本，修改后交给 Commit。</summary>
    public JsonObject Snapshot() => (JsonObject)_root.DeepClone();

    /// <summary>用规范化后的树替换当前树，不落盘。</summary>
    internal void Adopt(JsonObject root) => _root = root;

    /// <summary>写盘并接受该节点树。</summary>
    public void Commit(JsonObject root)
    {
        root[VersionKey] = CurrentVersion;
        AtomicFile.WriteText(_file, Serialize(root));

        _root = root;
    }

    private static JsonNode? Walk(JsonObject root, string path)
    {
        JsonNode? node = root;
        foreach (string segment in path.Split(':'))
        {
            if (node is not JsonObject current)
            {
                return null;
            }

            node = current[segment];
            if (node is null)
            {
                return null;
            }
        }

        return node;
    }

    private static bool Remove(JsonObject node, string[] segments, int index)
    {
        string segment = segments[index];
        if (node[segment] is not JsonNode child)
        {
            return false;
        }

        if (index == segments.Length - 1)
        {
            node.Remove(segment);
            return true;
        }

        if (child is not JsonObject childObject)
        {
            return false;
        }

        if (Remove(childObject, segments, index + 1) && childObject.Count == 0)
        {
            node.Remove(segment);
        }

        return true;
    }

    /// <summary>取结构版本，同层的大小写变体只保留规范键。</summary>
    private static int ReadVersion(JsonObject root)
    {
        int version = CurrentVersion;

        foreach (string key in VersionKeys(root))
        {
            if (root[key] is JsonValue value)
            {
                version = Math.Max(version, ReadInteger(value));
            }

            root.Remove(key);
        }

        return version;
    }

    /// <summary>同层中与规范版本键忽略大小写同名的键。</summary>
    private static string[] VersionKeys(JsonObject root) =>
        [.. root.Select(pair => pair.Key).Where(key => key.Equals(VersionKey, StringComparison.OrdinalIgnoreCase))];

    private static int ReadInteger(JsonValue value)
    {
        try
        {
            return value.GetValue<int>();
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            throw new InvalidOperationException($"{VersionKey} 必须是整数。", ex);
        }
    }

    private JsonObject Load()
    {
        if (!File.Exists(_file))
        {
            return [];
        }

        string text = File.ReadAllText(_file);
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text, nodeOptions: null, ReadOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{_file} 不是合法 JSON：{ex.Message}", ex);
        }

        if (node is not JsonObject root)
        {
            throw new InvalidOperationException($"{_file} 的根节点必须是对象。");
        }

        int version = ReadVersion(root);
        if (version > CurrentVersion)
        {
            throw new InvalidOperationException(
                $"{_file} 的结构版本为 {version}，高于本程序支持的 {CurrentVersion}，请升级 Kuroe。");
        }

        root[VersionKey] = CurrentVersion;
        return root;
    }
}
