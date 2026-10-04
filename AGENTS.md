# Coding agent guidelines

TrendWeight is an npm-workspace monorepo: React/TypeScript in `apps/web`, ASP.NET
Core in `apps/api`. Read [architecture](docs/ARCHITECTURE.md) and
[testing](docs/TESTING.md) for cross-component behavior and verification.

## Commands — run from repository root

```bash
npm ci                             # Install scripts are disabled
npm run setup:hooks                 # Explicit, once per checkout
npm run dev                         # Requires tmux and tmuxinator
npm run -w apps/web dev              # Frontend, port 5173
npm run -w apps/api dev              # Backend, port 5199
npm run check && npm run test        # Mandatory before every commit
npm run check:ci                     # Also checks formatting
npm run format                      # Formats both workspaces; review its diff
npm run -w apps/web test -- src/lib/core/dates.test.ts
dotnet test --project apps/api/TrendWeight.Tests --filter-class '*ProfileServiceTests'
```

The backend uses Microsoft.Testing.Platform and xUnit v3. Do not use legacy
VSTest `--filter` examples. On macOS, sandbox restrictions can block .NET named
pipes and package downloads; use an approved execution context if this occurs,
without weakening repository checks.

## Conventions

- Frontend: two-space indentation, kebab-case filenames, PascalCase components,
  strict TypeScript, `@/` imports instead of parent-relative imports.
- Keep routes minimal (normally under 30 lines). Delegate rendering, hooks, and
  business logic to components/helpers; pass the route context's query client to
  loaders so accounts cannot share cached data.
- Use semantic Tailwind colors from `index.css` and existing UI components.
- Backend: four-space indentation, PascalCase public members, camelCase locals.
  Keep controllers thin and I/O asynchronous.
- Store weights in kilograms and database names in snake_case. Preserve existing
  timestamp and JSON property formats when changing storage contracts.
- Test observable behavior and realistic failures with Vitest/Testing Library/MSW
  and xUnit. Use HTTP integration tests when middleware/authentication composition
  is the behavior being verified. Add regressions with bug fixes.
- Use conventional commits (`fix:`, `refactor:`, `test:`, `docs:`, `chore:`), with
  one independently reversible concern per commit. Reserve `feat:` for significant
  new user functionality; do not use `BREAKING CHANGE` in commit messages.

## Operational boundaries

- Preserve unrelated edits. Commit/push/deploy only when authorized; deployment is
  a separate action from local validation.
- Never print credentials, OAuth codes, signed state, or token response bodies.
- Review executable editor/agent hooks and install scripts as code. Do not run
  obfuscated or unexplained startup scripts to investigate them.
- `supabase/migrations` is the schema source of truth. Create versioned migrations;
  never change the remote schema directly through a dashboard or ad hoc SQL.
  Applying migrations to a remote project requires deployment authorization.
- Keep architecture and setup instructions in `docs/` and `README.md`; other
  agent instruction files should refer here instead of duplicating these rules.

## Shared agent resources

- Keep project skills in `.agents/skills/` and subagent definitions in
  `.agents/agents/`. The `.claude/skills` and `.claude/agents` symlinks expose these
  same resources to Claude Code.
- `CLAUDE.md` is a symlink to this file; edit `AGENTS.md` for shared guidance.
- Keep personal tool permissions, caches, and worktrees out of Git.
- `docs/solutions/` holds documented solutions to past problems (bugs, best
  practices, workflow patterns), organized by category with YAML frontmatter
  (`module`, `tags`, `problem_type`). It is relevant when implementing or
  debugging in a documented area.
- After a solved, verified problem, automatically invoke the `ce-compound` skill with `mode:non-interactive` at the completion checkpoint only when the work produced durable project reasoning that is not readily recoverable from the final code, tests, types, comments, or existing documentation, and losing it would plausibly cause recurrence, material risk, or substantial rediscovery. Apply this counterfactual: if the learning document disappeared, would a future engineer reading the final implementation still be likely to repeat the mistake or redo substantial investigation? If not, do not invoke it. Completion, effort, and diff size alone are not enough. Capture at the checkpoint so a qualifying learning can ship in the PR that produced it, and only where the repository treats captured learnings as tracked, committed knowledge.
- Write every report, summary, or handoff to the user through the `ce-noslop` skill. This applies when you are the top-level agent writing to the user, not when you are a subagent reporting to its caller. Do not apply it to code, config, verbatim quotes, or text the user asked to post as written.
