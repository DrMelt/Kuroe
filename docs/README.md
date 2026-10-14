# Kuroe 文档

新用户先读「入门」两篇，完成第一个任务后按需查阅。

## 入门

- [快速上手](usage/快速上手.md)：环境准备到第一个任务的最短路径
- [安装与运行](usage/安装与运行.md)：环境要求、依赖准备、启动参数与工作目录

## 核心

- [任务操作](usage/任务操作.md)：任务与 run、查看、批准与返工
- [命令参考](usage/命令参考.md)：全部斜杠命令与参数规则
- [模型接入](usage/模型接入.md)：提供商与模型注册、模型选择语义
- [设置与目录管理](usage/设置与目录管理.md)：偏好设置与目录迁移

## 进阶

- [流程文件与节点库](usage/流程文件与节点库.md)：flows.json 结构、节点库合并与装载校验
- [节点定义](usage/节点定义.md)：节点类型、执行节点、输入节点、容器定义与端口
- [流程装配](usage/流程装配.md)：模型选择、引用与绑定、依赖展开、上游接线与校验环
- [工具扩展](usage/工具扩展.md)：ITool 实现、工具面与命令模板工具

## 开发

```powershell
dotnet build Kuroe.slnx
dotnet test Kuroe.slnx
```

测试项目对应生产项目的职责：`Kuroe.Shared.Tests`、`Kuroe.Flows.Shared.Tests`、`Kuroe.Flows.Json.Tests`、`Kuroe.Flows.Tests`、`Kuroe.Flows.Node.Llm.Tests` 验证各自库的单元行为，`Kuroe.Tests` 验证宿主装配出的集成行为，`Kuroe.Cli.Tests` 验证终端呈现与命令路由。`Kuroe.Tests` 除解析冒烟外以假执行器替换 `IRunExecutor`，不覆盖真实模型执行链。

生产项目依赖单向：`Kuroe.Shared` 与 `Kuroe.Flows.Shared` 是基础层，`Kuroe.Flows.Json` 依赖 `Kuroe.Flows.Shared`，`Kuroe.Flows` 依赖这两个库，`Kuroe.Flows.Node.Llm` 依赖这两个基础库，`Kuroe` 依赖上述全部，`Kuroe.Cli` 依赖 `Kuroe`、`Kuroe.Shared`、`Kuroe.Flows.Shared` 与 `Kuroe.Flows.Node.Llm`。

引擎不反向依赖宿主：`Kuroe.Flows` 与 `Kuroe.Flows.Shared` 只依赖 .NET 与 ErrorOr，宿主经 `IRunExecutor`、`IRunContract`、`IRunPolicy`、`IExecutionTargetResolver`、`IFlowCatalog` 接入；错误码分侧放在 `Kuroe.Flows.Shared.FlowErrorCodes` 与 `Kuroe.Shared.ErrorCodes`。执行节点的模型执行独立在 `Kuroe.Flows.Node.Llm`，它只依赖 `Kuroe.Flows.Shared` 与 `Kuroe.Shared`，模型接入与请求参数由宿主经 `IModelAccess`、`IModelSettings` 提供。

`Kuroe` 内部按域分层，依赖单向：`Storage` → `Configuration` → `Catalogs` → `Workflows`，装配入口 `Kuroe` 依赖全部；`Configuration` 与 `Catalogs` 分别实现 `Kuroe.Flows.Node.Llm` 的 `IModelSettings` 与 `IModelAccess` 端口。`Presentation` 提供状态、节点类型、端口与连接的展示名，只依赖引擎的只读类型，被 `Workflows` 与 `Kuroe.Cli` 引用。
