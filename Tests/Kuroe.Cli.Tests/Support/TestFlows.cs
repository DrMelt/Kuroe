namespace Kuroe.Cli.Tests.Support;

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
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                { "Name": "制定计划", "Kind": "Executable", "Model": "规划者", "Outputs": [{ "Name": "拆分", "Type": "WorkItems" }], "From": [ { "目标": "Start@目标" } ] },
                { "Name": "分配执行", "Kind": "Executable", "Model": "执行者", "Tools": ["GetLocalTime"], "Mode": "PerItem", "Gate": "Review", "From": [ { "拆分": "制定计划@拆分" } ] }
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
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Name": "用户输入", "Kind": "Input", "Question": "请补充需要的背景", "Outputs": [{ "Name": "回答", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
                    { "Name": "实施", "Kind": "Executable", "Model": "执行者", "From": [ { "回答": "用户输入@回答" } ] }
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
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    {
                      "Name": "输入分支",
                      "Kind": "Container",
                      "Outputs": [{ "Name": "回答", "Ref": "用户输入@回答" }],
                      "Nodes": [
                        { "Name": "用户输入", "Kind": "Input", "Question": "补充背景", "Outputs": [{ "Name": "回答", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] }
                      ]
                    },
                    {
                      "Name": "审查分支",
                      "Kind": "Container",
                      "Gate": "Review",
                      "Outputs": [{ "Name": "结论", "Ref": "准备@结论" }],
                      "Nodes": [
                        { "Name": "准备", "Kind": "Executable", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] }
                      ]
                    },
                    { "Name": "实施", "Kind": "Executable", "Model": "执行者", "From": [ { "回答": "输入分支@回答", "结论": "审查分支@结论" } ] }
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
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Name": "输入一", "Kind": "Input", "Question": "问题一", "From": [ { "目标": "Start@目标" } ] },
                    { "Name": "输入二", "Kind": "Input", "Question": "问题二", "From": [ { "目标": "Start@目标" } ] }
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
              "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Name": "计划", "Kind": "Executable", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
                    { "Name": "修订", "Kind": "Executable", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "Validate": { "Predicate": "TextContains", "Argument": "通过" }, "From": [ { "结论": "计划@结论" } ] }
                  ]
                }
              ]
            }
          ]
        }
        """;
}
