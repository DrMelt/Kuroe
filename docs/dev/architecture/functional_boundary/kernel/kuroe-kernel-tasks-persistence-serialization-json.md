# Kuroe.Kernel.Tasks.Persistence.Serialization.Json

存档的 JSON 编解码：把通用文档形状写成 JSON 文本并读回，字段映射与流程片段仍由通用格式负责。

## 职责

- 存档文本与文档之间的转换。
- 文件绑定的读写约定：读取拒绝未定义字段，写出省略空字段并保留非 ASCII 字符。

## 边界外

- 不含文档形状与字段映射：在 Kuroe.Kernel.Tasks.Persistence.Serialization.Shared。
- 不含文件定位与原子替换：文件定位在 Kuroe.Kernel.Tasks.Persistence.Files，原子替换在 Kuroe.Files。
- 不含流程片段格式与任务域。

## 依赖

- Kuroe.Kernel.Tasks.Persistence.Serialization.Shared、Kuroe.Kernel.Execution.Shared、Kuroe.Kernel.Tasks.Shared、Kuroe.Kernel.Tasks.Records.Shared。