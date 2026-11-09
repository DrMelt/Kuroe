using ErrorOr;
using Kuroe.Tests.Support;
using Kuroe.Workflows.Flows.Files;
using Xunit;

namespace Kuroe.Tests.Workflows.Flows.Files;

/// <summary>hostStartInputs.json 的读取：文件不存在或空对象没有表项，结构、名字与值的问题一次给全。</summary>
public sealed class HostStartInputsStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));

    public HostStartInputsStoreTests() => Directory.CreateDirectory(_root);

    private string FilePath => Path.Combine(_root, "hostStartInputs.json");

    /// <summary>文件不存在时没有表项。</summary>
    [Fact]
    public void A_missing_file_has_no_entries() => Assert.Empty(Load());

    /// <summary>空对象没有表项。</summary>
    [Fact]
    public void An_empty_object_has_no_entries() => Assert.Empty(Load("{}"));

    /// <summary>表项按流程名与开始端口名读出。</summary>
    [Fact]
    public void Entries_are_read_by_flow_and_port()
    {
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> entries = Load("""
            {
              "HostStartInputs": {
                "默认": { "启动路径": "WORKSPACE_PATH", "模型": "MODEL" }
              }
            }
            """);

        IReadOnlyDictionary<string, string> entry = Assert.Single(entries).Value;
        Assert.Equal("WORKSPACE_PATH", entry["启动路径"]);
        Assert.Equal("MODEL", entry["模型"]);
    }

    /// <summary>表为 null 时没有表项。</summary>
    [Fact]
    public void A_null_section_has_no_entries() => Assert.Empty(Load("""{ "HostStartInputs": null }"""));

    /// <summary>非法 JSON 报错时带上文件路径。</summary>
    [Fact]
    public void Invalid_json_reports_the_file()
    {
        ErrorOr<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> loaded = TryLoad("{");

        Assert.True(loaded.IsError);
        Assert.Contains("hostStartInputs.json", loaded.FirstError.Description, StringComparison.Ordinal);
    }

    /// <summary>根节点必须是对象。</summary>
    [Fact]
    public void A_root_that_is_not_an_object_is_rejected() =>
        Assert.Contains("JSON 对象", Errors("[]").Single().Description, StringComparison.Ordinal);

    /// <summary>顶层只认 HostStartInputs。</summary>
    [Fact]
    public void An_unknown_section_is_rejected() =>
        Assert.Contains("不认识 Flows", Errors("""{ "Flows": {} }""").Single().Description, StringComparison.Ordinal);

    /// <summary>HostStartInputs 必须是流程名到端口表的映射。</summary>
    [Fact]
    public void A_section_that_is_not_an_object_is_rejected() =>
        Assert.Contains("必须是流程名到端口表的映射",
            Errors("""{ "HostStartInputs": [] }""").Single().Description, StringComparison.Ordinal);

    /// <summary>流程表必须是开始端口到宿主输入名的映射。</summary>
    [Fact]
    public void A_flow_that_is_not_an_object_is_rejected() =>
        Assert.Contains("必须是开始端口到宿主输入名的映射",
            Errors("""{ "HostStartInputs": { "默认": [] } }""").Single().Description, StringComparison.Ordinal);

    /// <summary>流程名与端口名不能为空或重复，错误一次给全。</summary>
    [Fact]
    public void Flow_and_port_names_must_be_present_and_unique()
    {
        IReadOnlyList<Error> errors = Errors("""
            {
              "HostStartInputs": {
                " ": {},
                "默认": { "": "WORKSPACE_PATH", "启动路径": "MODEL", "启动路径 ": "MODEL" }
              }
            }
            """);

        Assert.Equal(3, errors.Count);
        Assert.Contains(errors, error => error.Description.Contains("流程名不能为空", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Description.Contains("空的开始端口名", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Description.Contains("端口 启动路径 重复", StringComparison.Ordinal));
    }

    /// <summary>宿主输入名必须是非空字符串。</summary>
    [Fact]
    public void A_port_value_must_be_a_non_blank_string()
    {
        IReadOnlyList<Error> errors = Errors("""
            {
              "HostStartInputs": {
                "默认": { "启动": 1, "模型": " " }
              }
            }
            """);

        Assert.Equal(2, errors.Count);
        Assert.All(errors, error => Assert.Contains("必须给非空的宿主输入名", error.Description, StringComparison.Ordinal));
    }

    private IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Load(string? json = null)
    {
        if (json is not null)
        {
            File.WriteAllText(FilePath, json);
        }

        return new HostStartInputsStore(FilePath).Load().ThrowIfError();
    }

    private ErrorOr<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> TryLoad(string json)
    {
        File.WriteAllText(FilePath, json);

        return new HostStartInputsStore(FilePath).Load();
    }

    private List<Error> Errors(string json) => TryLoad(json).ErrorsOrEmptyList;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // 临时目录可能已被清理或占用，收尾不再上报
        }
    }
}