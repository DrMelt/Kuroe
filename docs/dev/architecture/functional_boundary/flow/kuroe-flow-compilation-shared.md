# Kuroe.Flow.Compilation.Shared

流程编译的共享契约：编译入口、库展开、节点类型描述与参数读侧。编译管道只按这些形状工作。

## 职责

- 节点类型描述注册表：类型名到描述的唯一入口，描述给出形状、字段表与参数段读取方式。
- 编译入口与库展开：构造、展开、校验与图编译的固定顺序，库装配把授权流程与节点库定义展开为具体流程。
- 参数段读侧：字段声明、取值形态与读侧，形状不符报错，缺失取安全默认。
- 编译结果：校验通过的流程模型与执行视图。

## 边界外

- 不含描述文档模型：在 Kuroe.Flow.Compilation.Documents.Shared。
- 不含节点规则：在 Kuroe.Flow.Node.Shared。
- 不含编译实现：文档构造、模型校验与图编译在 Kuroe.Flow.Compilation。
- 不含图运行期与流程控制。
- 不含文件格式：在 Kuroe.Flow.Json。

## 依赖

- Kuroe.Flow.Compilation.Documents.Shared、Kuroe.Tasks.Shared、Kuroe.Flow.Node.Shared。