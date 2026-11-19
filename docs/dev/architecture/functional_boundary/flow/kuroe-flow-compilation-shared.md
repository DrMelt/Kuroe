# Kuroe.Flow.Compilation.Shared

流程编译的共享契约：编译入口、库展开与节点类型描述。编译管道只按这些形状工作。

## 职责

- 节点类型描述注册表：类型名到描述的唯一入口，描述给出形状、字段表与参数段读取方式。
- 编译入口与库展开：构造、展开、校验与图编译的固定顺序，库装配把授权流程与节点库定义展开为具体流程。
- 参数段契约：字段表与描述到类型实例的转换由执行器契约给出，读侧接口在 Kuroe.Executor.Shared。
- 编译结果：按文档声明序返回全部流程图。

## 边界外

- 不含描述文档模型：在 Kuroe.Document.Shared。
- 不含节点规则：在 Kuroe.Flow.Graph.Shared。
- 不含编译实现：文档构造、模型校验与图编译在 Kuroe.Flow.Compilation。
- 不含图运行期与流程控制。
- 不含文件格式：在 Kuroe.Document.Json。

## 依赖

- Kuroe.Document.Shared、Kuroe.Tasks.Shared、Kuroe.Flow.Graph.Shared、Kuroe.Executor.Shared。