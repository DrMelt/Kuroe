# Kuroe.FlowDescription.Compilation.Shared

共享描述编译的对外契约：编译入口、库装配入口、节点类型描述的扩展契约与参数读取。

## 职责

- 编译入口：`IFlowDescriptionCompiler` 把流程描述编译为展开后的流程定义列表。
- 库装配：`IFlowExpander` 合并内置与文件定义并逐流程展开。
- 节点类型契约：类型名、静态形状、字段表与描述到类型实例的转换。
- 类型注册表：类型名到描述的唯一入口。
- 参数读取：按字段名取本轮取值，字段声明描述取值形态。

## 边界外

- 不含描述形态：在 Kuroe.FlowDescription.Description.Shared。
- 不含静态模型、端口词汇与语言规则：在 Kuroe.FlowDescription.Definition.Shared。
- 不含编译实现：在 Kuroe.FlowDescription.Compilation。
- 不含文件格式：在 Kuroe.FlowDescription.Json。

## 依赖

- Kuroe.FlowDescription.Description.Shared、Kuroe.FlowDescription.Definition.Shared、Kuroe.Kernel.Tasks.Shared。