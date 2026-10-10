namespace Kuroe.TestSupport;

/// <summary>测试共享的流程模板 JSON。</summary>
public static class TestFlows
{
    /// <summary>按条目展开且每个条目 run 都停在待批准的执行节点。</summary>
    public const string ReviewPerItemFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "执行者", "Model": "fake" }
              ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                { "Name": "制定计划", "Model": "规划者", "Output": "Plan" },
                { "Name": "分配执行", "Model": "执行者", "Tools": ["GetLocalTime"], "Mode": "PerItem", "Gate": "Review", "From": ["制定计划@拆分"] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>根输入节点后接一个实施节点：回答后下游 run 消费回答。</summary>
    public const string InputFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                    { "Name": "用户输入", "Output": "Input", "Question": "请补充需要的背景", "Outputs": ["回答"] },
                    { "Name": "实施", "Output": "Text", "Model": "执行者", "From": ["用户输入@回答"] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>并行分支：一分支停在输入等待回答，另一分支 Review 容器停在待批准。</summary>
    public const string ParallelInputAndReviewFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                    {
                      "Name": "输入分支",
                      "Out": { "回答": "用户输入@回答" },
                      "Nodes": [
                        { "Name": "用户输入", "Output": "Input", "Question": "补充背景", "Outputs": ["回答"] }
                      ]
                    },
                    {
                      "Name": "审查分支",
                      "Gate": "Review",
                      "Out": { "结论": "准备@结论" },
                      "Nodes": [
                        { "Name": "准备", "Output": "Text", "Model": "执行者", "Outputs": ["结论"] }
                      ]
                    },
                    { "Name": "实施", "Output": "Text", "Model": "执行者", "From": ["输入分支@回答", "审查分支@结论"] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>两个并行输入节点：不带节点名回答时要求点名。</summary>
    public const string TwoInputFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                    { "Name": "输入一", "Output": "Input", "Question": "问题一" },
                    { "Name": "输入二", "Output": "Input", "Question": "问题二" }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>输入节点 From 上游：上游发布提供背景，输入节点提交即停驻等待回答。</summary>
    public const string InputFromUpstreamFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                    { "Name": "准备", "Output": "Text", "Model": "执行者", "Outputs": ["结论"] },
                    { "Name": "用户输入", "Output": "Input", "Question": "补充背景", "Outputs": ["回答"], "From": ["准备@结论"] },
                    { "Name": "实施", "Output": "Text", "Model": "执行者", "From": ["用户输入@回答"] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>挂点环：收话与回话互引，输入节点每轮回答发布后沿环回绕被消费，复位重挂等下一轮。</summary>
    public const string InputLoopFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                    { "Name": "收话", "Output": "Input", "Question": "请说", "Outputs": ["回答"], "From": ["回话@回复"] },
                    { "Name": "回话", "Output": "Text", "Model": "执行者", "Outputs": ["回复"], "From": ["收话@回答"] }
                  ]
                }
              ]
            }
          ]
        }
        """;
    /// <summary>声明输出端口的文本节点把命名段交给下游，下游按端口精确消费。</summary>
    public const string PortFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                    { "Name": "分析", "Output": "Text", "Model": "执行者", "Outputs": ["结论", "理由"], "Prompt": "给出结论与理由" },
                    { "Name": "实施", "Output": "Text", "Model": "执行者", "From": ["分析@结论"] },
                    { "Name": "复盘", "Output": "Text", "Model": "执行者", "From": ["分析@理由"] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>执行节点用 From 条目的 Context 标记绑定上下文输入：来源产出置于上下文开头，同时普通数据条目照常注入。</summary>
    public const string ContextInputFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                    { "Name": "准备", "Output": "Text", "Model": "执行者", "Outputs": ["结论"] },
                    { "Name": "实施", "Output": "Text", "Model": "执行者", "From": ["准备@结论", { "Node": "准备@结论", "Context": true }] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>下游消费上游的隐式上下文输出端口：上游 run 的上下文按统一结构整体注入，角色与出处保留，指令单列。</summary>
    public const string ContextOutputFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "执行者", "Model": "fake" }
              ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                    { "Name": "制定", "Output": "Plan", "Model": "规划者", "Prompt": "拆分目标" },
                    { "Name": "实施", "Output": "Text", "Model": "执行者", "Mode": "PerItem", "From": ["制定@拆分"] },
                    { "Name": "汇报", "Output": "Text", "Model": "执行者", "From": ["实施@ContextOutput"] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>声明恒定系统指令的文本节点：指令随 run 装配进请求最前的系统指令通道。</summary>
    public const string SystemPromptFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                    { "Name": "实施", "Output": "Text", "Model": "执行者", "SystemPrompt": ["固定背景一", "固定背景二"] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>库容器声明命名输出端口：装配层下游按 容器@端口 消费容器内成员的命名段。</summary>
    public const string ContainerOutFlow = """
        {
          "Nodes": [
            { "Name": "允许工具", "Tools": ["GetLocalTime"], "Mode": "PerItem", "Outputs": ["结论", "理由"] },
            {
              "Name": "交付",
              "Inputs": ["计划"],
              "Out": { "结论": "实施@结论", "理由": "实施@理由" },
              "Nodes": [
                {
                  "Name": "实施",
                  "Use": "允许工具",
                  "Model": "执行者",
                  "From": ["@计划"]
                }
              ]
            }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                    { "Name": "制定计划", "Model": "执行者", "Output": "Plan" },
                    {
                      "Name": "交付",
                      "Use": "交付",
                      "In": { "计划": "制定计划@拆分" },
                      "Models": { "执行者": "执行者" }
                    },
                    { "Name": "复盘", "Output": "Text", "Model": "执行者", "From": ["交付@结论"] }
                  ]
                }
              ]
            }
          ]
        }
        """;
}
