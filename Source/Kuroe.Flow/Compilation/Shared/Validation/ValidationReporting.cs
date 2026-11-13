using ErrorOr;
using Kuroe.Flow.Node.StaticGraph.Nodes;

namespace Kuroe.Flow.Compilation.Shared.Validation;

/// <summary>报出一条带作用域的校验错误。</summary>
/// <param name="error">已带节点位置的错误。</param>
public delegate void ValidationErrorHandler(Error error);

/// <summary>报出一条校验错误文本，作用域由调用方补上。</summary>
/// <param name="message">错误文本。</param>
public delegate void ValidationMessageHandler(string message);

/// <summary>报出一个节点的违规文本。</summary>
/// <param name="node">违规的节点。</param>
/// <param name="message">违规文本。</param>
public delegate void NodeViolationHandler(NodeSpec node, string message);