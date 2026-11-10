# git

`git` 工具分 `git/read`、`git/write` 与 `git/branch` 三组，白名单写 `git` 放行三组。`git/write` 的写操作不改变当前检出的分支，只放行这组时模型只能在当前分支上改动；`git/branch` 放分支级操作。全部函数执行 PATH 中的 `git` 命令，命令在 `root` 变量给出的目录里执行。操作都是本地的，不含推送与抓取。

## 函数

| 函数                | 参数                                                                                                       | 作用                                                     |
| ------------------- | ---------------------------------------------------------------------------------------------------------- | -------------------------------------------------------- |
| `git/read/Status`   | `paths`、`untracked` 可选                                                                                  | 查看当前分支与各项的暂存、未暂存、未跟踪情况             |
| `git/read/Diff`     | `staged`、`stat`、`nameOnly`、`nameStatus` 开关，`diffFilter`、`revision`、`paths`、`excludePaths` 可选      | 查看改动差异，`nameOnly` 与 `nameStatus` 二选一          |
| `git/read/Log`      | `count` 必填，`revision`、`author`、`grep`、`since`、`until`、`paths`、`excludePaths` 可选，`stat`、`firstParent` 开关 | 查看提交历史，可按作者、说明、时间与修订筛选             |
| `git/read/Show`     | `revision` 必填，`stat`、`firstParent` 开关，`paths` 可选                                                  | 查看一个提交的说明与改动                                 |
| `git/read/Branches` | `pattern` 可选                                                                                             | 列出全部分支及其当前提交，含远端分支                     |
| `git/write/Add`     | `paths` 必填，`excludePaths` 可选                                                                          | 把路径的改动放入暂存区                                   |
| `git/write/Commit`  | `message` 必填，`paths` 可选，`excludePaths` 只在给出 `paths` 时生效                                                               | 提交暂存区的改动，给 `paths` 时只提交这些路径             |
| `git/write/Restore` | `paths` 必填，`staged` 开关，`excludePaths` 可选                                                           | 丢弃改动，`staged` 为真时移出暂存区并保留工作树内容      |
| `git/branch/Switch` | `branch` 必填，`create` 开关                                                                               | 切到分支，`create` 为真时新建分支并切过去                |

## 行为

`paths` 与 `excludePaths` 是路径参数，按 `root` 变量解析，越界或为空时调用被拒绝且不启动进程。路径参数以 `:` 开头时拒绝。`excludePaths` 的每一项展开为 `:(exclude)` 路径，通常与正向路径一起使用，正向路径写 `.` 表示根；`Commit` 的 `excludePaths` 只在给出 `paths` 时生效，单独给出时排除项不进入命令。

筛选参数中，`diffFilter`、`author`、`grep`、`since`、`until` 是选项值，缺省时整个选项消失；`staged`、`stat`、`nameOnly`、`nameStatus`、`firstParent` 是开关，命令行文字由模板固定。`nameOnly` 与 `nameStatus` 只能给出一个。

`revision`、`pattern`、`branch` 与筛选参数是模型给出的值，以 `-` 开头时在启动进程前被拒绝。`message`、`paths`、`excludePaths` 声明了 `AllowLeadingDash`，接受以 `-` 开头的值。修订号、分支名与筛选值的合法性由 git 判断，非法值由 git 报错并交回文本。

`git/write` 不改变当前检出的分支；`git/branch/Switch` 改变当前检出的分支，给的 `branch` 是目标分支。

命令合并 stdout 与 stderr 交回，交回文本以实际执行的命令行开头，随后是输出与退出码。超时 120 秒，输出上限 8000 字符。输出颜色由 git 配置决定。路径基准见[内置工具](index.md)。