# Kuroe.Maf

MAF 流程装配入口：在核心装配之上注册 MAF 流程实现、存档编解码与流程模板，核心装配不引用 MAF 运行期。

## 职责

- 装配入口：注册流程实现、存档编解码与模板来源，宿主一次调用即可提交 MAF 流程。
- 按定义来源登记可供提交的 MAF 流程。

## 边界外

- 不含流程定义：在 Kuroe.Maf.Definition。
- 不含运行期与检查点：在 Kuroe.Maf.Control。
- 不含编解码实现：在 Kuroe.Maf.Persistence。
- 不含核心装配、图流程与入口项目。

## 依赖

- Kuroe.Control.Shared、Kuroe.Tasks.Records.Shared、Kuroe.Maf.Definition、Kuroe.Maf.Control、Kuroe.Maf.Persistence。