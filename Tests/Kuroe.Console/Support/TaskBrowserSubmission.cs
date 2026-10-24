using Kuroe.Console.Views;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Console.Tests.Support;

/// <summary>只声明 目标 端口的测试流程按单个目标提交任务的入口。</summary>
internal static class TaskBrowserSubmission
{
    public static void Submit(this TaskBrowser browser, FlowName flow, string goal) =>
        browser.Submit(flow, StartInputs.Single(new PortName("目标"), goal));
}