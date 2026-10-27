using Kuroe.Process;
using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Command;

/// <summary>命令模板工具载体：按配置定义声明一个可调用的函数，调用体在执行目录执行命令并把输出写回模型。</summary>
public sealed class CommandTool : ITool
{
    /// <summary>用单工具绑定建立载体。</summary>
    public CommandTool(CommandToolDefinition definition, ToolBinding context)
        : this(definition, context, new ProcessRunner())
    {
    }

    /// <summary>用执行绑定的根目录建立载体，供测试与无变量工具使用。</summary>
    public CommandTool(CommandToolDefinition definition, string baseDirectory)
        : this(definition, baseDirectory, new ProcessRunner())
    {
    }

    /// <summary>用给定的进程执行建立载体，路径由工具自行读取。</summary>
    /// <param name="definition">命令定义。</param>
    /// <param name="context">工具绑定视图。</param>
    /// <param name="process">命令进程的执行边界。</param>
    public CommandTool(
        CommandToolDefinition definition,
        ToolBinding context,
        IProcessRunner process)
    {
        CommandToolRunner runner = new(definition, context, process);
        Functions = [definition.Definition.Bind(runner.Execute, runner.Paths)];
    }

    /// <summary>用执行绑定的根目录建立载体，供测试与无变量工具使用。</summary>
    public CommandTool(
        CommandToolDefinition definition,
        string baseDirectory,
        IProcessRunner process)
        : this(definition, ToolBinding.Root(baseDirectory), process)
    {
    }

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }
}
