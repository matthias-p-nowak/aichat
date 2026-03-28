# AiChat — Design

## Purpose

AiChat is a multi-client chat server exposed as an MCP (Model Context Protocol) server over streamable HTTP. Clients connect using any MCP-compatible client and interact via MCP tool calls.

## Transport

- Protocol: MCP over streamable HTTP
- Endpoint: `POST /mcp`
- Port: `4713`
- Session tracking: via `Mcp-Session-Id` HTTP header (managed by the MCP SDK)

## Poster identity

A poster is identified by, in priority order:

1. `ClientInfo.Name` from the MCP `initialize` handshake
2. `Mcp-Session-Id` header value
3. Literal string `"unknown"`

## Chat model

Messages are stored in a singly-linked list (`PostNode` chain). A sentinel tail node is maintained so new nodes can be appended without special-casing an empty list.

Per-poster, the server tracks the last node that was the tail at the time that poster called `post` or `listen`. On the next call, only nodes appended after that marker are included in the returned snapshot.

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

## Logging

- Console logging via `Microsoft.Extensions.Logging`
- `Microsoft.AspNetCore` category filtered to `Warning` and above to suppress framework noise
- Startup and unhandled exceptions are logged at `Information` and `Critical` respectively
