using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.Shared.RuntimeGraph.Tasks;

/// <summary>提交任务时给出的一个开始输入：开始端口名与文本。</summary>
public readonly record struct StartInput(PortName Port, string Text);

/// <summary>任务的开始输入表：按开始端口给出的文本，只覆盖不带固定值的开始端口。
/// 提交校验保证端口已声明、不重复且文本非空。</summary>
public sealed class StartInputs
{
    private readonly List<StartInput> _values;

    /// <summary>按提交顺序建立输入表，每项文本去掉两端空白。</summary>
    public StartInputs(IReadOnlyList<StartInput> values) =>
        _values = [.. values.Select(value => value with { Text = value.Text.Trim() })];

    /// <summary>按提交顺序排列的输入，与开始端口声明的顺序一致。</summary>
    public IReadOnlyList<StartInput> Values => _values;

    /// <summary>只含一个端口与文本的输入表。</summary>
    public static StartInputs Single(PortName port, string text) => new([new StartInput(port, text)]);

    /// <summary>按端口取输入文本，未给出时为空。</summary>
    public string? Text(PortName port)
    {
        foreach (StartInput value in _values)
        {
            if (value.Port == port)
            {
                return value.Text;
            }
        }

        return null;
    }
}