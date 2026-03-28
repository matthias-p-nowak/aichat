---
name: aichat
description: Use when communicating with other agents via the aichat MCP server — to post messages, long-poll for replies, or coordinate tasks using the post and listen tools.
---

# AiChat Agent Skill

Use this skill when you need to communicate with another agent via the aichat MCP server.

## Tools

- `post(message)` — send a message; returns new messages since your last `post` or `listen` (may include others' messages posted since then, plus your own new post is now past your marker)
- `listen(timeoutMilliseconds)` — wait for new messages without posting; returns when a message arrives or timeout expires; may return empty

## Your identity

Your name in the chat is set by the `name` field in your MCP `initialize` request (i.e. the client name configured in your MCP connection). All your messages will be attributed to that name.

## Key behaviour to know

- **No history on connect** — first-time callers start from the current tail and only see messages posted *after* they connect. No history is delivered.
- **`post` then immediate `listen` returns empty** — posting advances your read marker past your own message. Use `listen` to wait for *others* to reply.
- **`listen` timeout is not a failure** — if `listen` returns `[]`, the peer hasn't posted yet. Loop with another `listen`; don't assume the peer is idle or failed.
- Both tools return `[poster, message]` pairs. Check the `poster` field to know who sent each message.

## Typical patterns

### Send a message and wait for a reply

```
post("your message here")
listen(30000)   # wait up to 30s for a response
# if empty, loop:
listen(30000)
```

### Announce yourself and start a conversation

```
post("Hi @<other agent> — I'm <your name>. <your question or task>")
listen(30000)
# read replies, respond, repeat
```

### Receive-only / monitoring loop

```
listen(30000)   # wait for any message
# process messages
listen(30000)   # wait for next
```

### Coordinate a task split

```
post("@<other> I'll handle X. Can you take Y? Reply when done.")
# do X
listen(60000)   # wait for other agent to confirm Y done
```

## Conventions

- **Prefix with `@name`** — in rooms with more than two participants, always prefix messages with `@recipient` (e.g. `@claude`, `@codex`) to avoid ambiguous handoffs.
- **Be explicit about handoffs** — say "your turn" or "standing by" so the other agent knows when to act.
- **Confirm receipt** — reply with a short ack when you receive a task, so the sender knows the message landed.
- **Sync before acting** — when coordinating parallel work, agree on the split via chat before starting, to avoid duplicate effort.
- **Report back** — when you finish your part, post a summary so the other agent and the user are informed.

## Timeouts

| Situation | Suggested timeout |
|---|---|
| Quick ack expected | 10 000 ms |
| Interactive response | 30 000 ms |
| Agent doing real work | 60 000 – 120 000 ms |
| Idle monitoring | 30 000 ms (loop) |

## Troubleshooting

- **No replies** — verify your session identity (check `poster` in your own posts), confirm the timeout is long enough, and confirm the peer actually posted after you connected.
- **Empty returns repeatedly** — the peer may not be active. Check if they are connected via a short `post` asking for an ack.
- **Messages from wrong session** — your identity comes from your MCP client name; ensure it's set correctly before connecting.

## Example exchange

```
# Agent A
post("@codex — can you implement X? I'll do Y.")
listen(30000)
# → [["codex-mcp-client", "Sure, starting on X now."]]

# Agent A does Y, then:
post("@codex Y is done. Build passes on my end.")
listen(60000)
# → [["codex-mcp-client", "X done too. Tests pass."]]

post("All good — reporting back to user.")
```
