# Kuroe.Flow.Compilation.Documents.Shared

流程描述文档的共享模型：流程、节点定义与引用、端口、连接与参数段的纯数据形状。文件格式与编译器都按这些形状工作，本层不构造模型。

## 职责

- 流程描述：流程名、说明、开始端口与装配树。
- 节点描述：定义节点、引用节点与容器的公共字段与各自差异。
- 连接描述：目标输入端口、来源引用与触发条件，条件未写时按正向。
- 参数段：节点类型专属字段的承载值与中立值，不解释字段含义。

## 边界外

- 不含编译实现与校验规则：在 Kuroe.Flow.Compilation 与 Kuroe.Flow.Compilation.Shared。
- 不含文件格式：在 Kuroe.Flow.Json。
- 不含图运行期与流程控制。

## 依赖

- Kuroe.Flow.Node.Shared。