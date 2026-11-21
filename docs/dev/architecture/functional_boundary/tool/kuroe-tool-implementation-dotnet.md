# Kuroe.Tool.Implementation.Dotnet

dotnet 工具组：查看环境、包引用、SDK 与包源，校验格式，以及还原、编译、测试、发布、运行、添加包与整理源码。全部是命令模板工具。

## 职责

- 工具源：装配期提供定义，运行期按执行绑定创建本轮可用的命令工具。
- 环境查询：查看本机 .NET 环境、已装 SDK 与包源。
- 项目查询：列出项目包引用。
- 构建动作：还原依赖、编译项目、发布项目、运行项目与整理源码。
- 测试动作：运行测试，并提供测试平台运行方式。
- 包动作：添加包引用。
- 格式动作：校验源码格式。

## 边界外

- 不含命令模板的执行与装配：在 Kuroe.Tool.Implementation.Command 与 Kuroe.Tool.Implementation.Command.Shared。
- 不含进程实现。
- 不含工具文件配置。

## 依赖

- Kuroe.Tool.Implementation.Command、Kuroe.Tool.Implementation.Command.Shared、Kuroe.Tool、Kuroe.Platform。