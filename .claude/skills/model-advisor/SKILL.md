---
name: model-advisor
description: Reference for selecting the right Claude model (Opus 4.8, Sonnet 4.6, Haiku 4.5) based on task complexity, reasoning depth, and cost/speed tradeoffs. Auto-loaded when model selection is relevant.
user-invocable: false
---

# Model Advisor

Pick the cheapest model that does the job well. **Default to Sonnet; escalate to Opus only when deep reasoning is genuinely required.**

## Cheatsheet

| Situation | Model | Switch with |
| --- | --- | --- |
| Default — code, TDD, refactoring, analysis, most tasks | Sonnet 4.6 | `/model claude-sonnet-4-6` |
| Deep reasoning — architectural tradeoffs, multi-step reasoning over large context, high-stakes judgment | Opus 4.8 | `/model claude-opus-4-8` |
| Conversational, templated, or config-only | Haiku 4.5 | `/model claude-haiku-4-5-20251001` |

## Rules

- Sonnet is the safe default for anything code-related — start there
- Escalate to Opus only when the task genuinely needs deep reasoning, then drop back to Sonnet
- Down-shift to Haiku for purely conversational or templated work
- Re-evaluate when task scope changes mid-conversation
