using ApiHub.Shared.Models;
using ErrorOr;

namespace Kuroe.Flows.Node.Llm;

/// <summary>执行节点到模型接入信息的解析，由宿主提供。目录与选择的语义在提供方。</summary>
public interface IModelAccess
{
    /// <summary>按模型名取接入信息，模型未注册时返回错误。</summary>
    ErrorOr<ModelConnection> Connect(ModelName model);

    /// <summary>模型可接入时成功，否则给出接入信息解析的错误，供不持有客户端的调用方判可用性。</summary>
    ErrorOr<Success> Check(ModelName model);
}
