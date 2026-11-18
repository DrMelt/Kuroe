# Kuroe.Tasks.Persistence.Saving

存档写盘协调实现：合并状态变化，后台逐任务捕获并写入，刷新时等待落盘。

## 职责

- 待写集合的合并：同一任务的多次变化只写一次。
- 后台写盘：逐任务捕获存档并交给存储写入。
- 刷新：等待全部或指定任务落盘后返回，供提交与终止等需要立即可见的状态使用。

## 边界外

- 不含协调契约：端口与结果在 Kuroe.Tasks.Persistence.Saving.Shared。
- 不含文件写入与编解码。
- 不含任务状态读取：捕获来源在运行时。

## 依赖

- Kuroe.Tasks.Persistence.Saving.Shared、Kuroe.Tasks.Records.Shared、Kuroe.Tasks.Persistence.Files.Shared、Kuroe.Tasks.Shared、Kuroe.Tasks.Presentation.Shared。