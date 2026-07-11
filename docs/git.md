# Git Workflow

Load this when: committing, branching, or coordinating changes across the two repos.

## Repos

| Repo | Remote |
|---|---|
| Backend | github.com/Jayadul/learnersight-netzero-gateway |
| Frontend | github.com/Jayadul/learnersight-netzero-gateway-ui |

The workspace root (compose, `backup/`, `deploy/`, shared root README) is **not**
version-controlled — treat changes there with extra care and mirror important
knowledge into this repo's `docs/`.

## Rules

- Release 1.0.0 was developed directly on `main`. From Release 2 onward use feature
  branches + PRs (see releases/release-2-plan.md); never force-push `main`.
- Commit messages: one imperative summary line; end body with
  `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>` when Claude authored it.
- Cross-repo features (API + UI): commit backend first, then frontend, with matching
  wording so history lines up.
- Never commit: `.vscode/`, `node_modules/`, build output, un-encrypted secrets.
- Tag releases on both repos: `git tag v1.0.0 && git push origin v1.0.0`.

## Release tagging + image sync

A release = matching git tag on both repos + freshly pushed `learnersight/*:latest`
images built from those exact commits. Follow [release-process.md](release-process.md).
