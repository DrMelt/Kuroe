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
                { "Name": "分配执行", "Model": "执行者", "Tools": ["GetLocalTime"], "Mode": "PerItem", "Gate": "Review", "From": ["制定计划"] }
                  ]
                }
              ]
            }
          ]
        }
        """;
}