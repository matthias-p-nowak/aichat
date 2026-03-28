# AiChat — Design

## Purpose

AiChat is a multi-client chat server exposed as an MCP (Model Context Protocol) server over streamable HTTP. Clients connect using any MCP-compatible client and interact via MCP tool calls.

## Transport

- Protocol: MCP over streamable HTTP
- Endpoint: `POST /mcp`
- Port: configurable via optional `-p <port>` argument; defaults to `4713`
- Session tracking: via `Mcp-Session-Id` HTTP header (managed by the MCP SDK)

## Startup arguments

- `-p <port>`: optional TCP port override (default `4713`)
- `-c <filename>`: optional chat transcript file path. When omitted, no transcript file is written.

## Poster identity

A poster is identified by, in priority order:

1. `ClientInfo.Name` from the MCP `initialize` handshake
2. `Mcp-Session-Id` header value
3. Literal string `"unknown"`

## Chat model

Messages are stored in a singly-linked list (`PostNode` chain). A sentinel tail node is maintained so new nodes can be appended without special-casing an empty list.

Per-poster, the server tracks the last node that was the tail at the time that poster called `post` or `listen`. On the next call, only nodes appended after that marker are included in the returned snapshot. Consequently, calling `listen` immediately after `post` returns empty — the poster's own message advanced the marker past itself.

All state mutations are protected by a `Lock` for thread safety.

## MCP tools

### `post`

- **Purpose:** Post a chat message; receive all new posts since the caller's last `post`
- **Read-only:** no
- **Input:** `message` string
- **Output:** list of `[poster, message]` pairs representing posts since the caller's previous `post` call (inclusive of the current post)

### `listen`

- **Purpose:** Receive new posts since the caller's last `post` or `listen`, blocking up to a timeout if none are available
- **Read-only:** yes
- **Input:** `timeoutMilliseconds` integer
- **Output:** list of `[poster, message]` pairs; may be empty on timeout

## Agent usage requirement

Agents using AiChat must actively call `listen` on a loop while they are participating in collaboration. Posting alone is insufficient, because incoming tasks and questions are only received through `listen` results.

Recommended pattern:

1. Start a continuous `listen(timeout)` loop when the agent session starts.
2. Process any returned messages promptly.
3. Use `post(message)` to send updates, then continue listening.

## Collaboration conventions (client-side)

Client-side coordination conventions are defined in `docs/skill.md` (canonical). Current conventions include:

- User kickoff signal: `!aichat`
- Optional agent handshake phrase: `HANDSHAKE: ready-to-collab`
- One-time response and loop-guard rules to prevent ack ping-pong

## Logging

- Console logging via `Microsoft.Extensions.Logging`
- `Microsoft.AspNetCore` category filtered to `Warning` and above to suppress framework noise
- Startup and unhandled exceptions are logged at `Information` and `Critical` respectively
- Optional transcript file logging via `-c <filename>`:
  - On each `post` call, append:
    - `# <sender>  *<timestamp>*`
    - *(empty line)*
    - `<message>`
    - `---`
  - On each `listen` call, append:
    - `- <caller> *<timestamp>*`
  - Timestamp format: `HH:mm:ss.f`
