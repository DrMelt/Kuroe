# Kuroe.Tool.Implementation.Command.Json

命令工具文件：文件字段形状、读入与装配、条目不合法时一次给出全部错误，以及工具文件形状的注册入口。

## 职责

- 命令工具文件形状：文件字段名与类型固定，读入后装配成命令定义。
- 文件内容：一个命令工具与它的参数声明、模板参数项。
- 读入与装配：文件不存在时为空，条目不合法时一次给出全部错误。
- 注册入口：把命令工具文件形状登记到工具文件注册表。
- 错误构造：命令工具条目的错误。

## 边界外

- 不含模板规则与执行：在 Kuroe.Tool.Implementation.Command.Shared 与 Kuroe.Tool.Implementation.Command。
- 不含工具文件注册表实现：在 Kuroe.Tool.Json。
- 不含工具载体。

## 依赖

- Kuroe.Tool.Json、Kuroe.Tool.Implementation.Command、Kuroe.Tool.Implementation.Command.Shared、Kuroe.Tool、Kuroe.Shared。