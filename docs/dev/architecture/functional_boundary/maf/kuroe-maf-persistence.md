# Kuroe.Maf.Persistence

MAF 流程记录的存档编解码：定义与状态各自成为一段 JSON 片段，状态包含模型与运行期检查点。

## 职责

- MAF 流程定义与状态的存档形状与映射。
- 任务开始状态与开始输入的形状。
- 片段读写约定：读取拒绝未定义字段，写出省略空字段。

## 边界外

- 不含记录模型与种类常量：在 Kuroe.Maf.Records.Shared。
- 不含运行期与检查点生成：在 Kuroe.Maf.Control。
- 不含任务存档中央映射与其他流程种类的编解码。

## 依赖

- Kuroe.Tasks.Shared、Kuroe.Tasks.Records.Shared、Kuroe.Maf.Records.Shared。