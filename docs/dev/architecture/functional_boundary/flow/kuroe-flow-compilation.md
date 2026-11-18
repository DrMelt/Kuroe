# Kuroe.Flow.Compilation

流程描述编译实现：文档构造、库展开、模型校验与图编译按固定顺序执行，节点类型描述在此注册。

## 职责

- 文档构造：字段映射、缺省值与形状驱动的结构，不判断模型不变量。
- 库展开：把授权流程与节点库定义展开成具体流程。
- 模型校验：按节点规则与节点类型校验契约逐条报错。
- 图编译：把校验后的描述编译为可运行图。
- 节点类型描述注册表：类型名到描述的唯一入口，同名覆盖。

## 边界外

- 不含描述模型：在 Kuroe.Flow.Compilation.Documents.Shared。不含规则定义：在 Kuroe.Flow.Compilation.Shared。
- 不含图运行期、流程控制与持久化。
- 不含文件读写：在 Kuroe.Flow.Json。

## 依赖

- Kuroe.Flow.Compilation.Documents.Shared、Kuroe.Flow.Compilation.Shared、Kuroe.Flow.Shared、Kuroe.Tasks.Shared、Kuroe.Flow.Node.Shared。