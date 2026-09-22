using Kuroe.Configuration;

namespace Kuroe.Agent;

/// <summary>一次模型注销的结果：注销的是否为当前选中的模型，以及取消选择带来的影响。</summary>
public sealed record ModelRemoval(bool WasSelected, SettingsEffect Effect);
