# 开发文档

这里面向修改 Kuroe 源码与把 Kuroe 作为库接入的读者。安装、配置与操作见[使用文档](../user/index.md)。

文档分层与写作规则见 [docs-rules](../docs-rules.md)。

## 域索引

| 域 | 内容 | 进入 |
| --- | --- | --- |
| kernel | 执行单元、任务域、流程控制、输入通道、表现与任务持久化 | [kuroe-execution-shared](architecture/functional_boundary/kernel/kuroe-execution-shared.md)、[kuroe-tasks-runtime](architecture/functional_boundary/kernel/kuroe-tasks-runtime.md)、[kuroe-control-runtime](architecture/functional_boundary/kernel/kuroe-control-runtime.md) |
| flow | 图流程模型、编译、运行期与宿主流程库 | [kuroe-flow-graph-shared](architecture/functional_boundary/flow/kuroe-flow-graph-shared.md)、[kuroe-flow-control](architecture/functional_boundary/flow/kuroe-flow-control.md)、[kuroe-flow-hosting](architecture/functional_boundary/flow/kuroe-flow-hosting.md) |
| document | 流程描述文档契约 | [kuroe-document-shared](architecture/functional_boundary/document/kuroe-document-shared.md) |
| executor | 执行器契约、装配与端口词汇 | [kuroe-executor-shared](architecture/functional_boundary/executor/kuroe-executor-shared.md)、[kuroe-executor-compilation](architecture/functional_boundary/executor/kuroe-executor-compilation.md) |
| codeflow | 代码定义流程的记录与编解码 | [kuroe-codeflow-records-shared](architecture/functional_boundary/codeflow/kuroe-codeflow-records-shared.md) |
| maf | MAF 流程定义、控制、记录与装配 | [kuroe-maf-definition](architecture/functional_boundary/maf/kuroe-maf-definition.md)、[kuroe-maf-control](architecture/functional_boundary/maf/kuroe-maf-control.md)、[kuroe-maf-compilation](architecture/functional_boundary/maf/kuroe-maf-compilation.md) |
| runner | Llm、命令、文件、输入与审批执行运行器 | [kuroe-runner-llm](architecture/functional_boundary/runner/kuroe-runner-llm.md)、[kuroe-runner-command](architecture/functional_boundary/runner/kuroe-runner-command.md) |
| node | 图流程的节点类型 | [kuroe-node-llm](architecture/functional_boundary/node/kuroe-node-llm.md)、[kuroe-node-command](architecture/functional_boundary/node/kuroe-node-command.md) |
| tool | 工具载体、工具组与工具文件 | [kuroe-tool](architecture/functional_boundary/tool/kuroe-tool.md)、[kuroe-tool-contract](architecture/functional_boundary/tool/kuroe-tool-contract.md) |
| assistant | 内置助手会话域 | [kuroe-assistant](architecture/functional_boundary/assistant/kuroe-assistant.md) |
| host | 核心装配、终端入口与命令行入口 | [kuroe](architecture/functional_boundary/host/kuroe.md)、[kuroe-console](architecture/functional_boundary/host/kuroe-console.md)、[kuroe-cli](architecture/functional_boundary/host/kuroe-cli.md) |
| platform | 路径与配置视图、工作区文件、进程执行 | [kuroe-shared](architecture/functional_boundary/platform/kuroe-shared.md)、[kuroe-files](architecture/functional_boundary/platform/kuroe-files.md)、[kuroe-process](architecture/functional_boundary/platform/kuroe-process.md) |

## 跨模块机制

[执行模型](architecture/mechanism/execution.md)、[流程控制与任务域](architecture/mechanism/flow-control.md)、[执行契约工具](architecture/mechanism/contract-tools.md)、[存档与持久化](architecture/mechanism/persistence.md)、[MAF 流程](architecture/mechanism/maf-flow.md)、[新增流程实现与扩展点](architecture/mechanism/extensions.md)。

## 第三方库机制

[Agent Framework 工作流](libraries/agent-framework-workflows.md)、[Agent Framework 会话](libraries/agent-framework-sessions.md)、[Spectre.Console 渲染](libraries/spectre-console.md)。

## 工程

[架构测试](架构测试.md)、[从源码构建](从源码构建.md)。