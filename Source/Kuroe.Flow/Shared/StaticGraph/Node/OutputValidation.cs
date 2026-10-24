using System.Text.RegularExpressions;
using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.Shared.StaticGraph.Node;

/// <summary>输出校验谓词：决定模型产出是否合格。</summary>
public enum ValidationPredicate
{
    /// <summary>产出非空。</summary>
    NonEmpty,

    /// <summary>产出包含指定文本。</summary>
    TextContains,

    /// <summary>产出不包含指定文本。</summary>
    TextNot,

    /// <summary>产出与指定文本完全一致。</summary>
    TextEquals,

    /// <summary>产出匹配指定正则。</summary>
    Pattern,
}

/// <summary>执行节点的输出校验：收口时校验模型产出，不通过则节点阻塞待返工。</summary>
public sealed record OutputValidation(ValidationPredicate Predicate, string? Argument)
{
    /// <summary>检查输出校验的取值边界。</summary>
    public void Check(Action<string> fail)
    {
        switch (Predicate)
        {
            case ValidationPredicate.NonEmpty:
                if (Argument is not null)
                {
                    fail("NonEmpty 校验不接受参数。");
                }

                break;

            case ValidationPredicate.Pattern:
                if (string.IsNullOrEmpty(Argument))
                {
                    fail("Pattern 校验需要正则参数。");
                }
                else
                {
                    try
                    {
                        _ = new Regex(Argument, RegexOptions.None, TimeSpan.FromSeconds(1));
                    }
                    catch (RegexParseException)
                    {
                        fail($"Pattern 校验的正则不合法：{Argument}。");
                    }
                }

                break;

            default:
                if (string.IsNullOrEmpty(Argument))
                {
                    fail($"{Predicate} 校验需要指定文本参数。");
                }

                break;
        }
    }

    /// <summary>端口产出是否满足校验。按声明序拼接 Text 端口后按谓词判断。</summary>
    public bool Passes(IReadOnlyList<Port> outputs, IReadOnlyDictionary<PortName, string>? values)
    {
        string? result = Join(outputs, values);

        return Predicate switch
        {
            ValidationPredicate.NonEmpty => result is { Length: > 0 },
            ValidationPredicate.TextContains => Contains(result, Argument),
            ValidationPredicate.TextNot => result is { Length: > 0 } && !Contains(result, Argument),
            ValidationPredicate.TextEquals => string.Equals(result, Argument, StringComparison.Ordinal),
            ValidationPredicate.Pattern => Matches(result, Argument),
            _ => true,
        };
    }

    /// <summary>按声明顺序拼接 Text 端口值，端口未全部交回时为空。</summary>
    private static string? Join(IReadOnlyList<Port> outputs, IReadOnlyDictionary<PortName, string>? values)
    {
        if (values is null)
        {
            return null;
        }

        string[] parts = [.. outputs
            .Where(port => Ports.CarriesText(port.Type))
            .Select(port => values.GetValueOrDefault(port.Name))
            .Where(value => value is { Length: > 0 })
            .Cast<string>()];

        return parts.Length == 0 ? null : string.Join("\n", parts);
    }

    private static bool Contains(string? text, string? needle) =>
        needle is { Length: > 0 } && text is not null && text.Contains(needle, StringComparison.Ordinal);

    /// <summary>正则匹配：产物为空或匹配超时一律视为不通过。</summary>
    private static bool Matches(string? text, string? pattern)
    {
        if (text is null || string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        try
        {
            return Regex.IsMatch(text, pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
