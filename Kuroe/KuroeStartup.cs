using Microsoft.Extensions.Logging;

namespace Kuroe;

/// <summary>装配完成时交给宿主的启动信息。</summary>
/// <param name="LogLevel">启动时生效的日志级别。</param>
public sealed record KuroeStartup(LogLevel LogLevel);