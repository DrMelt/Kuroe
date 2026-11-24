# 开发文档

这里面向修改 Kuroe 源码与把 Kuroe 作为库接入的读者。安装、配置与操作见[使用文档](../user/index.md)。

## 总体设计

[项目结构](architecture/functional_boundary/index.md)给出分层结构图与项目索引。

## 层索引

模块与边界文档的完整索引见[项目结构](architecture/functional_boundary/index.md)。

| 层 | 内容 |
| --- | --- |
| entry | 交互终端与命令行入口 |
| assembly | 核心装配、图流程装配、MAF 装配与内置助手 |
| flow | 图流程与 MAF 流程运行期、流程编译与流程库 |
| description | 流程描述 |
| ability | 节点类型、运行器与工具 |
| persistence | 任务存档、写盘协调、启动恢复与流程编解码 |
| kernel | 任务域、执行、流程控制、输入通道与表现契约 |
| platform | 路径与配置视图、工作区文件、进程执行 |

## 跨模块机制

[跨模块机制](architecture/mechanism/index.md)。

## 工程

[开发规范](../standards/index.md)、[架构测试](architecture-tests.md)、[从源码构建](build-from-source.md)。