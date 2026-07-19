# Workspace Skills (versioned copy)

These skills drive the recurring workflows (feature recipes, shipping, smoke tests,
UI audits). Claude Code loads skills from the **working directory's** `.claude/skills/`.

The live working setup is a workspace folder containing BOTH repos side by side:

```
NetZero/                              ← Claude Code runs here
├── .claude/skills/                   ← live copies of these skills
├── docker-compose.yml, backup/, deploy/   (un-versioned orchestration)
├── learnersight-netzero-gateway/     ← this repo (docs hub, versioned skills)
└── learnersight-netzero-gateway-ui/
```

On a fresh machine: clone both repos into a workspace folder and copy this
`skills/` folder AND the sibling `settings.json` to `<workspace>/.claude/` (paths
inside the skills are relative to the workspace root). When editing a skill or
settings, edit here (versioned) first, then mirror to the root copy.
