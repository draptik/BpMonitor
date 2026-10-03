---
name: model-advisor
description: Reference for selecting the right Claude model tier (Opus, Sonnet, Haiku) based on task complexity, reasoning depth, and cost/speed tradeoffs. Auto-loaded when model selection is relevant.
user-invocable: false
---

# Model Advisor

Pick the cheapest model that does the job well. **Default to Sonnet; escalate to Opus only when deep reasoning is genuinely required.**

## Cheatsheet

| Situation | Model | Switch with |
| --- | --- | --- |
| Default — code, TDD, refactoring, analysis, most tasks | Sonnet | `/model sonnet` |
| Deep reasoning — architectural tradeoffs, multi-step reasoning over large context, high-stakes judgment | Opus | `/model opus` |
| Conversational, templated, or config-only | Haiku | `/model haiku` |

Use the aliases (here and in skill `model:` frontmatter) rather than full model
IDs — they always resolve to the latest model in each tier, so nothing goes stale.

## Rules

- Sonnet is the safe default for anything code-related — start there
- Escalate to Opus only when the task genuinely needs deep reasoning, then drop back to Sonnet
- Down-shift to Haiku for purely conversational or templated work
- Re-evaluate when task scope changes mid-conversation
