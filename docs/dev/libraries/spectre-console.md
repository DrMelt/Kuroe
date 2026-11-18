# Spectre.Console 渲染与能力探测

`Spectre.Console` 提供终端渲染元素与输出能力探测。Kuroe 只在 Console 使用它，动态文本一律按纯文本写出。

## 控制台实例

`AnsiConsole.Console` 是标准输出实例。标准错误需要单独建立：

```csharp
AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(System.Console.Error) });
```

`IAnsiConsole.Profile.Capabilities` 给出 ANSI、交互与 Unicode 能力。非终端或不支持 ANSI 时自动降级为无样式文本，因此不需要自行判断环境。

## 渲染元素

渲染元素实现 `IRenderable`，可以互相嵌套：

| 元素 | 用途 |
| --- | --- |
| `Text` | 纯文本，不做标记解析 |
| `Markup` | 按标记解析的文本，动态内容必须先经 `Markup.Escape` |
| `Rule` | 分隔横线，可带标题与对齐 |
| `Panel` | 带边框与标题的块 |
| `Table` | 表格，可关闭边框与表头 |

Kuroe 的约定：标题等固定文案之外，所有来自运行期的文本都构造为 `Text` 或先转义，避免方括号被当作标记。

## 直接写控制序列

`AnsiWriter` 由输出实例的 `Profile.Out.Writer` 与 `Profile.Capabilities` 构造，提供 `EraseInDisplay`、`ClearScrollback` 与 `CursorPosition`。写入器按能力决定是否发出控制序列，不支持时操作静默失效。Kuroe 用它实现页面重绘。

## 排版约定

- 页面标题用 `Rule`，层级路径按分隔符连接，横线占满宽度。
- 块用 `Panel`，形状与线样式由类别决定，内边距固定。
- 列表用无边框 `Table`，列声明是否换行；键值表关闭表头，标签列不换行。
- 全部写入串行，并发通知不插入正在输出的行。