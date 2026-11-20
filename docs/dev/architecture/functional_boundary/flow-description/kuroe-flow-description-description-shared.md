# Kuroe.FlowDescription.Description.Shared

流程文件写下的中立形态：流程描述、节点描述、连接描述与参数段，以及描述层的错误作用域。格式层与编译层都按这组记录读取与转换，记录本身不解释字段含义。

## 职责

- 流程描述：流程名、说明、开始端口与装配树。
- 节点描述：定义节点、引用节点与容器的公共字段与各自差异。
- 连接描述：目标输入端口、来源引用与触发条件，条件未写时按正向。
- 参数段：节点类型专属字段的承载值与中立值，不解释字段含义。
- 错误作用域：节点库与流程名称拼成统一错误文本，格式层与编译层共用。
- 状态边界：只承载描述，不保存运行状态，不构造模型。

## 边界外

- 不含静态模型与端口词汇：在 Kuroe.FlowDescription.Definition.Shared。
- 不含编译契约与参数读取：在 Kuroe.FlowDescription.Compilation.Shared。
- 不含编译实现：在 Kuroe.FlowDescription.Compilation。
- 不含文件格式：在 Kuroe.FlowDescription.Json。

## 依赖

- Kuroe.Tasks.Shared。