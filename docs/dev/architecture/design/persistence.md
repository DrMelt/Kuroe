# 存档恢复

上层结构见[系统结构](index.md)。

```mermaid
flowchart TD
    Persistence["存档文件、租约、写盘、启动恢复、存档编解码<br>Kuroe.Kernel/Tasks/Persistence"]
    GraphCodec["图流程编解码<br>Kuroe.Flow/Persistence"]
    CodeRecords["代码流程记录与编解码<br>Kuroe.CodeFlow"]
    MafCodec["MAF 流程编解码<br>Kuroe.Maf/Persistence"]
```