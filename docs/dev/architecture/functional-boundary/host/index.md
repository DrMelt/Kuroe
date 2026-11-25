# 宿主

上层结构见[项目结构](../index.md)。

```mermaid
flowchart TD
    Ability["执行能力<br>Kuroe/Host/Ability<br>Kuroe/Flow/NodeType"]
    Assembly["宿主装配<br>Kuroe/Host/{Composition,Assistant,FlowIntegration}"]
    Platform["宿主平台契约<br>Kuroe/Host/Platform"]
    Assembly --> Ability
    Ability --> Platform
    Assembly --> Platform
```

| 子域 | 项目 | 文件 |
| --- | --- | --- |
| 执行能力 |  | [ability](ability/index.md) |
| 宿主装配 |  | [assembly](assembly/index.md) |
| 宿主平台契约 | `Kuroe.Host.Platform` | [kuroe-host-platform](platform/kuroe-host-platform.md) |