# Agent Framework 工作流

`Microsoft.Agents.AI.Workflows` 以执行器与边构成工作流，由运行期按超步推进并流式发出事件，检查点让运行可以从已完成的状态继续。Kuroe 只在 MAF 流程的装配与运行期使用它。

## 运行环境

`InProcessExecution` 提供三种环境：

| 环境 | 推进方式 | 事件时机 |
| --- | --- | --- |
| `OffThread` | 后台线程逐个超步推进 | 事件发生即发出 |
| `Lockstep` | 在事件监听线程内推进 | 每个超步完成后成批发出 |
| `Concurrent` | 后台线程，允许并发运行 | 事件发生即发出 |

`InProcessExecution.Default` 等于 `OffThread`。Kuroe 走默认环境。

## 启动与恢复

启动有两个入口，区别在于是否带检查点：

- `RunStreamingAsync<TInput>(workflow, input, sessionId)` 启动一次运行并返回 `StreamingRun`。
- `RunStreamingAsync<TInput>(workflow, input, checkpointManager, sessionId)` 在启动时启用检查点，每个超步完成后提交一次。

恢复从已有检查点继续，不重跑已完成的超步：

- `ResumeStreamingAsync(workflow, checkpointInfo, checkpointManager)`。
- `ResumeAsync(workflow, checkpointInfo, checkpointManager)`，非流式。

恢复要求传入同一个工作流实例：执行器集合与边必须与提交检查点时一致，否则检查点里的执行进度无法对应。

## 事件流

`StreamingRun.WatchStreamAsync` 按发生顺序给出事件，Kuroe 关心三类：

| 事件 | 携带 | 用途 |
| --- | --- | --- |
| `ExecutorInvokedEvent` | `ExecutorId` | 步骤开始 |
| `ExecutorCompletedEvent` | `ExecutorId` | 步骤完成 |
| `ExecutorFailedEvent` | `ExecutorId` | 步骤失败 |

`ExecutorId` 是构建工作流时为每个执行器给定的标识。Kuroe 用步骤标识作执行器标识，事件因此能还原为流程步骤。

## 检查点

`CheckpointManager.CreateJson(ICheckpointStore<JsonElement>, JsonSerializerOptions)` 把检查点序列化为 JSON 并交给存储。`CheckpointManager.CreateInMemory` 只用于进程内。

- `CheckpointInfo` 由 `SessionId` 与 `CheckpointId` 组成，恢复时两者都要给出。
- `RetrieveIndexAsync(sessionId, withParent)` 按提交顺序返回索引，最后一项是最新检查点。
- `GetLatestCheckpointAsync(sessionId)` 取该会话最新检查点，没有时返回空。

检查点含消息与执行器状态，自定义消息类型必须提供序列化选项，否则提交时序列化失败。

## 工作流构建

工作流由执行器与边构成，边在构建期建立，运行期不可增删。Kuroe 的构建约定：

- 每个步骤对应一个执行器，执行器标识取步骤标识。
- 步骤之间的连接按流程定义逐条加边，一个步骤至多一个上游。
- 步骤执行器接收上游消息并产出下游消息，消息在步骤之间传递。

Kuroe 侧对应实现：`MafWorkflowRunner` 构建并启动或恢复运行，`MafFlowCheckpoints` 把检查点写入流程状态，`MafWorkflowEventTranslator` 把运行事件还原为流程步骤事件。