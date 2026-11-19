# Kuroe.Executor.Shared

执行器与端口词汇的中立契约：端口类型注册、端口模式、输入角色、连接触发条件、参数段读取接口与执行器条目形状。图流程与 MAF 流程按同一份词汇解释端口。

## 职责

- 端口词汇：端口类型名、语义、默认模式与允许模式。类型只能经注册产生，解析不区分大小写。
- 输入角色：端口在执行中作为内容、配置还是控制。
- 触发条件：门控条件的取值与判定。
- 参数段读取：节点描述按字段名取本轮取值，形状不符经报出接口交给调用方。
- 执行器条目：种类名、静态形状、输入输出端口声明、输出声明方式、展示属性与条目实例。
- 装配形状：节点定义、已装配流程、已装配节点与已解析接线。

## 边界外

- 不含节点形状的校验规则与图结构：在 Kuroe.Flow.Graph.Shared 与 Kuroe.Flow.Compilation。
- 不含流程描述模型：在 Kuroe.FlowDescription.Shared。
- 不含装配行为：在 Kuroe.Executor.Compilation、Kuroe.Flow.Compilation、Kuroe.Flow.NodeLibrary。
- 不含执行运行期与运行器：在 Kuroe.Execution.Shared 与 Kuroe.Runner。

## 依赖

- Kuroe.Tasks.Shared。