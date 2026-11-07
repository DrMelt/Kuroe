# dotnet

`dotnet` 工具分 `dotnet/read` 与 `dotnet/write` 两组，白名单写 `dotnet` 放行两组。全部函数执行 PATH 中的 `dotnet` 命令，命令在 `root` 变量给出的目录里执行，不在交互模式下运行。

## 函数

| 函数                       | 参数                                           | 超时 | 输出上限 |
| -------------------------- | ---------------------------------------------- | ---- | -------- |
| `dotnet/read/Info`         | 无                                             | 120  | 8000     |
| `dotnet/read/ListSdks`     | 无                                             | 120  | 8000     |
| `dotnet/read/ListPackages` | `project` 可选，`outdated` 开关                | 300  | 8000     |
| `dotnet/read/NugetSources` | 无                                             | 120  | 8000     |
| `dotnet/read/VerifyFormat` | `project`、`severity` 可选                     | 600  | 8000     |
| `dotnet/write/Build`       | `project`、`configuration` 可选                | 600  | 8000     |
| `dotnet/write/Test`        | `project`、`filter` 可选                       | 600  | 8000     |
| `dotnet/write/TestMtp`     | `project`、`filter` 可选                       | 600  | 8000     |
| `dotnet/write/Run`         | `project` 可选，`arguments` 列表               | 600  | 8000     |
| `dotnet/write/Restore`     | `project` 可选                                 | 600  | 8000     |
| `dotnet/write/Format`      | `project`、`severity` 可选                     | 600  | 8000     |
| `dotnet/write/Publish`     | `project`、`configuration` 可选                | 600  | 8000     |
| `dotnet/write/AddPackage`  | `project` 可选，`package` 必填，`version` 可选 | 300  | 8000     |

## 格式整理

`dotnet/write/Format` 按项目里的格式设置整理源码。`severity` 是诊断严重级别，可选 `info`、`warn`、`error`，省略时不传 `--severity`，由 `dotnet format` 使用自身缺省级别。

`dotnet/read/VerifyFormat` 只校验不改动文件，存在待整理的文件时命令以非零退出码结束。`severity` 的取值与省略规则与 `Format` 相同。退出码与输出按命令结果原样交回。

命令合并 stdout 与 stderr 交回，交回文本以实际执行的命令行开头，随后是输出与退出码。超时与输出上限见上表。
