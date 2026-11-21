# Kuroe.Kernel.Tasks.Persistence.Serialization.Shared

存档文档的通用形状与映射：任务存档与文档字段一一对应，流程片段交给按身份注册的编解码器，中央映射不按流程种类分支。

## 职责

- 存档文档：格式版本与任务形状。
- 任务形状：身份、生命周期、完成事实、过程记录、流程记录与执行记录。
- 执行形状：状态、输入、用量、恢复负载与契约，端口值逐项成形状。
- 映射与校验：任务存档与文档之间的双向映射，流程定义与状态经编解码端口转换。
- 格式版本：读取只接受当前版本，写入始终使用当前版本。
- 编解码端口：任务存档文本与任务存档之间的转换；不需要文本的存储可直接实现存储端口。

## 边界外

- 不含具体流程片段格式：由 flow、codeflow 与 maf 的编解码器解释。
- 不含 JSON 文本处理。
- 不含文件存储与任务域。

## 依赖

- Kuroe.Kernel.Execution.Shared、Kuroe.Kernel.Execution.Records.Shared、Kuroe.Kernel.Tasks.Records.Shared、Kuroe.Kernel.Tasks.Shared。