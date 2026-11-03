namespace Kuroe.Flow.InputChannel.Shared;

/// <summary>收下一次外部输入文本。</summary>
/// <param name="input">外部交付的输入文本。</param>
public delegate void InputReceiver(string input);