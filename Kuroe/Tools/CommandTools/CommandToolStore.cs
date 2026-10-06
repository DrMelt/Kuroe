using System.Text.Json;
using ErrorOr;
using Kuroe.Shared.Executions.Tools;

namespace Kuroe.Tools.CommandTools;

/// <summary>命令工具定义文件的读写与装配。文件不存在时为空；条目不合法时一次给出全部错误。</summary>
internal static class CommandToolStore
{
    /// <summary>读取命令工具文件，文件不存在时得到空列表。workDirectory 用作相对执行目录的解析基准。</summary>
    public static ErrorOr<IReadOnlyList<CommandToolDefinition>> Load(string file, string workDirectory)
    {
        if (!File.Exists(file))
        {
            return new List<CommandToolDefinition>();
        }

        string text;
        try
        {
            text = File.ReadAllText(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [CommandToolErrors.Read(file, ex.Message)];
        }

        CommandToolFileDto? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize(text, CommandToolJson.Default.CommandToolFileDto);
        }
        catch (JsonException ex)
        {
            return [CommandToolErrors.Format($"{file}：{ex.Message}")];
        }

        if (parsed is null)
        {
            return [CommandToolErrors.Format($"{file} 的内容是 null。")];
        }

        return ToDefinitions(parsed, workDirectory);
    }

    /// <summary>装配定义并校验，全部非法项一次给出。</summary>
    private static ErrorOr<IReadOnlyList<CommandToolDefinition>> ToDefinitions(CommandToolFileDto file, string workDirectory)
    {
        List<Error> errors = [];
        List<CommandToolDefinition> definitions = [];
        HashSet<string> names = [];

        foreach (CommandToolDto dto in file.Tools ?? [])
        {
            if (dto is null)
            {
                errors.Add(CommandToolErrors.Invalid("(未命名)", "工具条目是 null。"));
                continue;
            }

            ErrorOr<CommandToolDefinition> converted = ToDefinition(dto, workDirectory);
            if (converted.IsError)
            {
                errors.AddRange(converted.ErrorsOrEmptyList);
                continue;
            }

            CommandToolDefinition definition = converted.Value;
            if (!names.Add(definition.Name.Value))
            {
                errors.Add(CommandToolErrors.Invalid(definition.Name.Value, "名字重复。"));
                continue;
            }

            CheckTemplate(definition, errors);
            definitions.Add(definition);
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        return definitions;
    }

    /// <summary>把文件形状装配成定义，参数的非法项在这层先给出，模板合法性单独校验。</summary>
    private static ErrorOr<CommandToolDefinition> ToDefinition(CommandToolDto dto, string workDirectory)
    {
        List<Error> errors = [];

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            errors.Add(CommandToolErrors.Invalid("(未命名)", "工具名不能为空。"));
        }
        else if (dto.Name.Any(char.IsWhiteSpace) || dto.Name.Contains('{') || dto.Name.Contains('}'))
        {
            errors.Add(CommandToolErrors.Invalid(dto.Name, "工具名不能含空白与花括号。"));
        }

        ErrorOr<IReadOnlyList<CommandToolTemplateItem>> template = ToTemplateItems(dto.Name ?? "(未命名)", dto.Template);
        if (template.IsError)
        {
            errors.AddRange(template.ErrorsOrEmptyList);
        }

        CheckPath(dto.Name ?? "(未命名)", dto.Path, errors);

        if (dto.OutputLimit is { } outputLimit && outputLimit < 1)
        {
            errors.Add(CommandToolErrors.Invalid(dto.Name ?? "(未命名)", "OutputLimit 必须是正整数。"));
        }

        if (dto.TimeoutSeconds is { } timeout && timeout < 1)
        {
            errors.Add(CommandToolErrors.Invalid(dto.Name ?? "(未命名)", "TimeoutSeconds 必须是正整数。"));
        }
        else if (dto.TimeoutSeconds > CommandToolRunner.MaxTimeoutSeconds)
        {
            errors.Add(CommandToolErrors.Invalid(dto.Name ?? "(未命名)", $"TimeoutSeconds 不能超过 {CommandToolRunner.MaxTimeoutSeconds} 秒。"));
        }

        if (dto.Directory is { Length: > 0 })
        {
            try
            {
                Path.GetFullPath(dto.Directory, workDirectory);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                errors.Add(CommandToolErrors.Invalid(dto.Name ?? "(未命名)", $"Directory {dto.Directory} 无法解析为路径。"));
            }
        }

        ErrorOr<IReadOnlyList<ToolParameter>> parameters = ToParameters(dto.Name ?? "(未命名)", dto.Parameters);
        if (parameters.IsError)
        {
            errors.AddRange(parameters.ErrorsOrEmptyList);
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        ToolName name = new(dto.Name!);
        List<ToolParameter> list = [.. parameters.Value];

        return new CommandToolDefinition(
            name,
            dto.Description ?? $"执行命令 {dto.Name}",
            template.Value,
            list,
            dto.Directory,
            dto.TimeoutSeconds ?? CommandToolRunner.DefaultTimeoutSeconds,
            dto.OutputLimit ?? CommandToolRunner.DefaultOutputLimit,
            dto.Path is { Length: > 0 } path ? new ToolPath(path) : null);
    }

    /// <summary>校验工具路径分组：未写或空合法，格式非法时给出配置错误。</summary>
    private static void CheckPath(string tool, string? raw, List<Error> errors)
    {
        if (raw is null || ToolPath.InvalidReason(raw) is not { } reason)
        {
            return;
        }

        errors.Add(CommandToolErrors.Invalid(tool, reason));
    }

    /// <summary>装配参数声明：名字不能为空、不能含空白与花括号，同一工具内不能重复。</summary>
    private static ErrorOr<IReadOnlyList<ToolParameter>> ToParameters(string tool, List<CommandParameterDto>? dtos)
    {
        List<Error> errors = [];
        List<ToolParameter> parameters = [];
        HashSet<string> names = [];

        foreach (CommandParameterDto dto in dtos ?? [])
        {
            if (dto is null)
            {
                errors.Add(CommandToolErrors.Invalid(tool, "参数条目是 null。"));
                continue;
            }

            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                errors.Add(CommandToolErrors.Invalid(tool, "参数名不能为空。"));
                continue;
            }

            if (dto.Name.Any(char.IsWhiteSpace) || dto.Name.Contains('{') || dto.Name.Contains('}'))
            {
                errors.Add(CommandToolErrors.Invalid(tool, $"参数名 {dto.Name} 不能含空白与花括号。"));
                continue;
            }

            if (!names.Add(dto.Name))
            {
                errors.Add(CommandToolErrors.Invalid(tool, $"参数名 {dto.Name} 重复。"));
                continue;
            }

            parameters.Add(new ToolParameter(new ToolName(dto.Name), dto.Description ?? $"参数 {dto.Name}", Required: dto.Required, List: dto.List));
        }

        return errors.Count > 0 ? errors : parameters;
    }

    /// <summary>装配模板参数项：字符串项要求非空白，对象项要求 Text 与 OmitWhenMissing 都非空且无未知字段。</summary>
    private static ErrorOr<IReadOnlyList<CommandToolTemplateItem>> ToTemplateItems(string tool, List<JsonElement>? elements)
    {
        if (elements is not { Count: > 0 })
        {
            return [CommandToolErrors.Invalid(tool, "模板不能为空。")];
        }

        List<Error> errors = [];
        List<CommandToolTemplateItem> items = [];

        foreach (JsonElement element in elements)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    string text = element.GetString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        errors.Add(CommandToolErrors.Invalid(tool, "模板项不能为空字符串。"));
                        break;
                    }

                    items.Add(new CommandToolTemplateItem(text, null));
                    break;

                case JsonValueKind.Null or JsonValueKind.Undefined:
                    errors.Add(CommandToolErrors.Invalid(tool, "模板项不能是 null。"));
                    break;

                case JsonValueKind.Object:
                    errors.AddRange(element.EnumerateObject()
                        .Where(property => !property.Name.Equals("Text", StringComparison.OrdinalIgnoreCase)
                            && !property.Name.Equals("OmitWhenMissing", StringComparison.OrdinalIgnoreCase))
                        .Select(property => CommandToolErrors.Invalid(tool, $"模板参数项不能含未知字段 {property.Name}。")));

                    CommandToolTemplateItemDto? itemDto;
                    try
                    {
                        itemDto = element.Deserialize(CommandToolJson.Default.CommandToolTemplateItemDto);
                    }
                    catch (JsonException ex)
                    {
                        errors.Add(CommandToolErrors.Format($"命令工具 {tool} 的模板参数项解析失败：{ex.Message}"));
                        break;
                    }

                    if (itemDto is null)
                    {
                        errors.Add(CommandToolErrors.Invalid(tool, "模板参数项不能是 null。"));
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(itemDto.Text))
                    {
                        errors.Add(CommandToolErrors.Invalid(tool, "模板参数项必须给出 Text。"));
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(itemDto.OmitWhenMissing))
                    {
                        errors.Add(CommandToolErrors.Invalid(tool, "模板参数项必须给出 OmitWhenMissing 关联的参数名。"));
                        break;
                    }

                    items.Add(new CommandToolTemplateItem(itemDto.Text, itemDto.OmitWhenMissing));
                    break;

                default:
                    errors.Add(CommandToolErrors.Invalid(tool, "模板项必须是字符串或 {Text, OmitWhenMissing} 对象。"));
                    break;
            }
        }

        return errors.Count > 0 ? errors : items;
    }

    /// <summary>模板与参数声明的对应关系：占位符必须有声明，声明必须出现在模板里，列表参数只能独立成参数项。</summary>
    private static void CheckTemplate(CommandToolDefinition definition, List<Error> errors)
    {
        List<string> placeholders = [.. CommandToolRunner.PlaceholdersOf(definition.Template)];

        foreach (CommandToolTemplateItem item in definition.Template)
        {
            if (item.OmitWhenMissing is not { } linkedName)
            {
                continue;
            }

            ToolParameter? linked = definition.Parameters.FirstOrDefault(parameter => parameter.Name.Value == linkedName);
            if (linked is null)
            {
                errors.Add(CommandToolErrors.Invalid(definition.Name.Value, $"模板参数项关联的参数 {linkedName} 没有对应声明。"));
            }
            else if (!item.Text.Contains($"{{{linkedName}}}"))
            {
                errors.Add(CommandToolErrors.Invalid(definition.Name.Value, $"模板参数项的 Text 里必须出现 {{{linkedName}}} 占位符。"));
            }
        }

        errors.AddRange(placeholders
            .Where(placeholder => definition.Parameters.All(parameter => parameter.Name.Value != placeholder))
            .Select(placeholder => CommandToolErrors.Invalid(definition.Name.Value, $"模板占位符 {{{placeholder}}} 没有对应的参数声明。")));

        errors.AddRange(definition.Parameters
            .Where(parameter => !placeholders.Contains(parameter.Name.Value))
            .Select(parameter => CommandToolErrors.Invalid(definition.Name.Value, $"参数 {parameter.Name.Value} 已声明但没有出现在模板里。")));

        // 列表参数混在别的文字里时在 ExpandToken 会拒绝整项，装载期先给出配置错误
        List<ToolParameter> listParameters = [.. definition.Parameters.Where(parameter => parameter.List)];
        errors.AddRange(
            from parameter in listParameters
            from item in definition.Template
            where item.Text != $"{{{parameter.Name.Value}}}"
                && CommandToolRunner.PlaceholdersOf([item]).Contains(parameter.Name.Value)
            select CommandToolErrors.Invalid(definition.Name.Value, $"列表参数 {parameter.Name.Value} 只能写成独立的 {{{parameter.Name.Value}}} 参数项。"));
    }
}
