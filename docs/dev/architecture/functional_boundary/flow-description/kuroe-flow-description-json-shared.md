# Kuroe.FlowDescription.Json.Shared

流程文件格式端口：节点库文件与流程文件的读写接口、导入文件类别与读取判定。承载格式由实现给出，端口只声明转换方向。

## 职责

- 节点库文件读入与写出：承载格式与节点定义互转，错误按文件位置或节点作用域给出。
- 流程文件读入与写出：承载格式与流程描述互转，错误按文件位置或流程作用域给出。
- 导入判定：按顶层字段判定节点库文件或流程文件，导入内容只承载其中一类。

## 边界外

- 不含 JSON 实现：在 Kuroe.FlowDescription.Json。
- 不含描述模型：在 Kuroe.FlowDescription.Shared。不含编译：在 Kuroe.Flow.Compilation。
- 不含流程控制与持久化。

## 依赖

- Kuroe.FlowDescription.Shared。