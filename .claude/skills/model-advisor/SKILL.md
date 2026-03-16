---
name: model-advisor
description: Reference for selecting the right Claude model (Opus 4.6, Sonnet 4.6, Haiku 4.5) based on task complexity, reasoning depth, and cost/speed tradeoffs. Auto-loaded when model selection is relevant.
user-invocable: false
---

# Model Advisor

Recommend the most appropriate Claude model for each task based on complexity, reasoning requirements, and cost/speed tradeoffs.

## Available Models

| Model ID | Name | Strengths |
| --- | --- | --- |
| `claude-opus-4-6` | Claude Opus 4.6 | Deep reasoning, complex decisions, nuanced judgment |
| `claude-sonnet-4-6` | Claude Sonnet 4.6 | Balanced capability and speed, code generation, most tasks |
| `claude-haiku-4-5-20251001` | Claude Haiku 4.5 | Fast, lightweight, simple and conversational tasks |

## Decision Rules

```text
Is the task conversational, templated, or config-only?
  → Haiku

Does the task involve code generation, analysis, refactoring, or standard TDD?
  → Sonnet

Does the task require ANY of the following?
    - Architectural tradeoffs or design decisions
    - Multi-step reasoning across large context
    - Nuanced judgment with significant consequences
  → Opus
```

When in doubt: **Sonnet is the safe default.**

## Persona Defaults

| Persona | Default Model | Reason |
| --- | --- | --- |
| Product Visionary (Lisa) | Haiku 4.5 | Conversational, exploratory |
| Architect (Professor Frink) | Opus 4.6 | Complex tradeoff analysis |
| Senior Developer / TDD (Sideshow Bob) | Sonnet 4.6 | Code generation + reasoning |
| Senior Tester (Martin Prince) | Sonnet 4.6 | Code analysis and test generation |

## Rules

- Prefer the cheapest/fastest model that can do the job well
- Escalate to Opus only when deep reasoning is genuinely required
- Sonnet is the safe default for anything code-related
- Haiku for conversational or templated tasks
- Re-evaluate when task scope changes mid-conversation

## How to Switch Models

```text
/model claude-opus-4-6
/model claude-sonnet-4-6
/model claude-haiku-4-5-20251001
```
