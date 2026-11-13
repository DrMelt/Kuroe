using Kuroe.Flow.Tasks.Shared;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Kuroe.Flow.Persistence.Serialization.Shared;
using Kuroe.Flow.Tasks.Records.Shared;

namespace Kuroe.Flow.Persistence.Code;

/// <summary>代码流程记录的存档编解码：定义与状态各自成为一段 JSON 片段。</summary>
public sealed class CodeFlowRecordCodec : IFlowRecordCodec
{
    /// <inheritdoc/>
    public bool Handles(FlowIdentity identity) => identity.Kind == FlowRecordKinds.Code;

    /// <inheritdoc/>
    public IFlowDefinitionRecord ReadDefinition(FlowRecordData data)
    {
        CodeFlowDefinitionData dto = Read(
            data.Definition,
            CodeFlowRecordJson.Default.CodeFlowDefinitionData,
            "代码流程定义");

        return new CodeFlowDefinitionRecord(
            RequiredText(dto.Implementation, "代码流程实现"),
            dto.Values ?? []);
    }

    /// <inheritdoc/>
    public IFlowStateRecord ReadState(FlowRecordData data)
    {
        CodeFlowStateData dto = Read(
            data.State,
            CodeFlowRecordJson.Default.CodeFlowStateData,
            "代码流程状态");
        TaskStartData start = dto.Start ?? throw new FormatException("存档缺少开始状态。");

        return new CodeFlowStateRecord(
            new StartInputs(
            [
                .. (start.Inputs ?? []).Select(input => new StartInput(
                    new PortName(RequiredText(input.Port, "开始输入端口")),
                    RequiredText(input.Text, "开始输入文本"))),
            ]),
            new FlowTexts(ReadTexts(start.StartTexts)),
            dto.Values ?? []);
    }

    /// <inheritdoc/>
    public FlowRecordData Write(IFlowDefinitionRecord definition, IFlowStateRecord state)
    {
        if (definition is not CodeFlowDefinitionRecord codeDefinition
            || state is not CodeFlowStateRecord codeState)
        {
            throw new NotSupportedException("代码流程编解码收到非代码流程记录。");
        }

        return new FlowRecordData(
            JsonSerializer.SerializeToElement(
                new CodeFlowDefinitionData(codeDefinition.Implementation, ToData(codeDefinition.Values)),
                CodeFlowRecordJson.Default.CodeFlowDefinitionData),
            JsonSerializer.SerializeToElement(
                new CodeFlowStateData(
                    new TaskStartData(
                        [.. codeState.Inputs.Values.Select(input => new StartInputData(input.Port.Value, input.Text))],
                        codeState.StartTexts.Texts.ToDictionary(pair => pair.Key.Value, pair => pair.Value)),
                    ToData(codeState.Values)),
                CodeFlowRecordJson.Default.CodeFlowStateData));
    }

    private static T Read<T>(JsonElement element, JsonTypeInfo<T> info, string field)
    {
        try
        {
            return element.Deserialize(info) ?? throw new FormatException($"存档缺少{field}。");
        }
        catch (JsonException ex)
        {
            throw new FormatException($"{field}不合法：{ex.Message}");
        }
    }

    private static Dictionary<string, string> ToData(IReadOnlyDictionary<string, string> values) =>
        values.ToDictionary(pair => pair.Key, pair => pair.Value);

    private static Dictionary<PortName, string> ReadTexts(Dictionary<string, string>? values) =>
        values is null
            ? []
            : values.ToDictionary(pair => new PortName(pair.Key), pair => pair.Value);

    private static string RequiredText(string? text, string field) =>
        string.IsNullOrWhiteSpace(text)
            ? throw new FormatException($"存档缺少{field}。")
            : text;
}