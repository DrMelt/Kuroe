using Kuroe.Configuration;

namespace Kuroe.Agent;

/// <summary>一次模型选择的结果：是否改动了用户层，以及该改动的影响。</summary>
public sealed record ModelSelection(bool Changed, SettingsEffect Effect);
