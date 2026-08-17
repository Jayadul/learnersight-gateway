# Release 2 — Working Plan & Workflows

Load this when: planning R2 work or starting any R2 feature.

## Recommended task workflow: feature-by-feature in small milestone batches

The team lead plans a **milestone of 3–6 features** (one short planning session, one
document); the milestone is then implemented **one feature at a time**, each in its own
branch.

Why this hybrid beats the alternatives:

| Approach | Verdict |
|---|---|
| One task at a time | Accurate but loses cross-task context; re-explains goals every session |
| Small milestone batches (3–6) | ✅ Plan once, execute focused; context stays small per session |
| Entire sprint backlog in one go | Context bloat, unreviewable diffs, compaction losses |
| Feature-by-feature | ✅ The execution unit inside a batch |
| Epic-based planning | Right for roadmap conversations only, not implementation |

Rules of thumb: one feature = one branch = one PR = one session. Start each session by
naming the feature and pointing at the milestone doc — not by re-pasting requirements.
Keep milestone docs in `docs/releases/` (e.g. `r2-milestone-1.md`).

## Branching strategy (changes from R1)

- `main` = releasable at all times; direct pushes stop.
- `feature/<short-name>` per feature, PR into `main`, team lead approves.
- Cross-repo features: same branch name in both repos; merge backend PR first.
- Tag `v2.x.y` on both repos at each client delivery.

## Feature implementation workflow

1. Load only the docs the task needs (see CLAUDE.md loading table).
2. Plan briefly; if medium/high impact → present options and **wait for approval**
   (see [../claude/workflow.md](../claude/workflow.md)).
3. Implement backend → frontend; keep diffs reviewable.
4. Verify: build + smoke test ([../testing.md](../testing.md)) + new tests for the area.
5. Update only the affected doc sections.
6. PR with a summary the team lead can review in minutes.

## Review workflow

- Every PR: self-review with `/code-review` first, fix findings, then hand to team lead.
- UI-facing changes: run the ui-consistency-review skill before the PR.

## Testing workflow (R2 goal: stop being test-free)

- First R2 milestone should include the test harness: xUnit project + first
  SendTextOrchestrator tests; `ng test` green in the UI.
- After that, every feature PR adds/updates tests for its area. CI (GitHub Actions:
  build + test on PR) once the harness exists.

## Deployment workflow

Unchanged from R1 — [../release-process.md](../release-process.md). Add version-pinned
image tags per delivery so rollback is one compose edit.

## Documentation workflow

- Docs live in `docs/`; CLAUDE.md files stay small and point there.
- A PR that changes behavior must touch the one doc section that describes it — no more.
- New release → new `docs/releases/X.Y.Z.md` (template: 1.0.0.md).

## Candidate R2 scope (needs team-lead prioritization)

From [1.0.0.md](1.0.0.md) debt list: test harness + CI (recommended first), TLS
guidance/reverse proxy in deploy package, rate limiting on `/sendtext`, secrets out of
the public api image, per-integrator API keys, Seq retention cap, full-result CSV
exports, per-recipient delivery reporting.
