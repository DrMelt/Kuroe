using Kuroe.FlowDescription.Definition.Shared.Model;

namespace Kuroe.FlowDescription.Semantics.Shared;

/// <summary>语义诊断：节点定位与说明。</summary>
public sealed record FlowSemanticDiagnostic(NodePath Path, string Message);