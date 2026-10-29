using Kuroe.Console.Views;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;

namespace Kuroe.Console.Tests.Support;

/// <summary>只声明 目标 端口的测试流程按单个目标提交任务的入口。</summary>
internal static class TaskBrowserSubmission
{
    public static void Submit(this TaskBrowser browser, FlowName flow, string goal) =>
        browser.Submit(flow, StartInputs.Single(new PortName("目标"), goal));
}