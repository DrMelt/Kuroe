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
| `dotnet/write/TestMtp`     | `project` 与 `solution` 二选一                 | 600  | 8000     |
| `dotnet/write/Run`         | `project` 可选，`arguments` 列表               | 600  | 8000     |
| `dotnet/write/Restore`     | `project` 可选                                 | 600  | 8000     |
| `dotnet/write/Format`      | `project`、`severity` 可选                     | 600  | 8000     |
| `dotnet/write/Publish`     | `project`、`configuration` 可选                | 600  | 8000     |
| `dotnet/write/AddPackage`  | `project` 可选，`package` 必填，`version` 可选 | 300  | 8000     |

## 格式整理

`dotnet/write/Format` 按项目里的格式设置整理源码。`severity` 是诊断严重级别，可选 `info`、`warn`、`error`，省略时不传 `--severity`，由 `dotnet format` 使用自身缺省级别。

`dotnet/read/VerifyFormat` 只校验不改动文件，存在待整理的文件时命令以非零退出码结束。`severity` 的取值与省略规则与 `Format` 相同。退出码与输出按命令结果原样交回。

## MTP 测试

`project` 是测试项目路径，`solution` 是解决方案路径，两者二选一，同时给出时调用被拒绝。两者分别对应 `--project` 与 `--solution`，都不给时由 `dotnet test` 在 `root` 目录里查找。

命令以 `--ignore-exit-code 8` 忽略未发现测试的退出码，并在 `--verbosity quiet` 之后以 `-- --report-trx` 让每个测试应用生成 TRX 报告。指定解决方案时 `dotnet test` 把 `--` 之后的选项转给各个测试应用。目标测试项目必须引用 `Microsoft.Testing.Extensions.TrxReport`，缺少扩展时测试应用把 `--report-trx` 当作未知选项并以退出码 5 结束。报告写到 `root` 目录下的 `TestResults`。

命令合并 stdout 与 stderr 交回，交回文本以实际执行的命令行开头，随后是输出与退出码。超时与输出上限见上表。