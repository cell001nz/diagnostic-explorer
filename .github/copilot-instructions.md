# Project Guidelines

## Diagnostic Explorer Integrations

When a task involves configuring Diagnostic Explorer in an application process,
read [Docs/agent-configuration-guide.md](../Docs/agent-configuration-guide.md)
before editing. Follow its guidance for hosting, stable object registration,
focused type profiles, collections, drilldowns, event routing, and verification.

Use the existing generic-host integration and the widget sample as the default
patterns. Preserve explicit capability semantics: configuration decides what the
viewer may display or do, and the UI must not infer them from displayed values.
