namespace Kuroe.Console.Tests.Support;

/// <summary>测试共享的流程模板 JSON。</summary>
public static class TestFlows
{
    /// <summary>审批节点把计划放行给执行节点。</summary>
    public const string ReviewFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
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
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
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
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
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
                      "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "@目标", "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
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

    /// <summary>开始声明目标与风格两个输入端口，执行节点按端口接两个输入。</summary>
    public const string MultiStartFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "风格", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "风格" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "Start@目标", "风格": "Start@风格", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                    {
                      "Name": "实施",
                      "Kind": "Llm",
                      "Static": { "MODEL_NAME": "@MODEL_NAME" },
                      "Inputs": [{ "Name": "目标" }, { "Name": "风格" }],
                      "From": [{ "目标": "@目标", "风格": "@风格" }]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>开始端口带固定值：固定端口只作为上下文，不带值的端口才询问。</summary>
    public const string FixedStartFlow = """
        {
          "Flows": [
            {
              "Name": "固定",
              "Start": { "Outputs": [
                { "Name": "目标", "Type": "Text" },
                { "Name": "风格", "Type": "Text", "Value": "简洁" },
                { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] },
              "Nodes": [
                {
                  "Name": "实施",
                  "Kind": "Llm",
                  "Static": { "MODEL_NAME": "Start@MODEL_NAME" },
                  "Inputs": [{ "Name": "目标" }, { "Name": "风格" }],
                  "From": [{ "目标": "Start@目标", "风格": "Start@风格" }]
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

    /// <summary>带校验的执行节点产出不含通过，任务停在阻塞，用于返工命令的装配。</summary>
    public const string BlockingFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                    { "Name": "计划", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                    { "Name": "修订", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "Validate": { "Predicate": "TextContains", "Argument": "通过" }, "From": [ { "结论": "计划@结论" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;
}
