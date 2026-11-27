using ErrorOr;
using Kuroe.Host.Ability.Node.Command;
using Kuroe.Host.Ability.Node.File;
using Kuroe.Host.Ability.Node.Llm;
using Kuroe.Host.Composition;
using Kuroe.Host.Composition.Catalogs;
using Kuroe.Host.FlowIntegration.GraphFlow;
using Kuroe.Host.FlowIntegration.GraphFlow.NodeExecution;
using Kuroe.Host.FlowIntegration.Maf;
using Kuroe.FlowDescription.Description.Shared;
using Kuroe.Kernel.Tasks.Shared;
using Kuroe.Maf.Compilation;
using Microsoft.Extensions.DependencyInjection;

namespace Kuroe.Host.FlowIntegration.Flows;

/// <summary>流程实现装配结果：没有可用实现的流程与单实现失败的提示。</summary>
public sealed record FlowIntegrationResult(
    IReadOnlyList<Error> Unavailable,
    IReadOnlyList<Error> Warnings);

/// <summary>全部流程实现的装配入口：图流程与 MAF 共同装配同一份流程描述，提交时按实现选择。</summary>
public static class FlowIntegrationServiceCollectionExtensions
{
    /// <summary>在图流程与 MAF 上各装配一次流程描述，并给出没有实现可用的流程。</summary>
    public static ErrorOr<FlowIntegrationResult> AddKuroeFlowImplementations(
        this IServiceCollection services,
        KuroeComposition composition,
        GraphFlowOptions? options = null)
    {
        GraphFlowOptions graphOptions = (options ?? new GraphFlowOptions()) with
        {
            AllowFlowFailures = true,
        };
        ErrorOr<GraphFlowComposition> graph =
            services.AddKuroeGraphFlowsComposition(composition, graphOptions);
        if (graph.IsError)
        {
            return graph.ErrorsOrEmptyList;
        }

        MafNodeExecutions executions = Executions(graph.Value.Runners);
        ErrorOr<MafDocumentFlowSource> maf = MafDocumentFlowSource.Create(
            new MafFlowAssembler(graph.Value.Compiler, executions),
            graph.Value.Library.Document);
        if (maf.IsError)
        {
            return maf.ErrorsOrEmptyList;
        }

        services.AddKuroeMafFlows(
            composition.FlowTemplates,
            maf.Value,
            provider => provider.GetRequiredService<ModelService>().Current?.Value);

        return Availability(graph.Value, maf.Value);
    }

    private static MafNodeExecutions Executions(FlowNodeExecutionRunners runners)
    {
        MafNodeExecutions executions = new();
        executions.Register(LlmNodeTypes.Name, type => LlmNodeExecution.Bind(type, runners.Llm));
        executions.Register(CommandNodeTypes.Name, type => CommandNodeExecution.Bind(type, runners.Command));
        executions.Register(FileReadNodeTypes.Name, type => FileNodeExecution.BindRead(type, runners.FileRead));
        executions.Register(FileWriteNodeTypes.Name, type => FileNodeExecution.BindWrite(type, runners.FileWrite));

        return executions;
    }

    private static FlowIntegrationResult Availability(GraphFlowComposition graph, MafDocumentFlowSource maf)
    {
        List<Error> unavailable = [];
        List<Error> warnings = [];
        HashSet<FlowName> seen = [];
        Dictionary<FlowName, IReadOnlyList<Error>> mafFailures = maf.Rejected
            .GroupBy(failure => failure.Name)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Error>)[.. group.SelectMany(failure => failure.Errors)]);
        Dictionary<FlowName, IReadOnlyList<Error>> graphFailures = graph.Library.Failures
            .GroupBy(failure => failure.Name)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Error>)[.. group.SelectMany(failure => failure.Errors)]);
        foreach (FlowEntry entry in graph.Library.Document.Flows)
        {
            ErrorOr<FlowName> name = FlowName.Create(entry.Name);
            if (name.IsError || !seen.Add(name.Value))
            {
                continue;
            }

            bool graphAvailable = graph.Catalog.FindCompiled(name.Value).IsSuccess;
            bool mafAvailable = maf.Find(name.Value).IsSuccess;
            IReadOnlyList<Error> graphErrors = graphFailures.GetValueOrDefault(name.Value, []);
            IReadOnlyList<Error> mafErrors = mafFailures.GetValueOrDefault(name.Value, []);
            if (!graphAvailable && !mafAvailable)
            {
                IReadOnlyList<Error> errors = [.. graphErrors, .. mafErrors];
                unavailable.AddRange(errors.Count > 0
                    ? errors.Select(error => Error.Validation(FlowErrorCodes.FlowAssembly,
                        $"流程 {name.Value} 没有可用实现：{error.Description}"))
                    : [Error.Validation(FlowErrorCodes.FlowAssembly, $"流程 {name.Value} 没有可用实现。")]);
                continue;
            }

            if (!graphAvailable)
            {
                warnings.AddRange(graphErrors.Select(error => Error.Validation(FlowErrorCodes.FlowAssembly,
                    $"图流程未装配 {name.Value}：{error.Description}")));
            }

            if (!mafAvailable)
            {
                warnings.AddRange(mafErrors.Select(error => Error.Validation(FlowErrorCodes.FlowAssembly,
                    $"MAF 流程未装配 {name.Value}：{error.Description}")));
            }
        }

        warnings.AddRange(graph.Library.Failures
            .Where(failure => !seen.Contains(failure.Name))
            .SelectMany(failure => failure.Errors)
            .Select(error => Error.Validation(FlowErrorCodes.FlowAssembly, $"图流程未装配：{error.Description}")));

        return new FlowIntegrationResult(unavailable, warnings);
    }
}