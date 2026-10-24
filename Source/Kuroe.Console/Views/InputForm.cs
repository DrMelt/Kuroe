using Spectre.Console;
using Spectre.Console.Rendering;

namespace Kuroe.Console.Views;

/// <summary>输入表的一项：标签、说明与是否凭据。</summary>
internal sealed record FormField(string Label, string? Description = null, bool Secret = false);

/// <summary>输入表的只读上下文行，不参与输入。</summary>
internal sealed record FormInfo(string Label, string Value);

/// <summary>一次输入会话：每次读取前重印全部项目与已填内容，全部读完后重印完整结果。</summary>
internal sealed class InputForm(
    Terminal terminal,
    string title,
    IReadOnlyList<FormField> fields,
    IReadOnlyList<FormInfo>? info = null)
{
    private readonly string[] _values = [.. fields.Select(_ => string.Empty)];

    /// <summary>按字段顺序读取全部项目，任一回答取消时返回 null。</summary>
    public IReadOnlyList<string>? Run()
    {
        for (int index = 0; index < fields.Count; index++)
        {
            Print(index);

            FormField field = fields[index];
            if (field.Description is { Length: > 0 } description)
            {
                terminal.Hint($"{field.Label}：{description}");
            }

            string? text = field.Secret
                ? terminal.AskSecret($"{field.Label}：")
                : terminal.Ask($"{field.Label}：");
            if (text is null)
            {
                return null;
            }

            _values[index] = text;
        }

        Print(-1);

        return _values;
    }

    /// <summary>重印整表。current 是正在输入的项目序号，-1 表示全部读完。</summary>
    private void Print(int current)
    {
        IReadOnlyList<FormInfo> context = info ?? [];
        List<(string Label, IRenderable Value)> rows =
        [
            .. context.Select(item => (item.Label, (IRenderable)new Text(item.Value))),
            .. fields.Select((field, index) => Row(field, _values[index], index == current)),
        ];
        if (rows.Count == 0)
        {
            return;
        }

        terminal.Write(Blocks.Section(BoxKind.View, title, Blocks.Fields([.. rows])));
    }

    /// <summary>一个项目在输入表里的行：标签、已填内容与当前标记。</summary>
    private static (string Label, IRenderable Value) Row(FormField field, string text, bool current)
    {
        string label = current ? $"{field.Label} · 输入中" : field.Label;
        IRenderable value;
        if (field.Secret && text.Length > 0)
        {
            value = new Text("已填写", Styles.Success);
        }
        else if (text.Length > 0)
        {
            value = new Text(text);
        }
        else
        {
            value = new Text("未填写", Styles.Hint);
        }

        return (label, value);
    }
}