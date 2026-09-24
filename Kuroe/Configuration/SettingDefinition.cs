namespace Kuroe.Configuration;

/// <summary>一个设置项的声明：所属节、项名与生效值的取值方式。</summary>
internal sealed record SettingDefinition(string Section, string Name, Func<KuroeSettings, object?> Value)
{
    /// <summary>规范路径，用户层键、命令参数与呈现共用同一份。</summary>
    public string Path => $"{Section}:{Name}";
}
