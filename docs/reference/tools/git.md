# git

`git` 工具分 `git/read` 与 `git/write` 两组，白名单写 `git` 放行两组。全部函数执行 PATH 中的 `git` 命令，命令在 `root` 变量给出的目录里执行。操作都是本地的，不含推送与抓取。

## 函数

| 函数                | 参数                                    | 作用                                                |
| ------------------- | --------------------------------------- | --------------------------------------------------- |
| `git/read/Status`   | 无                                      | 查看当前分支与各项的暂存、未暂存、未跟踪情况        |
| `git/read/Diff`     | `staged` 开关，`revision`、`paths` 可选 | 查看改动差异，`staged` 为真时比较暂存区与 HEAD      |
| `git/read/Log`      | `count` 必填，`paths` 可选              | 查看提交历史，单行概述并按新到旧排列                |
| `git/read/Show`     | `revision` 必填                         | 查看一个提交的说明与改动                            |
| `git/read/Branches` | 无                                      | 列出全部分支及其当前提交，含远端分支                |
| `git/write/Add`     | `paths` 必填                            | 把路径的改动放入暂存区                              |
| `git/write/Commit`  | `message` 必填，`paths` 可选            | 提交暂存区的改动，给 `paths` 时只提交这些路径       |
| `git/write/Switch`  | `branch` 必填，`create` 开关            | 切到分支，`create` 为真时新建分支并切过去           |
| `git/write/Restore` | `paths` 必填，`staged` 开关             | 丢弃改动，`staged` 为真时移出暂存区并保留工作树内容 |

## 行为

`paths` 是路径参数，按 `root` 变量解析，越界时调用被拒绝且不启动进程。开关是布尔参数，命令行文字由模板固定。修订号与分支名不做校验，非法值由 git 报错并交回文本。

命令合并 stdout 与 stderr 交回，交回文本以实际执行的命令行开头，随后是输出与退出码。超时 120 秒，输出上限 8000 字符。路径基准见[内置工具](index.md)。