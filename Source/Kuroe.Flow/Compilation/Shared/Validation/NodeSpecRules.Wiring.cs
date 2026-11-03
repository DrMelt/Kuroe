using Kuroe.Flow.Shared.StaticGraph.Nodes;

namespace Kuroe.Flow.Compilation.Shared.Validation;

/// <summary>节点接线规则：From 与输入绑定的上下文限制。绑定目标能否承接由 <see cref="CheckBindings"/> 按端口模式判定。</summary>
public static partial class NodeSpecRules
{
    /// <summary>按作用域检查接线字段。container 为真时按容器判定，引用条目按被引用定义判定。</summary>
    public static void CheckWiring(this NodeSpec node, NodeRuleContext context, ValidationMessageHandler fail, bool? container = null)
    {
        bool isContainer = container ?? node.IsContainer;
        if (context == NodeRuleContext.Reference)
        {
            if (isContainer && node.From.Count > 0)
            {
                fail("引用容器不能声明 From，容器自身不接线。");
            }

            return;
        }

        if (isContainer)
        {
            if (context == NodeRuleContext.LibraryDefinition)
            {
                if (node.From.Count > 0)
                {
                    fail("库容器定义不接线，不能声明 From。");
                }

                if (node.Bindings is not null)
                {
                    fail("库容器定义不接线，不能声明输入绑定。");
                }

                return;
            }

            if (node.From.Count > 0)
            {
                fail("容器节点不是执行节点，不支持 From。");
            }

            return;
        }

        if (context == NodeRuleContext.LibraryDefinition)
        {
            if (node.From.Count > 0)
            {
                fail("执行节点库定义的接线由引用处提供，不能写 From。");
            }

            if (node.Bindings is not null)
            {
                fail("执行节点库定义的端口由引用处绑定，不能写输入绑定。");
            }
        }
    }
}