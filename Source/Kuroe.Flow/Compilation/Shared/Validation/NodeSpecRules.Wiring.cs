using Kuroe.Flow.Shared.StaticGraph.Nodes;

namespace Kuroe.Flow.Compilation.Shared.Validation;

/// <summary>节点接线规则：From、In、Static 与 Validate 的上下文限制。</summary>
public static partial class NodeSpecRules
{
    /// <summary>按作用域检查接线字段。container 为真时按容器判定，引用条目按被引用定义判定。</summary>
    public static void CheckWiring(this NodeSpec node, NodeRuleContext context, Action<string> fail, bool? container = null)
    {
        bool isContainer = container ?? node.IsContainer;
        if (context == NodeRuleContext.Reference)
        {
            if (isContainer)
            {
                if (node.From.Count > 0)
                {
                    fail("引用容器不能声明 From，容器自身不接线。");
                }

                if (node.Static is not null)
                {
                    fail("引用容器不写 Static，静态输入由 In 绑定。");
                }

                if (node.Validate is not null)
                {
                    fail("引用节点组不能声明 Validate，输出校验只属于执行节点。");
                }
            }
            else
            {
                if (node.In is { Count: > 0 })
                {
                    fail("引用执行节点不写输入端口绑定，接线一律经 From。");
                }
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

                if (node.In is not null)
                {
                    fail("库容器定义不接线，不能声明输入端口绑定。");
                }

                if (node.Static is not null)
                {
                    fail("库容器定义不写 Static，静态输入由引用处绑定。");
                }

                if (node.Validate is not null)
                {
                    fail("库容器定义不声明 Validate，输出校验只属于执行节点。");
                }
            }
            else
            {
                if (node.From.Count > 0)
                {
                    fail("容器节点不是执行节点，不支持 From。");
                }

                if (node.Static is not null)
                {
                    fail("容器节点不写 Static，静态输入由 In 绑定。");
                }

                if (node.Validate is not null)
                {
                    fail("容器节点不是执行节点，不支持 Validate。");
                }
            }

            return;
        }

        if (context == NodeRuleContext.LibraryDefinition)
        {
            if (node.From.Count > 0)
            {
                fail("执行节点库定义的接线由引用处提供，不能写 From。");
            }

            if (node.In is not null)
            {
                fail("执行节点库定义不绑定输入端口，接线从引用处提供。");
            }

            if (node.Static is not null)
            {
                fail("执行节点库定义的静态端口由引用处绑定，不能写 Static。");
            }
        }
        else
        {
            if (node.In is { Count: > 0 })
            {
                fail("执行节点不写输入端口绑定，接线一律经 From。");
            }
        }
    }
}