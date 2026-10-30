namespace Kuroe.Tests.Support;

/// <summary>测试共享的流程模板 JSON。</summary>
public static class TestFlows
{
    /// <summary>审批节点把计划放行给执行节点。</summary>
    public const string ReviewFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [
                { "Name": "目标", "Type": "Text" },
                { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }
              ] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
                  "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                { "Name": "制定计划", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                { "Name": "审阅", "Kind": "Approval", "Question": "确认计划", "Inputs": [{ "Name": "计划", "Type": "Signal" }], "From": [ { "计划": "制定计划@计划" } ] },
                { "Name": "分配执行", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Tools": ["GetLocalTime"], "Inputs": [{ "Name": "任务" }, { "Name": "批准", "Type": "Signal" }], "From": [ { "任务": "制定计划@计划", "批准": "审阅@计划" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>根输入节点后接一个实施节点：回答后下游执行消费回答。</summary>
    public const string InputFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [
                { "Name": "目标", "Type": "Text" },
                { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }
              ] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                    { "Name": "用户输入", "Kind": "Input", "Question": "请补充需要的背景", "Outputs": [{ "Name": "回答", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                    { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "回答": "用户输入@回答" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>并行分支：一分支停在输入等待回答，另一分支经审批节点停在待批准。</summary>
    public const string ParallelInputAndReviewFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [
                { "Name": "目标", "Type": "Text" },
                { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }
              ] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                    {
                      "Name": "输入分支",
                      "Kind": "Container",
                      "Outputs": [{ "Name": "回答", "Ref": "用户输入@回答" }],
                      "Inputs": [{ "Name": "目标" }], "In": { "目标": "@目标" }, "Nodes": [
                        { "Name": "用户输入", "Kind": "Input", "Question": "补充背景", "Outputs": [{ "Name": "回答", "Type": "Text" }], "From": [ { "目标": "@目标" } ] }
                      ]
                    },
                    {
                      "Name": "审查分支",
                      "Kind": "Container",
                      "Outputs": [{ "Name": "结论", "Ref": "准备@结论" }],
                      "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "@目标", "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
                        { "Name": "准备", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] }
                      ]
                    },
                    { "Name": "审阅", "Kind": "Approval", "Inputs": [{ "Name": "结论" }], "From": [ { "结论": "审查分支@结论" } ] },
                    { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "回答": "输入分支@回答", "结论": "审阅@结论" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>两个并行输入节点：不带节点路径回答时要求点名。</summary>
    public const string TwoInputFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }], "In": { "目标": "Start@目标" }, "Nodes": [
                    { "Name": "输入一", "Kind": "Input", "Question": "问题一", "From": [ { "目标": "@目标" } ] },
                    { "Name": "输入二", "Kind": "Input", "Question": "问题二", "From": [ { "目标": "@目标" } ] }
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
              "Start": { "Outputs": [
                { "Name": "目标", "Type": "Text" },
                { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }
              ] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                    { "Name": "准备", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                    { "Name": "用户输入", "Kind": "Input", "Question": "补充背景", "Outputs": [{ "Name": "回答", "Type": "Text" }], "From": [ { "结论": "准备@结论" } ] },
                    { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "回答": "用户输入@回答" } ] }
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
              "Start": { "Outputs": [
                { "Name": "目标", "Type": "Text" },
                { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }
              ] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                    { "Name": "收话", "Kind": "Input", "Question": "请说", "Outputs": [{ "Name": "回答", "Type": "Text" }], "From": [ { "目标": "@目标", "回复": "回话@回复" } ] },
                    { "Name": "回话", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "回复", "Type": "Text" }], "From": [ { "回答": "收话@回答" } ] }
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
              "Start": { "Outputs": [
                { "Name": "目标", "Type": "Text" },
                { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }
              ] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                    { "Name": "分析", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }, { "Name": "理由", "Type": "Text" }], "Prompt": "给出结论与理由", "From": [ { "目标": "@目标" } ] },
                    { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "结论": "分析@结论" } ] },
                    { "Name": "复盘", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "理由": "分析@理由" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>Signal 输出端口只交回端口名：下游的 Text 输入取它，取值不承载内容。</summary>
    public const string SignalPortFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [
                { "Name": "目标", "Type": "Text" },
                { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }
              ] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                    { "Name": "动手", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "完成", "Type": "Signal" }], "From": [ { "目标": "@目标" } ] },
                    { "Name": "收尾", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "标记" }], "From": [ { "标记": "动手@完成" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>执行节点用 Context 输入端口的声明绑定上下文输入：来源产出置于上下文开头，同时普通 Text 输入照常注入。</summary>
    public const string ContextInputFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [
                { "Name": "目标", "Type": "Text" },
                { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }
              ] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                    { "Name": "准备", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                    { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Inputs": [{ "Name": "结论" }, { "Name": "上下文", "Type": "Context" }], "From": [ { "结论": "准备@结论", "上下文": "准备@结论" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>下游消费上游声明的 Context 类型端口：上游执行的输入取值按统一结构整体注入。</summary>
    public const string ContextPortFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [
                { "Name": "目标", "Type": "Text" },
                { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }
              ] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                    { "Name": "制定", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Prompt": "写出计划", "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                    { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "上下文", "Type": "Context" }], "From": [ { "计划": "制定@计划" } ] },
                    { "Name": "汇报", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "上下文": "实施@上下文" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>声明恒定系统指令的文本节点：指令随执行装配进请求最前的系统指令通道。</summary>
    public const string SystemPromptFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [
                { "Name": "目标", "Type": "Text" },
                { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }
              ] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                    { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "SystemPrompt": ["固定背景一", "固定背景二"], "From": [ { "目标": "@目标" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>库容器声明命名输出端口：装配层下游按 容器@端口 消费容器内成员的命名段。</summary>
    public const string ContainerOutputsFlow = """
        {
          "Nodes": [
            { "Name": "允许工具", "Kind": "Llm", "Tools": ["GetLocalTime"], "Outputs": [{ "Name": "结论", "Type": "Text" }, { "Name": "理由", "Type": "Text" }] },
            {
              "Name": "交付",
              "Kind": "Container",
              "Inputs": [{ "Name": "计划" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
              "Outputs": [{ "Name": "结论", "Ref": "实施@结论" }, { "Name": "理由", "Ref": "实施@理由" }],
              "Nodes": [
                {
                  "Label": "实施",
                  "Use": "允许工具",
                  "Static": { "MODEL_NAME": "@MODEL_NAME" },
                  "From": [ { "计划": "@计划" } ]
                }
              ]
            }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [
                { "Name": "目标", "Type": "Text" },
                { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }
              ] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                    { "Name": "制定计划", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                    {
                      "Label": "交付",
                      "Use": "交付",
                      "In": { "计划": "制定计划@计划", "MODEL_NAME": "@MODEL_NAME" }
                    },
                    { "Name": "复盘", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "结论": "交付@结论" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;
}
