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
| `dotnet/write/Run`         | `project` 可选，`arguments` 列表               | 600  | 8000     |
| `dotnet/write/Restore`     | `project` 可选                                 | 600  | 8000     |
| `dotnet/write/Format`      | `project` 可选                                 | 600  | 8000     |
| `dotnet/write/Publish`     | `project`、`configuration` 可选                | 600  | 8000     |
| `dotnet/write/AddPackage`  | `project` 可选，`package` 必填，`version` 可选 | 300  | 8000     |

## 行为

`Info` 给出本机 SDK 与已装运行时的版本、安装位置与当前 RID。`ListSdks` 列出已安装的 SDK。`ListPackages` 列出项目顶层的包引用，`outdated` 为真时只列有新版本的包。`NugetSources` 读到 `root` 目录及其上级的 NuGet 配置。

`Build` 与 `Test` 以 quiet 详细级别运行，交回错误、警告与构建摘要，不包含还原信息与产物路径。

`project` 是路径参数，按 `root` 变量解析并限制在工作区内，缺省时 dotnet 在基准目录里查找。`configuration`、`filter`、`version` 可选，缺省时对应选项不出现在命令行。`arguments` 是独立参数列表，写在 `--` 之后。`package` 必填。

超时后连同进程树终止。命令合并 stdout 与 stderr 交回，并附退出码。不含推送与部署。