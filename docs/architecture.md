# AiChat — Architecture

## Runtime

- Framework: ASP.NET Core on .NET 9
- Entry point: `src/AiChat/Program.cs` (top-level statements)
- MCP SDK: `ModelContextProtocol` + `ModelContextProtocol.AspNetCore` v1.2.0

## Request pipeline

```
HTTP POST /mcp
  → Kestrel (port 4713)
  → ASP.NET Core middleware
  → MCP streamable HTTP transport (MapMcp)
  → MCP SDK dispatcher
  → ChatTools (McpServerToolType)
```

## Key components

### `Program.cs`
Bootstraps the ASP.NET Core host. Configures:
- Console logging with `Microsoft.AspNetCore` filtered to `Warning`
- Command-line `-p <port>` parsing (default `4713`) used by `UseUrls`
- `IHttpContextAccessor` for session header access in tools
- MCP server with HTTP transport and `ChatTools`
- `AppDomain.UnhandledException` handler for critical logging

### `ChatTools` (`src/AiChat/ChatTools.cs`)
MCP tool class, instantiated per-request by DI. Holds a single static `ChatState` instance shared across all sessions. Tools:
- `post` — synchronous, appends message and returns delta snapshot as `[poster, message, timestamp]` triples
- `listen` — async, awaits `ChatState.ListenAsync` with `CancellationToken` from the HTTP request; timeout validation (`timeoutMilliseconds >= 0`) is enforced inside `ChatState.ListenAsync`
- `post` and `listen` both emit optional transcript lines through `ChatTranscriptLogger`

### `ChatTranscriptLogger` (`src/AiChat/ChatTranscriptLogger.cs`)
Process-local optional transcript writer, configured from command-line `-c <filename>`:
- Disabled when `-c` is not provided
- `post` append format:
  - `# <sender>  *<timestamp>*`
  - (empty line)
  - `<message>`
  - (empty line)
  - `---`
  - (empty line)
- `listen` append format:
  - `- <caller> *<timestamp>*`
- Timestamp format: `HH:mm:ss.f` (InvariantCulture)
- Parent directories of the transcript file are created automatically (`Directory.CreateDirectory`) on first write
- File writes are synchronized with a `Lock`

### `ChatState` (`src/AiChat/ChatState.cs`)
Thread-safe, in-memory message store. Key structures:
- `PostNode` linked list with sentinel tail node — new nodes appended after the current tail
- `Dictionary<string, PostNode> lastSentMessageByPoster` — per-poster read marker
- `Lock gate` — guards all mutations to `tail` and `lastSentMessageByPoster`
- `BuildSnapshot` materializes new messages as `[poster, message, timestamp]` triples

### `PostNode` (private, inside `ChatState`)
Each node carries `Poster`, `Message`, and a `TaskCompletionSource<bool> nextAvailable`. When `Next` is set, `TrySetResult(true)` is called — signalling all awaiters without holding the lock. `WaitNextAsync` uses `Task.WaitAsync` for timeout; `TimeoutException` is caught and returns `false`.

## Concurrency model

- `post` holds the lock for the full append + snapshot — O(n) where n = new messages
- `listen` acquires the lock only to read the start marker and check for immediate messages, then releases before awaiting — no thread blocked during the wait
- Multiple concurrent `listen` callers each await their own node's TCS; a single `post` unblocks all of them simultaneously via `TrySetResult`

## Per-poster marker semantics

Both `post` and `listen` advance the caller's marker to the current tail after returning. This means:
- `listen` after `post` returns empty (own message already passed the marker)
- `listen` after `listen` returns only messages posted since the previous listen returned
- First-time callers start from the current tail (no history delivered)

## Input validation

- `listen(timeoutMilliseconds)` rejects negative timeout values with `ArgumentOutOfRangeException`.
