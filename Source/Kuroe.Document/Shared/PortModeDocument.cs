namespace Kuroe.Document.Shared;

/// <summary>端口模式的描述取值：普通动态、触发动态或常量。未写模式时的默认值与支持范围由端口类型注册决定。</summary>
public enum PortModeDocument
{
    /// <summary>普通动态端口。</summary>
    Plain,

    /// <summary>触发动态端口，交回时携带触发值。</summary>
    Trigger,

    /// <summary>常量端口，值在任务内只确定一次，只供下游绑定读取。</summary>
    Constant,
}