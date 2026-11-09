using System.Collections.ObjectModel;
using ErrorOr;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Workflows.Flows;

/// <summary>宿主维护的开始输入表：一条流程一项，键是开始端口名，值是宿主输入名。
/// 表项之外的开始端口按用户输入处理，表项之内的开始端口由宿主给值，不依赖端口命名。
/// 表项与流程开始入口的一致性在任务提交时校验，流程编译不涉及这份配置。</summary>
public sealed class HostStartInputs(
    IReadOnlyDictionary<FlowName, IReadOnlyDictionary<PortName, string>> entries,
    IReadOnlySet<string> supportedInputs) : IHostStartInputs
{
    private static readonly IReadOnlyDictionary<PortName, string> Empty =
        new ReadOnlyDictionary<PortName, string>(new Dictionary<PortName, string>());

    private readonly IReadOnlyDictionary<FlowName, IReadOnlyDictionary<PortName, string>> _entries = entries;
    private readonly IReadOnlySet<string> _supportedInputs = supportedInputs;

    /// <summary>由文件形状的表项建立输入表，流程名或端口名不合法时一次给出全部错误。</summary>
    public static ErrorOr<HostStartInputs> Create(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> entries,
        IReadOnlySet<string> supportedInputs)
    {
        List<Error> errors = [];
        Dictionary<FlowName, IReadOnlyDictionary<PortName, string>> parsed = [];
        foreach ((string name, IReadOnlyDictionary<string, string> entry) in entries)
        {
            ErrorOr<FlowName> flow = FlowName.Create(name);
            if (flow.IsError)
            {
                errors.Add(HostStartInputsErrors.InvalidFlow(name));
                continue;
            }

            Dictionary<PortName, string> ports = [];
            foreach ((string port, string input) in entry)
            {
                ErrorOr<PortName> parsedPort = PortName.Create(port);
                if (parsedPort.IsError)
                {
                    errors.Add(HostStartInputsErrors.InvalidPort(name, port));
                    continue;
                }

                ports[parsedPort.Value] = input;
            }

            parsed[flow.Value] = ports;
        }

        return errors.Count > 0 ? errors : new HostStartInputs(parsed, supportedInputs);
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<PortName, string> Bindings(FlowName flow) =>
        _entries.TryGetValue(flow, out IReadOnlyDictionary<PortName, string>? entry) ? entry : Empty;

    /// <inheritdoc/>
    public ErrorOr<IReadOnlyDictionary<PortName, string>> Resolve(FlowDefinition flow)
    {
        if (!_entries.TryGetValue(flow.Name, out IReadOnlyDictionary<PortName, string>? entry))
        {
            return new Dictionary<PortName, string>();
        }

        List<Error> errors = [];
        Dictionary<PortName, string> bindings = [];
        foreach ((PortName port, string input) in entry)
        {
            if (flow.StartOutputs.Find(port) is not { } declared)
            {
                errors.Add(HostStartInputsErrors.UndeclaredPort(flow.Name.Value, port.Value));
                continue;
            }

            if (declared.Value is not null)
            {
                errors.Add(HostStartInputsErrors.ConstantPort(flow.Name.Value, port.Value));
                continue;
            }

            if (!PortRules.CarriesText(declared.Type))
            {
                errors.Add(HostStartInputsErrors.NonTextPort(flow.Name.Value, port.Value));
                continue;
            }

            if (!_supportedInputs.Contains(input))
            {
                errors.Add(HostStartInputsErrors.UnsupportedInput(flow.Name.Value, port.Value, input));
                continue;
            }

            bindings[port] = input;
        }

        return errors.Count > 0 ? errors : bindings;
    }
}