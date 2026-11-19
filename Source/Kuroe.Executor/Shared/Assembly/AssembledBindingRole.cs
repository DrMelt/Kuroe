namespace Kuroe.Executor.Shared.Assembly;

/// <summary>装配后的绑定参与方式。</summary>
public enum AssembledBindingRole
{
    /// <summary>动态驱动：进入启动组，参与齐备与版本驱动。</summary>
    Dynamic,

    /// <summary>门控：参与齐备与抑制，不驱动新版本。</summary>
    Gate,

    /// <summary>静态：只作为启动条件，不进入版本与触发。</summary>
    Static,

    /// <summary>别名：容器传入端口到外部来源的绑定，不参与容器发布。</summary>
    Alias,
}