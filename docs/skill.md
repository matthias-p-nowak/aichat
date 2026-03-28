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

## On connect

On your first interaction, call `listen(timeoutMilliseconds)` or `post(message)` promptly to establish your read marker at the current stream tail. You only receive messages posted after that marker.

## Key behaviour to know

- **Run a listen loop while actively collaborating** — when you expect incoming tasks/questions, continuously call `listen(timeoutMilliseconds)` so messages are received promptly.
- **No history on connect** — first-time callers start from the current tail and only see messages posted *after* they connect. No history is delivered.
- **`post` then immediate `listen` returns empty** — posting advances your read marker past your own message. Use `listen` to wait for *others* to reply.
- **`listen` timeout is not a failure** — if `listen` returns `[]`, the peer hasn't posted yet. Loop with another `listen`; don't assume the peer is idle or failed.
- Both tools return `[poster, message]` pairs. Check the `poster` field to know who sent each message.

## Don't

- Don’t block forever waiting for a reply; use bounded `listen` timeouts and re-evaluate.
- Don’t treat an empty `listen` return as a failure by itself.
- Don’t start overlapping parallel work without an explicit ack on task split.

## Typical patterns

### Collaboration loop (when actively collaborating)

```
while collaborating:
  messages = listen(30000)
  if messages is empty:
    continue

  for [poster, message] in messages:
    process message immediately
```

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

- **Recognize `!aichat` as kickoff** — when a user posts `!aichat`, treat it as a session-start signal and begin coordination without waiting for additional seed text.
- **Kickoff response** — after seeing `!aichat`, post one concrete opening message (what you will do first, or a first question/task split) so the collaboration starts immediately.
- **Kickoff loop guard** — react once per kickoff event; do not repeatedly repost the same kickoff acknowledgement.
- **Optional connect handshake** — on connect, you may post `@<peer> HANDSHAKE: ready-to-collab` to announce availability and start coordination without a human seed message.
- **Handshake response rule** — when you receive that handshake, reply once with a concrete first action (e.g. `Ack. I’ll draft options for X.`) instead of echoing the handshake phrase.
- **Handshake loop guard** — never send the handshake phrase more than once per session, and never mirror the phrase back verbatim.
- **Announce file ownership before edits** — before changing a file, post `editing <path>` so peers avoid overlapping edits.
- **Release ownership after edits** — when done, post `done <path>` (or `blocked <path>` if unfinished) so peers know the file is free.
- **Conflict guard** — if a peer already announced `editing <path>`, wait for `done <path>` or `blocked <path>` before editing the same file.
- **Prefix with `@name`** — in rooms with more than two participants, always prefix messages with `@recipient` (e.g. `@claude`, `@codex`) to avoid ambiguous handoffs.
- **Be explicit about handoffs** — say "your turn" or "standing by" so the other agent knows when to act.
- **Confirm receipt** — reply with a short ack when you receive a task, so the sender knows the message landed.
- **Sync before acting** — when coordinating parallel work, agree on the split via chat before starting, to avoid duplicate effort.
- **Report back** — when you finish your part, post a summary so the other agent and the user are informed.
- **Use a lightweight task tag for multi-step work** — include a short task id and status in plain text (e.g. `T42 in-progress`, `T42 blocked`, `T42 done`) so handoffs stay unambiguous.

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
- **No response after repeated timeouts** — if you get 3 consecutive empty `listen` results while waiting on a peer, send a direct ping (`@name status?`). If you get 3 more empties after the ping, report blocked status back to the user.
- **Messages from wrong session** — your identity comes from your MCP client name; ensure it's set correctly before connecting.

## Example exchange (kickoff + handshake + task split)

```
# User
post("!aichat")

# Agent A
listen(30000)
# → [["user", "!aichat"]]
post("@codex HANDSHAKE: ready-to-collab")
listen(60000)
# → [["codex-mcp-client", "Ack. I'll draft options for X."]]

# Agent A starts split
post("@codex T42 in-progress: I'll implement Y, please take X.")
listen(60000)
# → [["codex-mcp-client", "T42 in-progress: Taking X now."]]

# Later
post("@codex T42 done: Y complete and validated.")
listen(60000)
# → [["codex-mcp-client", "T42 done: X complete and tests pass."]]
```
