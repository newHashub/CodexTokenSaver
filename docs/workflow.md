# Workflow / 工作流

CodexTokenSaver is useful because it separates durable project memory from long chat history.

CodexTokenSaver 的核心不是“让当前长会话立刻变小”，而是让你随时能安全迁移到轻量新会话。

## Three Layers

1. **Context packs** keep project state in `docs/codex/`.
2. **Handoff summaries** compress the current session into a paste-ready prompt.
3. **Token Lights** warns when recent Codex sessions are becoming expensive to continue or close to the model context limit.

## Suggested Rules

- Green: keep working.
- Yellow: prepare a handoff if the task will continue for a while.
- Red: generate a handoff and continue in a fresh session.
- Image-heavy sessions should be handed off earlier.
- Important projects should point new sessions to files, not pasted chat transcripts.

## Token Signals

Token Lights uses both per-turn input size and model-window occupancy:

- input below `80k` and window below `50%`: green
- input `80k-150k` or window `50%-75%`: yellow
- input `150k+` or window `75%+`: red
