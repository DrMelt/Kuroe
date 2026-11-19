# Kuroe.Executor.Compilation

执行器装配的中立实现：执行器描述注册表、装配错误作用域与参数段读取。

## 职责

- 执行器描述注册表：种类名到描述的唯一入口，同名覆盖，按注册序保留种类名。
- 装配错误作用域：执行器库定义与流程两类作用域，节点标识与说明拼成统一错误文本。
- 参数段读取：按条目字段表检查未知字段与取值形状，再建出条目实例。

## 边界外

- 不含执行器契约与端口词汇：在 Kuroe.Executor.Shared。
- 不含描述文档模型：在 Kuroe.Document.Shared。
- 不含文档构造、库展开与接线解析：在 Kuroe.Flow.Compilation 与 Kuroe.Flow.NodeLibrary。
- 不含图结构、图校验与运行期。

## 依赖

- Kuroe.Executor.Shared、Kuroe.Document.Shared、Kuroe.Tasks.Shared。