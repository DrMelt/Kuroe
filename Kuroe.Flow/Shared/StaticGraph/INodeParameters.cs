namespace Kuroe.Flow.Shared.StaticGraph;

/// <summary>节点类型的参数：类型专属声明的载体。文件形状由文件层的 INodeParameterFile 给出，运行形状由
/// <see cref="Kuroe.Flow.Shared.Execution.INodeParameterRun"/> 给出，引擎只透传，不读其中内容。</summary>
public interface INodeParameters;