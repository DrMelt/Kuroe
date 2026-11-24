# 架构测试

项目引用、跨层类型与执行写入口的约束由 `Tests/Architecture` 下的测试固定。新增引用、跨层类型或执行写入口时先补齐或调整对应断言。

## 覆盖范围

| 文件 | 覆盖 |
| --- | --- |
| `RepositoryStructureTests` | 仓库结构、`InternalsVisibleTo` 目标、共享测试工程引用、编译集成测试与宿主分离 |
| `ProjectLayoutTests` | 源项目名与目录、测试类型目录、单元测试镜像、解决方案项目清单、命名空间根 |
| `KernelAreaTests` | Kernel 只引用 Kernel、输入项目边界 |
| `KernelControlTests` | 控制共享契约、流程运行期、流程模板目录、恢复按身份解析 |
| `KernelExecutionTests` | 执行契约与共享项目、执行状态机归属、执行记录来源 |
| `KernelTaskTests` | 任务域与任务注册表、任务快照装配归属、执行记录来源标识、任务动作与执行归属、工作树状态改动面 |
| `PresentationBoundaryTests` | 表现共享与任务表现的项目归属 |
| `RecordPersistenceTests` | 图记录边界、持久化项目引用边界、任务持久化不出现流程模型、记录种类常量与编解码器随实现存放 |
| `FlowAreaTests` | 编译契约的引用边界、流程项目不引用宿主目录、流程库只引用流程与内核契约、图共享层只引用描述层与契约层、图执行接口命名、流程实现按职责拆分端口、图状态保存执行上下文 |
| `FlowDescriptionBoundaryTests` | 描述、定义与编译三个共享层逐层引用，JSON 层只依赖描述层与格式契约 |
| `NodeRunnerTests` | 节点引用自身运行器实现、共享节点不引用流程实现、图流程装配与 MAF 节点执行装配各自引用运行器、运行器不引用节点与图、运行期能力中性、运行器共享契约无项目引用 |
| `HostAndToolBoundaryTests` | 除 Console 外宿主入口不引用图运行期与图共享层、Console 只引用图共享层与图契约层、文件访问共享项不依赖同模块核心 |
| `AssistantBoundaryTests` | 助手宿主只依赖代码流程、对话宿主与代码流程分区 |
| `MafBoundaryTests` | MAF 与 Kernel 契约的引用关系、MAF 运行期不引用流程区域、文档装配只引用流程描述 |
| `GlobalStateTests` | 静态成员只允许不可变常量与纯函数、时钟随机源与环境只在系统适配器与组合根读取、按源码路径豁免 |

## 项目与目录约束

`ProjectLayoutTests` 固定项目布局。

- 源项目名由 `Source` 下的目录段以点连接得到，末级目录为 `Core` 时省略该段，其他层目录保留。
- 镜像单元测试位于 `Tests/Unit`，目录按源项目目录镜像，项目名为源项目名加 `.Tests`。
- 集成测试位于 `Tests/Integration`，架构测试位于 `Tests/Architecture`。
- 测试支持项目位于 `Tests/Support`，项目名以 `.TestSupport` 结尾，目录名为项目名。
- `Kuroe.Architecture.Tests`、`Kuroe.Integration.Tests` 与 `Kuroe.FlowDescription.Compilation.Integration.Tests` 按测试类型放置，不镜像源项目。
- `Kuroe.slnx` 包含全部源项目与测试项目且不重复。
- 命名空间根取项目名，类型由项目名定位；`Kuroe.Runner.Shared` 取使用方命名空间 `Kuroe.Runner`，由 `ProjectLayoutTests` 校验。