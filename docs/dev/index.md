# 开发文档

这里面向修改 Kuroe 源码与把 Kuroe 作为库接入的读者。安装、配置与操作见[使用文档](../user/index.md)。

## 总体设计

[系统结构](architecture/design/index.md)给出运行时分层图。

## 域索引

模块与边界文档的完整索引见 [architecture](architecture/architecture.md#项目文档索引)。

| 域 | 内容 |
| --- | --- |
| kernel | 执行单元、任务域、流程控制、输入通道、表现与任务持久化 |
| flow | 图流程运行期与宿主流程库 |
| flow-description | 流程描述、端口词汇与共享描述编译 |
| codeflow | 代码定义流程的记录与编解码 |
| maf | MAF 流程定义、控制、记录与装配 |
| runner | Llm、命令、文件、输入与审批执行运行器 |
| node | 图流程的节点类型 |
| tool | 工具载体、工具组与工具文件 |
| assistant | 内置助手会话域 |
| host | 核心装配、终端入口与命令行入口 |
| platform | 路径与配置视图、工作区文件、进程执行 |

## 跨模块机制

[跨模块机制](architecture/mechanism/index.md)。

## 工程

[开发规范](../standards/index.md)、[架构测试](architecture-tests.md)、[从源码构建](build-from-source.md)。