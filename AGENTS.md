## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them — don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

Carve-out: for clearly-scoped bug reports with a reproduction, don't ask permission — diagnose, fix, and show the fix passing.

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you wrote 200 lines and it could be 50, rewrite it.

Test: "Would a senior engineer say this is overcomplicated?" If yes, simplify. 

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it — don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

Test: every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

## 5. Don't Claim Done Without Proof

**"It should work" is not done. Show evidence.**

Before marking a task complete:
- Paste the test output, command output, or log line that proves it.
- If you can't show it, you haven't done it — keep working.
- Ask yourself: "Would a staff engineer approve this on PR review?"

Anti-rationalization: don't say "I'll add tests later" — write them now or say out loud you're not going to.

## 6. Learn From Corrections

**Every correction is a rule waiting to be written.**

When I correct you:
- Append a one-line rule to [.agents/lessons.md](.agents/lessons.md).
- Format: `[area] Don't X — do Y instead. (cause: <one phrase>)`
- Read `.agents/lessons.md` at the start of every session, before planning.
- If the same rule fires three times, promote it into the relevant SKILL.md.

## 7. Keep Context Clean

**Context is finite. Spend it on the task, not on "just in case".**

- For research-heavy steps (reading large files, exploring unfamiliar code), use a subagent and return only the conclusion.
- One task per subagent. Don't bundle.
- Don't fetch files "just in case" — fetch on demand.

## Skills

Reusable skill definitions live in [.agents/skills/](.agents/skills/). Load the relevant `SKILL.md` before starting work that matches its scope:

- [code-standards](.agents/skills/code-standards/SKILL.md): coding conventions for this repo — apply when making, reviewing, or refactoring code.
- [planning-and-task-breakdown](.agents/skills/planning-and-task-breakdown/SKILL.md): use for broad, vague, multi-step, or parallelizable work that needs ordered tasks with dependencies, acceptance criteria, and verification steps.
- [writing-tests](.agents/skills/writing-tests/SKILL.md): adding or modifying tests — naming, project structure, InternalsVisibleTo setup for internal types, and verification.
- [verify-readme-docs](.agents/skills/verify-readme-docs/SKILL.md): audit and minimally fix README files against actual code and tests.
- [infragate-mcp-gateway](.agents/skills/infragate-mcp-gateway/SKILL.md): use the local InfraGate MCP gateway for Kubernetes inspection and guarded changes.
- [run-local-sonarqube](.agents/skills/run-local-sonarqube/SKILL.md): run local SonarQube Community Build analysis and ensure the agent-ingestible report is saved to disk.
- [sonar-local-remediaton](.agents/skills/sonar-local-remediaton/SKILL.md): consume the saved local SonarQube report and produce a remediation plan.
- [sonarcloud-remediation](.agents/skills/sonarcloud-remediation/SKILL.md): consume SonarCloud CI findings and produce a remediation plan.
- [repo-onboarding](.agents/skills/repo-onboarding/SKILL.md): orient agents in the repo before broad investigations, repo navigation, or unfamiliar work.
- [tdd](.agents/skills/tdd/SKILL.md): test-first development — write failing tests before writing implementation code.
- [grill-with-docs](.agents/skills/grill-with-docs/SKILL.md): cross-check code behavior against documentation and surface gaps.
- [improve-codebase-architecture](.agents/skills/improve-codebase-architecture/SKILL.md): structural or architectural analysis and refactor proposals.
- [run-tests](.agents/skills/run-tests/SKILL.md): run the test suite, interpret failures, and validate fixes.

## Codegraph

When `.codegraph/` is present, prefer these tools over file reads and grep:

- `codegraph_status` — verify the index is healthy before relying on it
- `codegraph_files` — project file tree with symbol counts (replaces `find`)
- `codegraph_context` — primary task entry point; run before deciding which docs to read
- `codegraph_search` — locate symbols by name (replaces grep)
- `codegraph_callers` / `codegraph_callees` — trace call chains through the approval and dispatch flows
- `codegraph_impact` — check blast radius before making changes

Use doc reads for rationale, flow diagrams, and ADR decisions that codegraph cannot answer.

## Agent Memory

Use agentmemory **exclusively via MCP tools** — never via curl or direct HTTP to the REST API:

- `memory_save` — persist a fact, pattern, architecture decision, bug, or workflow rule
- `memory_recall` — retrieve memories by query
- `memory_smart_search` — semantic + graph search across all memories
- `memory_sessions` — list known sessions
- `memory_lesson_save` — save a lesson learned (maps to `workflow` type internally)

The MCP shim proxies to the engine at `http://localhost:3111` internally — that is not your concern. All memory operations go through MCP, period.

<!-- headroom:rtk-instructions -->
# RTK (Rust Token Killer) - Token-Optimized Commands

When running shell commands, **always prefix with `rtk`**. This reduces context
usage by 60-90% with zero behavior change. If rtk has no filter for a command,
it passes through unchanged — so it is always safe to use.

## Key Commands
```bash
# Git (59-80% savings)
rtk git status          rtk git diff            rtk git log

# Files & Search (60-75% savings)
rtk ls <path>           rtk read <file>         rtk grep <pattern>
rtk find <pattern>      rtk diff <file>

# Test (90-99% savings) — shows failures only
rtk pytest tests/       rtk cargo test          rtk test <cmd>

# Build & Lint (80-90% savings) — shows errors only
rtk tsc                 rtk lint                rtk cargo build
rtk prettier --check    rtk mypy                rtk ruff check

# Analysis (70-90% savings)
rtk err <cmd>           rtk log <file>          rtk json <file>
rtk summary <cmd>       rtk deps                rtk env

# GitHub (26-87% savings)
rtk gh pr view <n>      rtk gh run list         rtk gh issue list

# Infrastructure (85% savings)
rtk docker ps           rtk kubectl get         rtk docker logs <c>

# Package managers (70-90% savings)
rtk pip list            rtk pnpm install        rtk npm run <script>
```

## Rules
- In command chains, prefix each segment: `rtk git add . && rtk git commit -m "msg"`
- For debugging, use raw command without rtk prefix
- `rtk proxy <cmd>` runs command without filtering but tracks usage
<!-- /headroom:rtk-instructions -->
