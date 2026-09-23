using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErrorOr;
using Kuroe.Agent;
using Kuroe.Storage;

namespace Kuroe.Workflows;

/// <summary>flows.json 的读写。文件形状由 DTO 承载，绑定成 <see cref="Workflow"/> 后交校验。</summary>
sealed class WorkflowStore(string file)
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _file = Path.GetFullPath(file);

    /// <summary>流程文件所在目录，用户给出的文件参数以此为基准。</summary>
    public string BaseDirectory => Path.GetDirectoryName(_file)!;

    /// <summary>把文件参数解析为绝对路径，相对参数按流程文件所在目录解析。</summary>
    public ErrorOr<string> Resolve(string path)
    {
        try
        {
            return Path.GetFullPath(path, BaseDirectory);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            return [WorkflowErrors.InvalidPath(path, ex.Message)];
        }
    }

    /// <summary>读取流程文件，文件不存在时得到内置流程。</summary>
    public ErrorOr<List<Workflow>> Load()
    {
        if (!File.Exists(_file))
        {
            return new List<Workflow> { Workflow.Builtin };
        }

        return Read(_file);
    }

    /// <summary>把全部流程写回文件。</summary>
    public ErrorOr<Success> Save(IReadOnlyList<Workflow> flows) => Write(_file, flows);

    /// <summary>从指定文件读取流程，供导入使用。</summary>
    public static ErrorOr<List<Workflow>> Read(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [WorkflowErrors.Read(path, ex.Message)];
        }

        FlowFileDto? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<FlowFileDto>(text, ReadOptions);
        }
        catch (JsonException ex)
        {
            return [WorkflowErrors.Format($"{path}：{ex.Message}")];
        }

        return parsed is null ? [WorkflowErrors.Format($"{path} 是空文件。")] : ToWorkflows(parsed);
    }

    private static ErrorOr<Success> Write(string path, IReadOnlyList<Workflow> flows)
    {
        FlowFileDto file = new()
        {
            Flows = [.. flows.Select(ToDto)],
        };

        try
        {
            AtomicFile.WriteText(path, JsonSerializer.Serialize(file, WriteOptions));

            return Result.Success;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [WorkflowErrors.Write(path, ex.Message)];
        }
    }

    private static ErrorOr<List<Workflow>> ToWorkflows(FlowFileDto file)
    {
        List<Workflow> flows = [];
        List<Error> errors = [];

        foreach (WorkflowDto dto in file.Flows)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                errors.Add(WorkflowErrors.Name("有流程缺少名称"));

                continue;
            }

            List<StepSpec> steps = [];
            foreach (StepDto step in dto.Steps)
            {
                if (step.Role is not { } role)
                {
                    errors.Add(WorkflowErrors.Step(dto.Name, step.Name ?? "(未命名)", "缺少 Role。"));

                    continue;
                }

                steps.Add(new StepSpec
                {
                    Name = step.Name ?? string.Empty,
                    Role = role,
                    Model = step.Model,
                    Prompt = step.Prompt,
                    Scope = step.Scope ?? StepScope.Single,
                    From = step.From ?? [],
                    Gate = step.Gate ?? StepGate.Auto,
                    OnReject = step.OnReject,
                    MaxAttempts = step.MaxAttempts,
                });
            }

            flows.Add(new Workflow(dto.Name, dto.Description, steps));
        }

        return errors.Count > 0 ? errors : flows;
    }

    private static WorkflowDto ToDto(Workflow flow) => new()
    {
        Name = flow.Name,
        Description = flow.Description,
        Steps = [.. flow.Steps.Select(step => new StepDto
        {
            Name = step.Name,
            Role = step.Role,
            Model = step.Model,
            Prompt = step.Prompt,
            Scope = step.Scope,
            From = [.. step.From],
            Gate = step.Gate,
            OnReject = step.OnReject,
            MaxAttempts = step.MaxAttempts,
        })],
    };

    private sealed class FlowFileDto
    {
        public List<WorkflowDto> Flows { get; init; } = [];
    }

    private sealed class WorkflowDto
    {
        public string? Name { get; init; }

        public string? Description { get; init; }

        public List<StepDto> Steps { get; init; } = [];
    }

    private sealed class StepDto
    {
        public string? Name { get; init; }

        public RunRole? Role { get; init; }

        public string? Model { get; init; }

        public string? Prompt { get; init; }

        public StepScope? Scope { get; init; }

        public List<string>? From { get; init; }

        public StepGate? Gate { get; init; }

        public RejectAction? OnReject { get; init; }

        public int? MaxAttempts { get; init; }
    }
}
