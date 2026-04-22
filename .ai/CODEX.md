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

## Implementation refinement rules

When implementing infrastructure providers:

- Avoid embedding provider-specific symbol transformations inline when they may change later.
- Extract provider symbol mapping into a dedicated private method or mapper when external symbol formats differ from internal symbols.
- Prefer small private methods for normalization, mapping, parsing, and validation.
- Keep provider behavior simple, but isolate likely change points.
- Do not hardcode fragile assumptions across multiple lines when a single method can centralize them.