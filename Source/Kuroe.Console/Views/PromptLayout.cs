namespace Kuroe.Console.Views;

/// <summary>输入回显的终端布局计算。</summary>
internal static class PromptLayout
{
    /// <summary>文本从行首写入时占用的终端行数，文本为空时占一行。</summary>
    public static int Rows(int width, int textWidth)
    {
        int usable = Math.Max(1, width);
        if (textWidth == 0)
        {
            return 1;
        }

        return 1 + ((textWidth - 1) / usable);
    }
}