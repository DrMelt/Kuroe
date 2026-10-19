using ErrorOr;

namespace Kuroe.Tool.Json;

/// <summary>工具文件的统一装载：按注册表读全部文件，合并结果并校验跨文件的路径重复。</summary>
public static class ToolFiles
{
    /// <summary>读全部已注册的工具文件，逐层收集错误，文件之间路径重复时一次给出全部重复项。</summary>
    /// <param name="files">文件形状的注册表。</param>
    /// <param name="configDirectory">配置目录，文件按文件名在其中查找。</param>
    /// <param name="workDirectory">工作目录，相对路径以此为基准。</param>
    public static ErrorOr<IReadOnlyList<ConfiguredTool>> Load(
        ToolFileRegistry files,
        string configDirectory,
        string workDirectory)
    {
        List<ConfiguredTool> tools = [];
        List<Error> errors = [];
        HashSet<string> paths = [];

        foreach (IToolFile file in files.Files)
        {
            ErrorOr<IReadOnlyList<ConfiguredTool>> loaded = file.Load(configDirectory, workDirectory);
            if (loaded.IsError)
            {
                errors.AddRange(loaded.ErrorsOrEmptyList);
                continue;
            }

            foreach (ConfiguredTool tool in loaded.Value)
            {
                if (!paths.Add(tool.Definition.Path.Value))
                {
                    errors.Add(ToolFileErrors.Duplicate(tool.Definition.Path.Value));
                    continue;
                }

                tools.Add(tool);
            }
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        return tools;
    }
}