# 开发文档

这里面向修改 Kuroe 源码与把 Kuroe 作为库接入的读者。安装、配置与操作见[使用文档](../user/index.md)。

文档分层与写作规则见 [docs-rules](../docs-rules.md)。

## 域索引

模块与边界文档的完整索引见 [architecture](architecture/architecture.md#项目文档索引)。

| 域 | 内容 |
| --- | --- |
| kernel | 执行单元、任务域、流程控制、输入通道、表现与任务持久化 |
| flow | 图流程模型、编译、运行期与宿主流程库 |
| flow-description | 流程描述契约 |
| executor | 执行器契约、端口词汇与装配 |
| codeflow | 代码定义流程的记录与编解码 |
| maf | MAF 流程定义、控制、记录与装配 |
| runner | Llm、命令、文件、输入与审批执行运行器 |
| node | 图流程的节点类型 |
| tool | 工具载体、工具组与工具文件 |
| assistant | 内置助手会话域 |
| host | 核心装配、终端入口与命令行入口 |
| platform | 路径与配置视图、工作区文件、进程执行 |

## 跨模块机制

[执行模型](architecture/mechanism/execution.md)、[流程控制与任务域](architecture/mechanism/flow-control.md)、[执行契约工具](architecture/mechanism/contract-tools.md)、[存档与持久化](architecture/mechanism/persistence.md)、[同名覆盖与重名](architecture/mechanism/overrides.md)、[MAF 流程](architecture/mechanism/maf-flow.md)、[新增流程实现与扩展点](architecture/mechanism/extensions.md)。

## 第三方库机制

[Agent Framework 工作流](libraries/agent-framework-workflows.md)、[Agent Framework 会话](libraries/agent-framework-sessions.md)、[Spectre.Console 渲染](libraries/spectre-console.md)。

## 工程

[架构测试](架构测试.md)、[从源码构建](从源码构建.md)。