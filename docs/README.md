# Kuroe 文档

使用说明按主题拆分：

- [安装与运行](usage/安装与运行.md)：环境要求、依赖准备、启动与工作目录
- [任务操作](usage/任务操作.md)：任务与 run、查看、批准与返工
- [流程节点配置](usage/流程节点配置.md)：flows.json 的模型选择、节点字段、校验规则与示例
- [命令参考](usage/命令参考.md)：全部斜杠命令与参数规则
- [设置与目录管理](usage/设置与目录管理.md)：偏好设置与提供商、模型维护
- [模型接入](usage/模型接入.md)：提供商与模型登记，模型选择语义
- [工具扩展](usage/工具扩展.md)：ITool 实现与工具面

## 开发

```powershell
dotnet build Kuroe.slnx
dotnet test Kuroe.slnx
```