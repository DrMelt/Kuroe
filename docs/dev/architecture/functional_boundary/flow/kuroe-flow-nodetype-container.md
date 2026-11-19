# Kuroe.Flow.NodeType.Container

容器节点类型：组织成员并限定作用域，自身不执行，成员齐备且启动组未被抑制时发布。运行期对象由引擎建立。

## 职责

- 容器类型定义：类型名与静态形状。
- 描述契约：类型实例由引擎提供，不接受类型参数。
- 内置注册：随引擎装配登记的容器描述。

## 边界外

- 不含作用域校验规则：在 Kuroe.FlowDescription.Compilation。
- 不含图运行期与节点调度：在 Kuroe.Flow.Control 与 Kuroe.Flow.Graph.Shared。
- 不含其他节点类型。

## 依赖

- Kuroe.FlowDescription.Shared、Kuroe.Tasks.Shared、Kuroe.Flow.Graph.Shared。