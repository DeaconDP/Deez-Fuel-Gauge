# Engineering instructions

## Workflow

- Never commit or push directly to the default branch.
- Make changes on a branch beginning with `cursor/`.
- Keep changes focused on the requested task.
- Add or update tests for behaviour changes.
- Do not change dependencies, deployment settings or GitHub workflows
  unless the task requires it.
- Never add credentials, API keys or local environment files to Git.

## Before declaring a task complete

Run:

1. `dotnet restore CursorUsageWidget.sln`
2. `dotnet build CursorUsageWidget.sln -c Release --no-restore`
3. `dotnet test CursorUsageWidget.sln -c Release --no-build` (when test projects exist)

Fix failures caused by the change.

## Completion report

Summarise:

- files changed
- tests run
- remaining risks
- any manual steps required
