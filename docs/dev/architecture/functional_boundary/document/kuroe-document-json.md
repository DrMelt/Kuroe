# Kuroe.Document.Json

流程文件的 JSON 格式实现：节点库文件与流程文件的文本读写与字段绑定，只做格式转换，不构造模型。

## 职责

- 流程文件与节点库文件的文本形态与文件形状互转，读法由源生成固定，不经反射式序列化。
- 字段绑定：端口、连接、触发条件与流程级开始端口的对象形状。
- 导入判定：顶层只写节点库时按节点库读取，只写流程时按流程读取。
- 写出：只映射字段，不依赖模型与类型目录。

## 边界外

- 不含格式端口：读写接口与导入类别在 Kuroe.Document.Json.Shared。
- 不含描述模型：在 Kuroe.Document.Shared。
- 不含校验与编译：端口类型名与节点类型名由各流程实现的装配层解释。
- 不含流程控制与持久化。

## 依赖

- Kuroe.Document.Json.Shared、Kuroe.Document.Shared、Kuroe.Tasks.Shared。