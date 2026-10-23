using System.CommandLine;
using Kuroe.Cli;

CliApplication application = new(
    Console.Out,
    Console.Error,
    directory => CliSession.Create(directory));

RootCommand root = CliCommandLine.Build(application);
ParseResult parsed = root.Parse(args);

// 解析失败时帮助写 stderr，stdout 只承载命令结果
InvocationConfiguration invocation = parsed.Errors.Count > 0
    ? new InvocationConfiguration { Output = Console.Error }
    : new InvocationConfiguration();

return await parsed.InvokeAsync(invocation, CancellationToken.None);