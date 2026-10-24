using System.CommandLine;
using Kuroe.Cli.Commands;

namespace Kuroe.Cli;

/// <summary>CLI 命令树。全局工作目录选项递归到所有子命令。</summary>
internal static class CliCommandLine
{
    /// <summary>构建根命令。</summary>
    public static RootCommand Build(CliApplication application)
    {
        RootCommand root = new("Kuroe 命令行：一次运行完成一个操作后退出。");
        Option<string?> workDirectory = new("--work-directory", "-d")
        {
            Description = "工作目录，未指定时用进程当前目录",
            Recursive = true,
        };
        root.Options.Add(workDirectory);

        root.Subcommands.Add(Run(application, workDirectory));
        root.Subcommands.Add(Flow(application, workDirectory));
        root.Subcommands.Add(Model(application, workDirectory));
        root.Subcommands.Add(Provider(application, workDirectory));
        root.Subcommands.Add(Catalog(application, workDirectory));
        root.Subcommands.Add(Config(application, workDirectory));

        return root;
    }

    private static Command Run(CliApplication application, Option<string?> workDirectory)
    {
        Command command = new("run", "提交任务并在本进程内驱动到结束。流程不能包含等待外部输入的节点。");
        Option<string> flow = new("--flow", "-f")
        {
            Description = "流程名",
            Required = true,
        };
        Option<string?> title = new("--title", "-t") { Description = "任务标题，未指定时取目标首行" };
        Argument<string[]> goal = new("目标")
        {
            Description = "提交给流程的目标",
            Arity = ArgumentArity.OneOrMore,
        };
        const string unsupported = "run 不接受 --answer 或 --approve，流程不能包含等待外部输入的节点。";
        Option<string?> answer = new("--answer", "-a")
        {
            Description = "run 不支持，保留以给出明确错误",
            Arity = ArgumentArity.ExactlyOne,
            Hidden = true,
        };
        answer.Validators.Add(result =>
        {
            if (result.Tokens.Count > 0)
            {
                result.AddError(unsupported);
            }
        });
        Option<string?> approve = new("--approve", "-y")
        {
            Description = "run 不支持，保留以给出明确错误",
            Arity = ArgumentArity.ExactlyOne,
            Hidden = true,
        };
        approve.Validators.Add(result =>
        {
            if (result.Tokens.Count > 0)
            {
                result.AddError(unsupported);
            }
        });

        command.Options.Add(flow);
        command.Options.Add(title);
        command.Options.Add(answer);
        command.Options.Add(approve);
        command.Arguments.Add(goal);
        command.SetAction(parseResult => application.RunAsync(
            parseResult.GetValue(workDirectory),
            parseResult.GetValue(flow) ?? string.Empty,
            parseResult.GetValue(title),
            parseResult.GetValue(goal) ?? []));

        return command;
    }

    private static Command Flow(CliApplication application, Option<string?> workDirectory)
    {
        Command group = new("flow", "查看与导入流程。");

        Command list = new("list", "列出流程与规模。");
        list.SetAction(parseResult => application.ExecuteAsync(
            parseResult.GetValue(workDirectory),
            (session, output) => new FlowCommands(session, output).List()));

        Command show = new("show", "打印流程字段与节点树。");
        Argument<string> name = new("流程") { Description = "流程名" };
        show.Arguments.Add(name);
        show.SetAction(parseResult => application.ExecuteAsync(
            parseResult.GetValue(workDirectory),
            (session, output) => new FlowCommands(session, output).Show(parseResult.GetValue(name) ?? string.Empty)));

        Command add = new("add", "导入流程与节点库，同名覆盖。");
        Argument<string> file = new("文件") { Description = "流程文件，相对工作目录解析" };
        add.Arguments.Add(file);
        add.SetAction(parseResult => application.ExecuteAsync(
            parseResult.GetValue(workDirectory),
            (session, output) => new FlowCommands(session, output).Add(parseResult.GetValue(file) ?? string.Empty)));

        group.Subcommands.Add(list);
        group.Subcommands.Add(show);
        group.Subcommands.Add(add);
        group.SetAction(parseResult => application.ExecuteAsync(
            parseResult.GetValue(workDirectory),
            (session, output) => new FlowCommands(session, output).List()));

        return group;
    }

    private static Command Model(CliApplication application, Option<string?> workDirectory)
    {
        Command group = new("model", "查看与维护当前模型。");

        Command list = new("list", "列出已注册模型。");
        list.SetAction(parseResult => application.ExecuteAsync(
            parseResult.GetValue(workDirectory),
            (session, output) => new ModelCommands(session, output).List()));

        Command use = new("use", "切换模型，要求已注册。");
        Argument<string> model = new("模型") { Description = "模型名" };
        use.Arguments.Add(model);
        use.SetAction(parseResult => application.ExecuteAsync(
            parseResult.GetValue(workDirectory),
            (session, output) => new ModelCommands(session, output).Use(parseResult.GetValue(model) ?? string.Empty)));

        Command none = new("none", "取消模型选择。");
        none.SetAction(parseResult => application.ExecuteAsync(
            parseResult.GetValue(workDirectory),
            (session, output) => new ModelCommands(session, output).None()));

        Command add = new("add", "把模型注册到提供商。");
        Argument<string> addedModel = new("模型") { Description = "模型名" };
        Argument<string> provider = new("提供商") { Description = "提供商名" };
        add.Arguments.Add(addedModel);
        add.Arguments.Add(provider);
        add.SetAction(parseResult => application.ExecuteAsync(
            parseResult.GetValue(workDirectory),
            (session, output) => new ModelCommands(session, output).Add(
                parseResult.GetValue(addedModel) ?? string.Empty,
                parseResult.GetValue(provider) ?? string.Empty)));

        Command remove = new("rm", "注销模型，是当前模型时一并取消选择。");
        Argument<string> removedModel = new("模型") { Description = "模型名" };
        remove.Arguments.Add(removedModel);
        remove.SetAction(parseResult => application.ExecuteAsync(
            parseResult.GetValue(workDirectory),
            (session, output) => new ModelCommands(session, output).Remove(parseResult.GetValue(removedModel) ?? string.Empty)));

        group.Subcommands.Add(list);
        group.Subcommands.Add(use);
        group.Subcommands.Add(none);
        group.Subcommands.Add(add);
        group.Subcommands.Add(remove);
        group.SetAction(parseResult => application.ExecuteAsync(
            parseResult.GetValue(workDirectory),
            (session, output) => new ModelCommands(session, output).Current()));

        return group;
    }

    private static Command Provider(CliApplication application, Option<string?> workDirectory)
    {
        Command group = new("provider", "查看与维护提供商。");

        Command list = new("list", "列出提供商。");
        list.SetAction(parseResult => application.ExecuteAsync(
            parseResult.GetValue(workDirectory),
            (session, output) => new ProviderCommands(session, output).List()));

        Command add = new("add", "新增提供商。");
        Argument<string> name = new("名") { Description = "提供商名" };
        Argument<string> endpoint = new("端点") { Description = "接口地址" };
        Argument<string?> key = new("凭据")
        {
            Description = $"接口凭据，省略时读环境变量 {ProviderCommands.CredentialEnvironmentVariable}",
            Arity = ArgumentArity.ZeroOrOne,
        };
        add.Arguments.Add(name);
        add.Arguments.Add(endpoint);
        add.Arguments.Add(key);
        add.SetAction(parseResult => application.ExecuteAsync(
            parseResult.GetValue(workDirectory),
            (session, output) => new ProviderCommands(session, output).Add(
                parseResult.GetValue(name) ?? string.Empty,
                parseResult.GetValue(endpoint) ?? string.Empty,
                parseResult.GetValue(key))));

        Command replaceKey = new("key", "更换提供商凭据。");
        Argument<string> keyProvider = new("名") { Description = "提供商名" };
        Argument<string?> keyValue = new("凭据")
        {
            Description = $"接口凭据，省略时读环境变量 {ProviderCommands.CredentialEnvironmentVariable}",
            Arity = ArgumentArity.ZeroOrOne,
        };
        replaceKey.Arguments.Add(keyProvider);
        replaceKey.Arguments.Add(keyValue);
        replaceKey.SetAction(parseResult => application.ExecuteAsync(
            parseResult.GetValue(workDirectory),
            (session, output) => new ProviderCommands(session, output).Key(
                parseResult.GetValue(keyProvider) ?? string.Empty,
                parseResult.GetValue(keyValue))));

        Command remove = new("rm", "删除提供商，仍被模型引用时拒绝。");
        Argument<string> removed = new("名") { Description = "提供商名" };
        remove.Arguments.Add(removed);
        remove.SetAction(parseResult => application.ExecuteAsync(
            parseResult.GetValue(workDirectory),
            (session, output) => new ProviderCommands(session, output).Remove(parseResult.GetValue(removed) ?? string.Empty)));

        group.Subcommands.Add(list);
        group.Subcommands.Add(add);
        group.Subcommands.Add(replaceKey);
        group.Subcommands.Add(remove);
        group.SetAction(parseResult => application.ExecuteAsync(
            parseResult.GetValue(workDirectory),
            (session, output) => new ProviderCommands(session, output).List()));

        return group;
    }

    private static Command Catalog(CliApplication application, Option<string?> workDirectory)
    {
        Command group = new("catalog", "查看、导出与导入提供商与模型目录。");

        Command list = new("list", "列出提供商与模型。");
        list.SetAction(parseResult => application.ExecuteAsync(
            parseResult.GetValue(workDirectory),
            (session, output) => new CatalogCommands(session, output).List()));

        Command export = new("export", "导出目录，凭据替换为占位符。");
        Argument<string> exportedFile = new("文件") { Description = "导出文件，相对工作目录解析" };
        export.Arguments.Add(exportedFile);
        export.SetAction(parseResult => application.ExecuteAsync(
            parseResult.GetValue(workDirectory),
            (session, output) => new CatalogCommands(session, output).Export(parseResult.GetValue(exportedFile) ?? string.Empty)));

        Command import = new("import", "合并导入目录，已有条目不受影响。");
        Argument<string> importedFile = new("文件") { Description = "导入文件，相对工作目录解析" };
        import.Arguments.Add(importedFile);
        import.SetAction(parseResult => application.ExecuteAsync(
            parseResult.GetValue(workDirectory),
            (session, output) => new CatalogCommands(session, output).Import(parseResult.GetValue(importedFile) ?? string.Empty)));

        group.Subcommands.Add(list);
        group.Subcommands.Add(export);
        group.Subcommands.Add(import);
        group.SetAction(parseResult => application.ExecuteAsync(
            parseResult.GetValue(workDirectory),
            (session, output) => new CatalogCommands(session, output).List()));

        return group;
    }

    private static Command Config(CliApplication application, Option<string?> workDirectory)
    {
        Command group = new("config", "打印生效配置与用户层设置。");

        Command list = new("list", "打印生效配置与来源。");
        list.SetAction(parseResult => application.ExecuteAsync(
            parseResult.GetValue(workDirectory),
            (session, output) => new ConfigCommands(session, output).List()));

        Command set = new("set", "写入用户层设置并立即生效。");
        Argument<string> path = new("路径") { Description = "设置项路径，如 Runtime:LogLevel" };
        Argument<string[]> value = new("值")
        {
            Description = "设置值，按 JSON 字面量解析，不是合法 JSON 时按字符串写入",
            Arity = ArgumentArity.OneOrMore,
        };
        set.Arguments.Add(path);
        set.Arguments.Add(value);
        set.SetAction(parseResult => application.ExecuteAsync(
            parseResult.GetValue(workDirectory),
            (session, output) => new ConfigCommands(session, output).Set(
                parseResult.GetValue(path) ?? string.Empty,
                string.Join(' ', parseResult.GetValue(value) ?? []))));

        Command unset = new("unset", "删除用户层中的该项。");
        Argument<string> removedPath = new("路径") { Description = "设置项路径，如 Runtime:LogLevel" };
        unset.Arguments.Add(removedPath);
        unset.SetAction(parseResult => application.ExecuteAsync(
            parseResult.GetValue(workDirectory),
            (session, output) => new ConfigCommands(session, output).Unset(parseResult.GetValue(removedPath) ?? string.Empty)));

        group.Subcommands.Add(list);
        group.Subcommands.Add(set);
        group.Subcommands.Add(unset);
        group.SetAction(parseResult => application.ExecuteAsync(
            parseResult.GetValue(workDirectory),
            (session, output) => new ConfigCommands(session, output).List()));

        return group;
    }
}