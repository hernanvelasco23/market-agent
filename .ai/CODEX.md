# CODEX Instructions

## Default startup behavior

Before coding, review relevant repository context:

- README.md
- .ai/AGENTS.md
- specs/vision.md
- specs/architecture.md
- relevant file inside changes/

## Engineering rules

- Respect clean architecture boundaries
- Domain must not depend on infrastructure
- Keep code simple and compilable
- Prefer small scoped changes
- Use clear naming
- No speculative abstractions
- No hardcoded secrets

## .NET rules

- Ensure solution builds after changes
- Keep namespaces aligned
- Do not add EF Core annotations unless explicitly requested
- Use decimal for monetary/price values

## Delivery rules

- Implement only requested scope
- Do not modify unrelated files
- Explain major design choices briefly
- Prefer maintainable code over clever code

## Workflow

If task is ambiguous:
- choose smallest safe implementation
- preserve architecture intent