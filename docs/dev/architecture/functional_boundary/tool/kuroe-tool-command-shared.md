# Kuroe.Tool.Command.Shared

命令模板工具的契约：占位符文本规则、执行参数缺省与模板一致性校验。占位符写成花括号包住的参数名，内层不含花括号。

## 职责

- 模板文本规则：占位符写法与参数项。
- 执行参数缺省：未给出时的取值约定。
- 模板一致性：模板、参数声明与路径变量必须互相吻合。

## 边界外

- 不含执行：在 Kuroe.Tool.Command。
- 不含文件格式：在 Kuroe.Tool.Command.Json。
- 不含工具载体与调用上下文。

## 依赖

- Kuroe.Tool、Kuroe.Shared。