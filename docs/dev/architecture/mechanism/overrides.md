# 同名覆盖与重名

装配期按名字合并多处来源。生效规则取决于所在位置：注册表同名覆盖，注册批内重名报错，文件导入按名覆盖合并，引用条目覆盖库定义的少量字段，端口类型重复注册被拒绝。

## 注册同名覆盖

| 注册表 | 键 | 注册项 |
| --- | --- | --- |
| `NodeTypeDescriptionRegistry` | `NodeKind` | `INodeTypeDescription` |
| `ExecutorRegistry` | `ExecutorKind` | `IExecutorDescription` |
| `ToolFileRegistry` | 文件名 | `IToolFile` |

同名注册原位替换，注册序不变。注册在装配期完成，之后只读调用。

## 注册批内重名

`NodeLibrary.Register(IReadOnlyList<NodeSpec>)` 返回同一批内的重名，同名后注册覆盖先注册并保留首次出现的位置。

`NodeLibraryExpander` 把内置定义与文件定义分两批注册。文件批次的重名报“节点名重复”并终止装配；内置批次的重名不报出，只按同名覆盖处理。

两批之间是同名覆盖：内置定义先注册，文件定义后注册，同名时文件定义生效。

## 按名覆盖合并

`FlowService.Import` 把导入的节点定义与流程按名覆盖合并：同名后写覆盖先写，并保留首次出现的位置。

- 不可用流程不导入。导入文件里的流程与已有流程同名且不可用时，保留原流程。
- 节点库导入要求节点库合法且不破坏当前可用流程，否则整个导入不生效。

## 引用条目覆盖

`Use` 引用库定义时，引用条目的 `Emission` 与 `MaxExecutions` 覆盖库定义的同名字段，展开取引用值优先。接线取引用处：`Bindings` 与 `From` 由引用条目给出，输入输出端口取库定义。

## 契约端口补全

节点的执行契约输入端口只在流程未声明同名端口时补入。`NodeSpec.DeclaredInputs` 是声明端口加未声明的契约端口，`NodeSpec.EffectiveInputs` 在类型允许推断时再补 `From` 目标引入的内容端口。

## 重复注册拒绝

`PortTypes.Register` 对同名端口类型抛 `InvalidOperationException`。端口类型不覆盖。

## 与覆盖不一致的现状

`FlowTemplateCatalog.Find` 按来源顺序取第一个命中，`All` 拼接各来源结果且不去重。同名模板既不覆盖也不报错。