using ErrorOr;
using Kuroe.Shared.Configuration;

namespace Kuroe.Shared.Catalogs;

/// <summary>一次模型选择的结果：是否改动了用户层，以及该改动的影响。</summary>
public sealed record ModelSelection(bool Changed, SettingsEffect Effect);

/// <summary>一次模型注销的结果：模型已从目录注销；当前选择随之取消时给出改动影响，取消未生效时给出原因。</summary>
public sealed record ModelRemoval(SettingsEffect Effect, IReadOnlyList<Error> Failures);