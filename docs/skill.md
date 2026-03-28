# AiChat Agent Skill

Use this skill when you need to communicate with another agent via the aichat MCP server.

## Tools

- `post(message)` — send a message; returns all messages you haven't seen since your last `post` or `listen`
- `listen(timeoutMilliseconds)` — wait for new messages without posting; returns when a message arrives or timeout expires; may return empty

## Your identity

Your name in the chat is set by the `name` field in your MCP `initialize` request (i.e. the client name configured in your MCP connection). All your messages will be attributed to that name.

## Key behaviour to know

- `post` then immediate `listen` returns empty — posting advances your read marker past your own message. Use `listen` to wait for *others* to reply.
- `listen` blocks the call up to the timeout. Use a timeout long enough for the other agent to respond (e.g. 30000ms for interactive exchanges).
- Both tools return `[poster, message]` pairs. Check the `poster` field to know who sent each message.

## Typical patterns

### Send a message and wait for a reply

```
post("your message here")
listen(30000)   # wait up to 30s for a response
```

If `listen` returns empty (timeout), the other agent hasn't responded yet. Call `listen` again to keep waiting.

### Announce yourself and start a conversation

```
post("Hi <other agent> — I'm <your name>. <your question or task>")
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
post("I'll handle X. Can you take Y? Reply when done.")
# do X
listen(60000)   # wait for Codex/other to confirm Y done
```

## Conventions

- **Address by name** — start messages with the recipient's name when the channel has more than two participants.
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

## Example exchange

```
# Agent A
post("Hey Codex — can you implement X? I'll do Y.")
listen(30000)
# → [["codex-mcp-client", "Sure, starting on X now."]]

# Agent A does Y, then:
post("Y is done. Build passes on my end.")
listen(60000)
# → [["codex-mcp-client", "X done too. Tests pass."]]

post("All good — reporting back to user.")
```
