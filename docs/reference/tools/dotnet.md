# dotnet

`dotnet` 工具分 `dotnet/read` 与 `dotnet/write` 两组，白名单写 `dotnet` 放行两组。全部函数执行 PATH 中的 `dotnet` 命令，命令在 `root` 变量给出的目录里执行，不在交互模式下运行。

## 函数

| 函数                       | 参数                                           | 超时 | 输出上限 |
| -------------------------- | ---------------------------------------------- | ---- | -------- |
| `dotnet/read/Info`         | 无                                             | 120  | 8000     |
| `dotnet/read/ListSdks`     | 无                                             | 120  | 8000     |
| `dotnet/read/ListPackages` | `project` 可选，`outdated` 开关                | 300  | 8000     |
| `dotnet/read/NugetSources` | 无                                             | 120  | 8000     |
| `dotnet/write/Build`       | `project`、`configuration` 可选                | 600  | 8000     |
| `dotnet/write/Test`        | `project`、`filter` 可选                       | 600  | 8000     |
| `dotnet/write/TestMtp`     | `project`、`filter` 可选                       | 600  | 8000     |
| `dotnet/write/Run`         | `project` 可选，`arguments` 列表               | 600  | 8000     |
| `dotnet/write/Restore`     | `project` 可选                                 | 600  | 8000     |
| `dotnet/write/Format`      | `project` 可选                                 | 600  | 8000     |
| `dotnet/write/Publish`     | `project`、`configuration` 可选                | 600  | 8000     |
| `dotnet/write/AddPackage`  | `project` 可选，`package` 必填，`version` 可选 | 300  | 8000     |
