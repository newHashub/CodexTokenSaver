---
name: handoff-summary
description: "Generate concise handoff summaries and copy-paste prompts for continuing work in a new Codex session. Use when the user says 交接, handoff, handoff summary, 新会话继续, 下次继续, 会话太长, 收尾, summarize for next session, or asks to reduce token/context load by moving state to a fresh chat."
---

# Handoff Summary

## Purpose

Create a compact, self-contained handoff prompt that lets a new Codex session continue without carrying the long chat history. Prefer durable project files when the work is important or recurring; use a chat-only summary for one-off work.

## Workflow

1. Classify the handoff.
   - Important, long-running, recurring, code, business, or project work: use the user's context-pack workflow.
   - Simple Q&A, translation, light writing, or one-off exploration: skip file updates and generate only a copy-paste prompt.

2. Locate project state when needed.
   - Prefer repo-local `docs/codex/`.
   - If there is no repo-local pack but the project name/path is obvious, use a local context root such as `C:\codex\contexts\<project-name>\` or another user-approved folder.
   - Read existing files before editing: `context.md`, `current-state.md`, `decisions.md`, `runbook.md`, and `handoff.md` as relevant.
   - Read `archive.md` only when historical detail is needed.
   - If the user explicitly asks to establish or restructure a full context pack, use the templates in this repository's `templates/context-pack/` folder or the user's local context-pack instructions.

3. Update durable files before answering when the turn produced durable state.
   - `current-state.md`: current progress, completed work, blockers, verification, and next steps.
   - `decisions.md`: confirmed important decisions or rejected directions.
   - `runbook.md`: reusable procedures, commands, validation steps, or account/tool boundaries.
   - `handoff.md`: the shortest recovery prompt for the next session.
   - `archive.md`: longer history, failed attempts, or context that is useful but not current state.

4. Generate the handoff prompt.
   - Keep it concise and directly pasteable into a new session.
   - Use bullet points, not a transcript.
   - Include only facts that are visible, verified, or clearly marked `to confirm`.
   - Mention files the next session should read instead of pasting long file contents.
   - For images, include file paths or short descriptions. Do not ask the next session to reconstruct old image context from chat history.
   - Exclude secrets, cookies, tokens, passwords, private account details, and unnecessary personal data.

## Output Format

Use this structure unless the user requests another format:

```text
交接完成。给下一个会话使用的提示词如下：

从这个 handoff 继续。请先不要执行高风险操作，先复述你理解的状态和下一步。

项目/任务：
- ...

当前状态：
- ...

已完成：
- ...

已改文件/重要产物：
- ...

关键决策：
- ...

验证结果：
- ...

阻塞点/待确认：
- ...

下一步建议：
- ...

边界和注意事项：
- ...

新会话开始后请先读取：
- `...`
```

If files were updated, add a short line before the prompt:

```text
已更新：`docs/codex/current-state.md`、`docs/codex/handoff.md`
```

If no files were updated, state why briefly:

```text
本次是轻量交接，没有明确项目上下文包，所以未写入文件。
```

## Quality Bar

- Make the handoff useful to an agent that cannot see the old conversation.
- Preserve user goals, hard boundaries, current blockers, and next action more carefully than narrative detail.
- Keep the prompt small enough to reduce token load; target 500-1000 Chinese characters for ordinary handoffs.
- For large project handoffs, prefer pointing to context-pack files over pasting long summaries.
- Do not claim tests, builds, browser checks, or external submissions happened unless they actually happened.
