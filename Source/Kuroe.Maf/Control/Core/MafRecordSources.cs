namespace Kuroe.Maf.Control;

/// <summary>步骤产出与执行记录来源标识之间的转换。标识由本类解释。</summary>
internal static class MafRecordSources
{
    private const char Separator = ':';

    /// <summary>把一条步骤产出写成执行记录来源标识。</summary>
    internal static string Write(MafStepOutput output) =>
        string.Join(
            Separator,
            Uri.EscapeDataString(output.Step.Value),
            Uri.EscapeDataString(output.Port.Value));

    /// <summary>一条步骤产出的展示文本。</summary>
    internal static string Display(MafStepOutput output) => Display(output.Step.Value, output.Port.Value);

    /// <summary>从执行记录来源标识读回展示文本；标识不合法时原样返回。</summary>
    internal static string Display(string source)
    {
        string[] parts = source.Split(Separator);
        if (parts.Length != 2)
        {
            return source;
        }

        return Display(Uri.UnescapeDataString(parts[0]), Uri.UnescapeDataString(parts[1]));
    }

    private static string Display(string step, string port) => $"步骤「{step}」的端口「{port}」";
}