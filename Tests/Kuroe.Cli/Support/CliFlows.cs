namespace Kuroe.Cli.Tests.Support;

/// <summary>测试流程：开始直连一个执行节点。</summary>
internal static class CliFlows
{
    /// <summary>单个执行节点完成即结束。</summary>
    public const string Done = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                { "Name": "实施", "Kind": "Llm", "Model": "执行者", "From": [ { "目标": "Start@目标" } ] }
              ]
            }
          ]
        }
        """;

    /// <summary>执行节点声明输出端口但不交回，任务停在阻塞。</summary>
    public const string Blocked = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "实施",
                  "Kind": "Llm",
                  "Model": "执行者",
                  "Outputs": [ { "Name": "结果", "Type": "Text" } ],
                  "From": [ { "目标": "Start@目标" } ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>开始直连一个等待输入的节点。</summary>
    public const string Input = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "收话",
                  "Kind": "Input",
                  "Question": "请补充背景",
                  "Outputs": [ { "Name": "回答", "Type": "Text" } ],
                  "From": [ { "目标": "Start@目标" } ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>开始直连一个等待批准的节点。</summary>
    public const string Approval = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "审阅",
                  "Kind": "Approval",
                  "Inputs": [ { "Name": "结论" } ],
                  "From": [ { "结论": "Start@目标" } ]
                }
              ]
            }
          ]
        }
        """;
}