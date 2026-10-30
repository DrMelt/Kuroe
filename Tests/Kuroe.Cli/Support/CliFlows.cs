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
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] },
              "Nodes": [
                { "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "Start@MODEL_NAME" }, "From": [ { "目标": "Start@目标" } ] }
              ]
            }
          ]
        }
        """;

    /// <summary>开始声明两个输入端口，执行节点把两者都接进输入。</summary>
    public const string MultiStart = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "风格", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] },
              "Nodes": [
                {
                  "Name": "实施",
                  "Kind": "Llm",
                  "Static": { "MODEL_NAME": "Start@MODEL_NAME" },
                  "Inputs": [ { "Name": "目标" }, { "Name": "风格" } ],
                  "From": [ { "目标": "Start@目标", "风格": "Start@风格" } ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>开始声明一个绑定宿主输入的开始端口，执行节点把它接进输入。</summary>
    public const string HostInput = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": {
                "Outputs": [
                  { "Name": "目标", "Type": "Text" },
                  { "Name": "启动路径", "Type": "Text", "Constant": true },
                  { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }
                ]
              },
              "HostStartInputs": { "启动路径": "WORKSPACE_PATH" },
              "Nodes": [
                {
                  "Name": "实施",
                  "Kind": "Llm",
                  "Static": { "MODEL_NAME": "Start@MODEL_NAME", "启动路径": "Start@启动路径" },
                  "Inputs": [ { "Name": "目标" }, { "Name": "启动路径", "Type": "Text", "Static": true } ],
                  "From": [ { "目标": "Start@目标" } ]
                }
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
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] },
              "Nodes": [
                {
                  "Name": "实施",
                  "Kind": "Llm",
                  "Static": { "MODEL_NAME": "Start@MODEL_NAME" },
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