# Kuroe.Executor.Shared

执行器与端口词汇的中立契约：端口类型注册、端口模式、输入角色、连接触发条件与参数段读取接口。图流程与 MAF 流程按同一份词汇解释端口。

## 职责

- 端口词汇：端口类型名、语义、默认模式与允许模式。类型只能经注册产生，解析不区分大小写。
- 输入角色：端口在执行中作为内容、配置还是控制。
- 触发条件：门控条件的取值与判定。
- 参数段读取：节点描述按字段名取本轮取值，形状不符经报出接口交给调用方。

## 边界外

- 不含节点形状、端口声明与校验规则：在 Kuroe.Flow.Graph.Shared 与 Kuroe.Flow.Compilation。
- 不含描述文档模型：在 Kuroe.Document.Shared。
- 不含执行运行期与运行器：在 Kuroe.Execution.Shared 与 Kuroe.Runner。
- 不含流程装配：在 Kuroe.Flow.Compilation、Kuroe.Flow.NodeLibrary 与 Kuroe.Maf.Compilation。

## 依赖

- 无工程依赖。