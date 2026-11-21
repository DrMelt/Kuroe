# Kuroe.Runner.File

文件读写运行器：读取按字符上限截断，写入按字符上限拒绝并建立缺失的父目录。

## 职责

- 执行参数：文件路径原文、路径变量映射、边界根与字符上限。
- 文件读取：按路径变量解析后读取，内容作为产出。
- 文件写入：把唯一的文本输入写入文件，写入内容作为产出。
- 路径解析与失败说明由读写两侧共用。

## 边界外

- 不含节点类型与参数读取：在 Kuroe.Node.File。
- 不含工作区文件系统实现：在 Kuroe.Files。
- 不含执行状态机、工具与图。

## 依赖

- Kuroe.Kernel.Execution.Shared、Kuroe.Files.Shared、Kuroe.Platform。