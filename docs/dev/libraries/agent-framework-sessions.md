# Agent Framework 会话

`AgentSession` 承载一次对话的模型侧状态，可序列化为 JSON 并从中恢复。Kuroe 用会话快照作为 Llm 执行的恢复数据。

## 会话归属

`AgentSession` 由创建它的 `AIAgent` 附加行为，不能跨 agent 复用。会话本身只保存状态，模型请求由 agent 发出：

- `ChatClientAgent` 把会话交给聊天客户端并驱动一轮请求。
- `InMemoryChatHistoryProvider` 把消息历史存在会话里。
- `AIAgent.CreateSessionAsync` 建立新会话。

## 序列化

会话是持久化单位，接口是：

- `AIAgent.SerializeSessionAsync(session, options, cancellationToken)` 返回 `JsonElement`。
- `AIAgent.DeserializeSessionAsync(json, options, cancellationToken)` 还原会话。

序列化内容是对话历史、会话标识与工具调用记录，属于敏感数据。恢复可信度等同于接受外部输入，存储必须按敏感数据保护。

自定义内容类型要求传入相同的 `JsonSerializerOptions`，否则反序列化后内容类型丢失。

## 读取历史

`AgentSession.TryGetInMemoryChatHistory(out List<ChatMessage>)` 直接给出内存中的消息列表，读取不触发模型请求。历史提供者可写回列表，Kuroe 在每轮结束后规范消息。

## 恢复判定

会话快照只在历史末尾可以继续时保存。判定规则：

- 存在未配对的功能调用时不可继续。
- 最后一条是用户或工具消息时可以继续。
- 最后一条是助手消息时，要求没有功能调用且包含文本或思考内容。

## Kuroe 的用法

`Session` 每轮把输入写入历史后调用 `RunAsync`，请求失败时本轮输入留在历史里，由丢弃本轮回到请求前。每轮结束规范消息：去掉用量内容与空内容，合并连续助手消息，本轮没有文本回复且不是恢复执行时整轮丢弃。

恢复数据由 `AgentSessionCodec` 编解码，格式名为 `llm.agent.session.v1`，负载是会话 JSON 与所用模型。恢复时按负载中的模型重建 agent 再还原会话，历史读取只读快照，不接触聊天客户端。