# Kuroe.FlowDescription.Compilation

共享描述编译：把流程描述构造为静态模型，装配文件节点库，展开引用与作用域并校验语言规则。输出展开后的流程定义，图流程与 MAF 流程各自从这一结果装配目标模型。

## 职责

- 描述构造：流程条目与节点库定义到静态模型，字段映射与缺省值。
- 节点类型：类型描述注册表、参数段读取与类型实例建立。
- 库装配：文件节点库校验、重名报错、引用展开与容器作用域。
- 校验：流程规则、节点规则与引用解析。
- 语言不变量：引用可解析、端口声明、连接覆盖与触发来源。
- 规则分界：单节点不变量在定义共享层，跨节点与图级规则在本层。

## 边界外

- 不含描述形态、静态模型、端口词汇、语言规则与编译契约：在 Kuroe.FlowDescription.Description.Shared、Kuroe.FlowDescription.Definition.Shared 与 Kuroe.FlowDescription.Compilation.Shared。
- 不含文件格式：在 Kuroe.FlowDescription.Json。
- 不含图编译、图静态视图与控制：在 Kuroe.Flow.Compilation、Kuroe.Flow.Graph.Shared 与 Kuroe.Flow.Control。
- 不含 MAF 投影与 MAF 运行期：在 Kuroe.Maf.Compilation 与 Kuroe.Maf.Control。

## 依赖

- Kuroe.FlowDescription.Description.Shared、Kuroe.FlowDescription.Definition.Shared、Kuroe.FlowDescription.Compilation.Shared、Kuroe.Kernel.Tasks.Shared。