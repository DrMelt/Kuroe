# Kuroe.Maf.NodeExecution

MAF 内置节点的执行装配：把 MAF 步骤可表达的节点类型注册为执行器绑定。

## 职责

- 执行器装配：按节点类型名注册 Llm、Command、FileRead 与 FileWrite 的执行器绑定。
- 运行器工厂接入口：由宿主提供各节点类型使用的运行器工厂。

## 边界外

- 不含节点类型、参数与校验：在 Kuroe.Node.*。
- 不含 MAF 步骤投影与流程定义：在 Kuroe.Maf.Compilation 与 Kuroe.Maf.Definition。
- 不含图流程输出投影与模型声明解析：在 Kuroe.Flow.NodeExecution。
- 不支持等待外部输入的节点类型。

## 依赖

- Kuroe.Node.Llm、Kuroe.Node.File、Kuroe.Node.Command、Kuroe.Maf.Compilation、Kuroe.Maf.Compilation.Shared、Kuroe.FlowDescription.Definition.Shared、Kuroe.Kernel.Execution.Shared。