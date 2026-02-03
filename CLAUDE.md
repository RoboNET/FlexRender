# Claude Code Instructions for FlexRender

Read [AGENTS.md](./AGENTS.md) for project overview, architecture, build commands, conventions, and resource limits.

## Agent Workflow

Follow the global workflow from `~/.claude/CLAUDE.md`:

1. **brainstormer** -- use FIRST for new template features, element types, content providers, layout algorithms
2. **plan-writer** -- create TDD implementation plan after design is approved
3. **csharp-pro** -- for ALL C# code changes in `src/` and `tests/`
4. **master-code-reviewer** -- MANDATORY after any code changes

Use **Explore** agent for codebase navigation before making changes.

## Domain-Specific Review Checklist

When reviewing code changes, verify:

- [ ] AOT safe -- no reflection, no `dynamic`, no `Type.GetType()`. Use `GeneratedRegex` for regex
- [ ] Classes are `sealed` unless designed for inheritance
- [ ] Null checks via `ArgumentNullException.ThrowIfNull()` -- not manual `if (x == null)`
- [ ] Resource limits preserved -- never remove or weaken `MaxFileSize`, `MaxNestingDepth`, `MaxRenderDepth`
- [ ] New element types follow the switch-based dispatch pattern (not base class properties)
- [ ] XML docs on all public API surface
- [ ] Snapshot tests added/updated for visual changes (`UPDATE_SNAPSHOTS=true`)
