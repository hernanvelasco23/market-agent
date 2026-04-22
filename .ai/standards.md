Never implement feature code on top of fake folder structures.

Use executable project scaffolding first:
- solution
- projects
- references
- build passes

## Maintainability rules

Isolate likely change points.

Examples:
- external symbol mappings
- provider-specific field mappings
- normalization rules
- parsing rules

Do not scatter provider-specific assumptions inline if they can be centralized in a small method.