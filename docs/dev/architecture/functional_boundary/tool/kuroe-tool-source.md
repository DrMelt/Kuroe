# Kuroe.Tool.Source

导出资料工具：把随产物分发的源码资料导出到指定目录。导出目录在装配期给出，任一函数被放行时创建载体。

## 职责

- 导出资料工具源：按装配期给出的导出目录建立载体。
- 资料导出：把产物内的源码资料写到目标目录。

## 边界外

- 不含工具载体框架与文件访问契约。
- 不含工作区文件系统实现。
- 不含宿主装配与入口。

## 依赖

- Kuroe.Tool、Kuroe.Tool.FileAccess.Shared、Kuroe.Tool.FileAccess、Kuroe.Files.Shared。