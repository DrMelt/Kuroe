# Kuroe.FlowDescription.Json

流程文件的 JSON 格式实现：节点库文件与流程文件的文本读写与字段绑定，只做格式转换，不构造模型。

## 职责

- 流程文件与节点库文件的文本形态与文件形状互转，读法由源生成固定，不经反射式序列化。
- 字段绑定：端口、连接、触发条件与流程级开始端口的对象形状。
- 条目归属：定义与引用两类条目的字段归属在读取时判定，写在条目上的类别字段与类型专属字段只有本层可见。
- 错误作用域：条目标识与说明按节点库或流程作用域拼出统一错误文本，与共享校验使用同一作用域。
- 导入判定：顶层只写节点库时按节点库读取，只写流程时按流程读取。
- 写出：只映射字段，不依赖模型与类型目录。

## 边界外

- 不含格式端口：读写接口与导入类别在 Kuroe.FlowDescription.Json.Shared。
- 不含描述模型：在 Kuroe.FlowDescription.Description.Shared。
- 不含校验与编译：端口类型名与节点类型名由流程描述的节点类型契约与共享编译解释。
- 不含流程控制与持久化。

## 依赖

- Kuroe.FlowDescription.Json.Shared、Kuroe.FlowDescription.Description.Shared、Kuroe.Kernel.Tasks.Shared。