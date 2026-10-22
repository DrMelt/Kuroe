using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Command;

/// <summary>命令模板工具载体：按配置定义声明一个可调用的函数，调用体在工作目录执行命令并把输出写回模型。</summary>
public sealed class CommandTool : ITool
{
    /// <summary>用工作目录与默认进程执行建立载体。</summary>
    public CommandTool(CommandToolDefinition definition, string baseDirectory)
        : this(definition, baseDirectory, new CommandProcessRunner())
    {
    }

    /// <summary>用给定的进程执行建立载体。</summary>
    /// <param name="definition">命令定义。</param>
    /// <param name="baseDirectory">工作目录根，命令执行目录相对它解析。</param>
    /// <param name="process">命令进程的执行边界。</param>
    public CommandTool(
        CommandToolDefinition definition,
        string baseDirectory,
        ICommandProcessRunner process)
    {
        CommandToolRunner runner = new(definition, baseDirectory, process);
        Functions = [definition.Definition.Bind(runner.Execute)];
    }

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }
}
