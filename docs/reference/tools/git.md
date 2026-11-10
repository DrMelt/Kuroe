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
| `git/write/Remove`  | `paths` 必填，`cached` 开关，`excludePaths` 可选                                                           | 删除已跟踪文件，总是递归，`cached` 为真时只移出索引      |
| `git/write/Clean`   | `paths` 必填，`dryRun`、`force` 开关                                                                       | 清理未跟踪文件与目录，`dryRun` 为真时只预演              |
| `git/write/Apply`   | `patch` 必填，`check`、`stat`、`reverse` 开关                                                              | 把补丁应用到工作树，`check` 为真时只校验                 |
| `git/branch/Switch` | `branch` 必填，`create` 开关                                                                               | 切到分支，`create` 为真时新建分支并切过去                |
| `git/branch/Merge`  | `revision` 与 `abort`、`continue` 三选一，`fastForwardOnly`、`noFastForward` 开关只在合并时用 | 把分支或提交合并进当前分支，可中止或继续一次合并         |
| `git/branch/Rebase` | `upstream` 与 `abort`、`continue`、`skip` 四选一，`autostash` 开关只在变基时用                  | 把当前分支变基到上游，可中止、继续或跳过                 |

## 行为

`paths`、`excludePaths` 与 `patch` 是路径参数，按 `root` 变量解析，越界或为空时调用被拒绝且不启动进程；展开进命令行的是解析后的绝对路径。路径参数以 `:` 开头时拒绝。`excludePaths` 的每一项展开为 `:(exclude)` 路径，通常与正向路径一起使用，正向路径写 `.` 表示根；`Commit` 的 `excludePaths` 只在给出 `paths` 时生效，单独给出时排除项不进入命令。

筛选参数中，`diffFilter`、`author`、`grep`、`since`、`until` 是选项值，缺省时整个选项消失；`staged`、`stat`、`nameOnly`、`nameStatus`、`firstParent` 是开关，命令行文字由模板固定。`nameOnly` 与 `nameStatus` 只能给出一个。

`revision`、`upstream`、`pattern`、`branch` 与筛选参数是模型给出的值，以 `-` 开头时在启动进程前被拒绝；路径参数不受这条规则约束，展开值是绝对路径。`message` 声明了 `AllowLeadingDash`，接受以 `-` 开头的值。修订号、分支名与筛选值的合法性由 git 判断，非法值由 git 报错并交回文本。

`git/write` 不改变当前检出的分支：`Add`、`Commit` 与 `Restore` 改暂存区与工作树，`Remove` 改索引与工作树，`Clean` 删除未跟踪条目，`Apply` 把补丁应用到工作树。`git/branch` 改变当前检出的分支或分支顶端的提交：`Switch` 切分支，`Merge` 与 `Rebase` 改写分支顶端。`Merge` 与 `Rebase` 的命令行固定带 `-c core.editor=:`，进程非交互，编辑器被替换为无操作，合并提交与继续变基都不打开编辑器。`Merge` 的 `revision` 与 `abort`、`continue` 三选一，快进开关只与 `revision` 同用；`abort`、`continue` 收尾一次进行中的合并，命令行只带自己的开关。`Rebase` 不含交互式变基，`upstream` 与 `abort`、`continue`、`skip` 四选一，`autostash` 只与 `upstream` 同用或无参数时单独给出。

`Clean` 的 `dryRun` 只列出将删除的条目，`force` 才实际删除，两者都缺时 git 自身以 `clean.requireForce` 拒绝。`Apply` 的 `check` 只判断补丁能否应用，`stat` 只给改动统计，两者都不改动文件。`Remove` 不给 `cached` 时删除工作树文件，`Clean` 删除未跟踪文件与目录，两者删除后都不保留可恢复内容。

命令合并 stdout 与 stderr 交回，交回文本以实际执行的命令行开头，随后是输出与退出码。超时 120 秒，输出上限 8000 字符。输出颜色由 git 配置决定。路径基准见[内置工具](index.md)。