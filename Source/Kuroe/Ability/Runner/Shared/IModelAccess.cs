using ApiHub.Shared.Models;
using ErrorOr;

namespace Kuroe.Ability.Runner.Shared;

/// <summary>模型接入信息的解析。实现由宿主提供。</summary>
public interface IModelAccess
{
    /// <summary>按模型名取接入信息，模型未注册时返回错误。</summary>
    ErrorOr<ModelConnection> Connect(ModelName model);

    /// <summary>模型可接入时成功，否则给出接入信息解析的错误。</summary>
    ErrorOr<Success> Check(ModelName model);
}