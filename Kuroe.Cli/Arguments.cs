namespace Kuroe.Cli;

/// <summary>带引号参数的空白拆分：双引号内的空白算一个参数项，引号本身不保留，未闭合的引号按到末尾处理。</summary>
internal static class Arguments
{
    /// <summary>按空白拆分参数项。</summary>
    public static string[] Split(string input)
    {
        List<string> parts = [];
        System.Text.StringBuilder part = new();
        bool quoted = false;
        bool started = false;

        foreach (char character in input)
        {
            if (character == '"')
            {
                quoted = !quoted;
                started = true;
            }
            else if (!quoted && character is ' ' or '\t')
            {
                if (started)
                {
                    parts.Add(part.ToString());
                    part.Clear();
                    started = false;
                }
            }
            else
            {
                part.Append(character);
                started = true;
            }
        }

        if (started)
        {
            parts.Add(part.ToString());
        }

        return [.. parts];
    }
}
