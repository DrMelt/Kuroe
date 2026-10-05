# Kuroe

基于 .NET 10 + Microsoft Agent Framework 与 Microsoft.Extensions.AI 的智能体骨架：终端 REPL、流式输出、自动工具调用，多个任务并发执行。模型接入与流程编排分别落在上层调用与图执行器上，会话与工具调用由框架承担。流程由节点组织：节点库定义可复用的执行节点与容器，流程按名装配并从装配层输入模型；执行节点声明工具白名单与产出契约，容器组合执行节点并限定作用域。

提供商与模型的接入目录由 [ApiHub](https://github.com/DrMelt/ApiHub) 提供。

## 特性

- 回复逐字流式输出，`Ctrl+C` 只中断当前一轮
- 多个任务同时执行，任务内的 run 由流程执行节点派出，互相不共享上下文
- 任务流程可配置：制定计划、分配执行都只是流程里的节点，每个执行节点声明自己的模型选择、能力工具与指令
- 按条目展开实施，执行节点与容器可停等批准，规则在加载流程时校验
- 查看以任务为单位：任务列表 → 任务内已执行与在执行的 run → run 的上下文来源与过程记录
- 工具调用自动发起，调用参数与结果分行呈现
- 前台对话可直接询问 Kuroe 状态：任务、run、流程、模型目录、配置与工具名单由内置信息查询工具取回，是斜杠命令之外的另一种查看方式
- 提供商与模型在命令行登记，目录可脱敏导出、合并导入
- 偏好改动立即生效，需要重启或会使旧上下文失效时当场提示
- 内置时间工具，实现 `ITool` 声明函数即可扩展工具库
- 命令工具可配置：`.kuroe/tools.json` 里声明命令模板，带参数占位符，模型调用时在工作目录执行

## 快速开始

环境要求：.NET SDK 10.0，可读键盘的交互终端。

ApiHub 未发布到 nuget.org，`nuget.config` 的 local 源指向仓库内的 `packages/`，该目录不入库。克隆后在仓库根目录下载 release 的四个包：

```powershell
$ver = '0.2.0'
'ApiHub', 'ApiHub.ChatClient', 'ApiHub.Json', 'ApiHub.Shared' | ForEach-Object {
    Invoke-WebRequest "https://github.com/DrMelt/ApiHub/releases/download/v$ver/$_.$ver.nupkg" -OutFile "packages/$_.$ver.nupkg"
}
```

启动：

```powershell
dotnet run --project Kuroe.Cli -- -d <工作目录>
```

`-d` 省略时用进程当前目录。首次使用先登记提供商与模型：

```text
/provider add openai https://api.openai.com/v1 sk-xxxx
/model add <模型> openai
/model <模型>
/task new 写一段百字以内的欢迎致辞
```

工作目录的 `.kuroe/` 下存放 `settings.json`、`catalog.json`、`flows.json` 与 `tools.json` 四个配置文件，分别是偏好、模型目录、流程模板与命令工具；`catalog.json` 含明文凭据，`tools.json` 声明由模型实参调用的命令，两者都要来源可信。

## 文档

完整使用说明见 [docs](docs/README.md)。

## 许可

AGPL-3.0-only，见 `LICENSE`。
