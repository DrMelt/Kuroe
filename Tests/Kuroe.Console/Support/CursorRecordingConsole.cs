using Spectre.Console;
using Spectre.Console.Rendering;
using Spectre.Console.Testing;

namespace Kuroe.Console.Tests.Support;

/// <summary>记录光标移动的测试控制台，输入与输出委托给 TestConsole。</summary>
internal sealed class CursorRecordingConsole : IAnsiConsole
{
    private readonly TestConsole _inner;

    public CursorRecordingConsole(TestConsole inner)
    {
        _inner = inner;
        Cursor = new RecordingCursor(Moves);
    }

    public List<(CursorDirection Direction, int Steps)> Moves { get; } = [];

    public IAnsiConsoleCursor Cursor { get; }

    public Profile Profile => _inner.Profile;

    public IAnsiConsoleInput Input => _inner.Input;

    public IExclusivityMode ExclusivityMode => _inner.ExclusivityMode;

    public RenderPipeline Pipeline => _inner.Pipeline;

    public void Clear(bool home) => _inner.Clear(home);

    public void Write(IRenderable renderable) => _inner.Write(renderable);

    public void WriteAnsi(Action<AnsiWriter> action) => _inner.WriteAnsi(action);

    /// <summary>只记录相对移动，绝对定位与显隐不在断言范围内。</summary>
    private sealed class RecordingCursor(List<(CursorDirection Direction, int Steps)> moves) : IAnsiConsoleCursor
    {
        public void Move(CursorDirection direction, int steps) => moves.Add((direction, steps));

        public void SetPosition(int column, int line)
        {
            // 输入回显不使用绝对定位。
        }

        public void Show(bool show)
        {
            // 输入回显不改变光标显隐。
        }
    }
}