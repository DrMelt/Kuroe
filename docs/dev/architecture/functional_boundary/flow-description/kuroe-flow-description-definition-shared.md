# Kuroe.FlowDescription.Definition.Shared

流程语言的静态定义模型：流程定义、节点树、端口词汇与单节点语言规则。描述编译输出这一模型，图流程、MAF 流程与节点类型都按它协作。

## 职责

- 标识与路径：节点类型名、节点名、节点标签与节点路径。
- 流程定义：流程级开始端口与根节点。
- 节点模型：定义、引用与容器的统一节点表示、类型形状、执行配置与输出声明。
- 端口词汇：端口类型、语义、模式、输入角色、端口引用与触发条件；类型集合在装配前确定，运行期只读。
- 语言规则：标识、结构、接线、绑定覆盖与端口接口的单节点不变量。
- 状态边界：只承载编译期派生，不保存运行状态；编译结果跨任务共享，运行期只读。

## 边界外

- 不含文件写下的描述形态与描述层错误作用域：在 Kuroe.FlowDescription.Description.Shared。
- 不含编译入口、库装配、节点类型扩展契约与参数读取：在 Kuroe.FlowDescription.Compilation.Shared。
- 不含编译实现：在 Kuroe.FlowDescription.Compilation。
- 不含图静态视图、就绪判定与流程控制：在 Kuroe.Flow.Compilation、Kuroe.Flow.Graph.Shared 与 Kuroe.Flow.Control。

## 依赖

- Kuroe.FlowDescription.Description.Shared、Kuroe.Kernel.Tasks.Shared。