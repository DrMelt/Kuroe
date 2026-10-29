using Kuroe.Flow.Shared.Model;

namespace Kuroe.Flow.Shared.StaticGraph.Ports;

/// <summary>编译期的一条成员端口引用：成员路径与端口名。定义层的引用写名字，编译期按作用域解析成这里的路径。</summary>
public readonly record struct NodePort(NodePath Node, PortName Port);