using Spectre.Console;
using Spectre.Console.Rendering;

namespace Kuroe.Console.Views;

/// <summary>带框的键盘选择：框内列出选项，当前项高亮，Enter 返回选中项，Esc 返回取消项。</summary>
internal sealed class MenuPrompt<T>(string title, IReadOnlyList<T> choices, Func<T, string> label, T cancel)
    : IPrompt<T>
    where T : notnull
{
    /// <summary>框内一次显示的选项数。</summary>
    private const int PageSize = 12;

    public T Show(IAnsiConsole console)
    {
        int selected = 0;
        T result = cancel;

        console.Live(Build(selected)).Start(context =>
        {
            context.Refresh();

            while (true)
            {
                ConsoleKeyInfo? pressed = console.Input.ReadKey(true);
                if (pressed is null)
                {
                    return;
                }

                switch (pressed.Value.Key)
                {
                    case ConsoleKey.UpArrow:
                        selected = Math.Max(0, selected - 1);
                        break;

                    case ConsoleKey.DownArrow:
                        selected = Math.Min(choices.Count - 1, selected + 1);
                        break;

                    case ConsoleKey.PageUp:
                        selected = Math.Max(0, selected - PageSize);
                        break;

                    case ConsoleKey.PageDown:
                        selected = Math.Min(choices.Count - 1, selected + PageSize);
                        break;

                    case ConsoleKey.Home:
                        selected = 0;
                        break;

                    case ConsoleKey.End:
                        selected = choices.Count - 1;
                        break;

                    case ConsoleKey.Enter:
                        result = choices[selected];

                        return;

                    case ConsoleKey.Escape:
                        result = cancel;

                        return;

                    default:
                        continue;
                }

                context.UpdateTarget(Build(selected));
            }
        });

        return result;
    }

    public Task<T> ShowAsync(IAnsiConsole console, CancellationToken cancellationToken) =>
        Task.FromResult(Show(console));

    private Panel Build(int selected)
    {
        (int start, int count) = Window(selected);
        List<IRenderable> rows = [];

        for (int i = start; i < start + count; i++)
        {
            bool current = i == selected;
            Paragraph row = new();
            row.Append(current ? "> " : "  ", current ? Styles.Key : Style.Plain);
            row.Append(label(choices[i]), current ? Styles.Key : Style.Plain);
            rows.Add(row);
        }

        return Blocks.Section(BoxKind.Menu, title, new Rows(rows));
    }

    /// <summary>选项超出框内页长时，窗口跟随当前项移动。</summary>
    private (int Start, int Count) Window(int selected)
    {
        if (choices.Count <= PageSize)
        {
            return (0, choices.Count);
        }

        int start = Math.Clamp(selected - PageSize / 2, 0, choices.Count - PageSize);

        return (start, PageSize);
    }
}