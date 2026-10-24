using Spectre.Console;
using Spectre.Console.Rendering;

namespace Kuroe.Console.Views;

/// <summary>输入表的一项：标签、说明与是否凭据。</summary>
internal sealed record FormField(string Label, string? Description = null, bool Secret = false);

/// <summary>输入表的只读上下文行，不参与输入。</summary>
internal sealed record FormInfo(string Label, string Value);

/// <summary>一次输入会话：每次选择前重印全部项目与已填内容，全部填完后重印完整结果。</summary>
internal sealed class InputForm(
    Terminal terminal,
    string title,
    IReadOnlyList<FormField> fields,
    IReadOnlyList<FormInfo>? info = null)
{
    private const int SubmitIndex = -2;
    private const int CancelIndex = -1;

    private static readonly Item Submit = new("√ 提交", SubmitIndex);
    private static readonly Item Cancel = new("← 取消", CancelIndex);

    private readonly string[] _values = [.. fields.Select(_ => string.Empty)];

    /// <summary>逐项选择填写或改写，选择提交时返回全部输入，取消时返回 null。</summary>
    public IReadOnlyList<string>? Run()
    {
        if (fields.Count == 0)
        {
            Print();

            return _values;
        }

        while (true)
        {
            Print();

            Item picked = terminal.Choose("选择填写项", Items(), item => item.Label, Cancel);
            if (picked.Index == CancelIndex)
            {
                return null;
            }

            if (picked.Index == SubmitIndex)
            {
                Print();

                return _values;
            }

            if (!Fill(picked.Index))
            {
                return null;
            }
        }
    }

    /// <summary>菜单项：未填字段在前，全部填写后加入提交，已填字段再后，取消在末尾。</summary>
    private List<Item> Items()
    {
        List<Item> items = [];
        AddFields(items, filled: false);
        if (_values.All(value => value.Length > 0))
        {
            items.Add(Submit);
        }

        AddFields(items, filled: true);
        items.Add(Cancel);

        return items;
    }

    /// <summary>按填写状态加入字段项，保持字段声明序。</summary>
    private void AddFields(List<Item> items, bool filled)
    {
        for (int index = 0; index < fields.Count; index++)
        {
            if (Filled(index) == filled)
            {
                items.Add(FieldItem(index));
            }
        }
    }

    private Item FieldItem(int index) =>
        new($"{fields[index].Label} · {(Filled(index) ? "已填写" : "未填写")}", index);

    private bool Filled(int index) => _values[index].Length > 0;

    /// <summary>读取一项；取消返回 false。</summary>
    private bool Fill(int index)
    {
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
            return false;
        }

        _values[index] = text;

        return true;
    }

    /// <summary>重印整表与已填内容。</summary>
    private void Print()
    {
        IReadOnlyList<FormInfo> context = info ?? [];
        List<(string Label, IRenderable Value)> rows =
        [
            .. context.Select(item => (item.Label, (IRenderable)new Text(item.Value))),
            .. fields.Select((field, index) => Row(field, _values[index])),
        ];
        if (rows.Count == 0)
        {
            return;
        }

        terminal.Write(Blocks.Section(BoxKind.View, title, Blocks.Fields([.. rows])));
    }

    /// <summary>一个项目在输入表里的行：标签与已填内容。</summary>
    private static (string Label, IRenderable Value) Row(FormField field, string text)
    {
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

        return (field.Label, value);
    }

    /// <summary>一个菜单项：标签与字段序号，或提交与取消的标记。</summary>
    private sealed record Item(string Label, int Index);
}