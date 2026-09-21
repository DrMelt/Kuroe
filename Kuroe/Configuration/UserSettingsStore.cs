using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Kuroe.Configuration;

/// <summary>用户层配置文件，内容是用户设置过的项构成的节点树。</summary>
public sealed class UserSettingsStore
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

    /// <summary>读取路径上的标量文本，路径不存在或指向对象时返回 null。</summary>
    public static string? TryGetValue(JsonObject root, string path) =>
        Walk(root, path) is JsonValue value ? value.ToString() : null;

    /// <summary>按路径写入节点，中间节点不存在时创建。</summary>
    public static void SetValue(JsonObject root, string path, JsonNode value)
    {
        string[] segments = Segments(path);
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
    public static void RemoveValue(JsonObject root, string path) => Remove(root, Segments(path), 0);

    public static byte[] Serialize(JsonObject root) => JsonSerializer.SerializeToUtf8Bytes(root, WriteOptions);

    /// <summary>读取路径上的标量文本，路径不存在或指向对象时返回 null。</summary>
    public string? TryGetValue(string path) => TryGetValue(_root, path);

    /// <summary>节点树副本，修改后交给 Commit。</summary>
    public JsonObject Snapshot() => (JsonObject)_root.DeepClone();

    /// <summary>写盘并接受该节点树。</summary>
    public void Commit(JsonObject root)
    {
        root[VersionKey] = CurrentVersion;
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);

        string temporary = _file + ".tmp";
        File.WriteAllBytes(temporary, Serialize(root));
        File.Move(temporary, _file, overwrite: true);

        _root = root;
    }

    private static string[] Segments(string path)
    {
        string[] segments = path.Split(':');
        if (segments.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException($"配置路径“{path}”无效，应形如 Agent:Model。");
        }

        if (segments.Length == 1 && segments[0].Equals(VersionKey, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{VersionKey} 是用户层文件的结构版本，不能作为设置项。");
        }

        return segments;
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

    private static int ReadVersion(JsonObject root)
    {
        if (root[VersionKey] is not JsonValue value)
        {
            return CurrentVersion;
        }

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