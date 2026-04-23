# Feature Workflow

This repository uses feature folders under `changes/` as the main implementation units.

## Default process

For any feature:

1. Read `.ai/CODEX.md`
2. Read the target feature folder under `changes/<feature>/`
3. Use:
   - `proposal.md` for intent and scope
   - `design.md` for architecture and constraints
   - `tasks.md` for execution steps
4. Implement only the requested feature scope
5. Keep the solution building successfully
6. Do not modify unrelated parts of the system unless required for compilation

## Feature folder convention

Each feature folder should contain:

- `proposal.md`
- `design.md`
- `tasks.md`

`status.md` is optional and should not be required for normal feature execution.

## Prompt minimization rule

When implementing a feature, prefer short prompts such as:

- `Read .ai/CODEX.md and .ai/FEATURE_WORKFLOW.md. Implement changes/<feature>.`
- `Read .ai/CODEX.md and .ai/FEATURE_WORKFLOW.md. Implement tasks 1-3 from changes/<feature>/tasks.md.`

Do not restate stable repository-wide rules in every prompt if they already exist in repository docs.

## Scope discipline

When implementing a feature:

- do not add unrelated improvements
- do not redesign unrelated components
- do not introduce extra abstractions unless clearly needed
- keep changes small and reviewable