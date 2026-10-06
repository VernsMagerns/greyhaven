# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Standing rules

- **Never run destructive git or file commands without asking first.** This includes
  `git clean`, `git reset --hard`, `git checkout -- .`, `git restore` on uncommitted work,
  `git push --force`, branch deletion, and `rm -rf` on project folders. Before running one,
  explain exactly what it will delete and wait for approval.
- **Commit after each verified step** (build passes, tests pass), so work is never left
  uncommitted for long.
- **Git identity:** commits are authored as `Kyler Verney <280451244+VernsMagerns@users.noreply.github.com>`. Do not
  use any other email address anywhere in the project (docs, README, license, package
  metadata, CI config).
- **Dates:** write dates in docs and data in the maintainer's local time, US Pacific
  (America/Los_Angeles), not UTC. Tool timestamps are often UTC; convert them first.

## Project

GreyHaven is a Windows app that rates a PC's hardware for Linux (Mint, Ubuntu, Pop!_OS).
See `docs/PLAN.md` for architecture, decisions and milestones, and
`docs/windows-validation.md` for checks that need a real Windows PC.

- Develop on Linux: everything except `GreyHaven.Scan.Windows` (and later the WPF app,
  elevated helper and USB disk I/O) runs and is tested on Linux.
- When a milestone needs real Windows hardware, say so clearly and add the check to
  `docs/windows-validation.md` so they can be done in batches.
- The repository is `VernsMagerns/greyhaven` (`GreyHavenInfo.GitHubRepository`), **private**
  for now (`GreyHavenInfo.IsRepositoryPublic = false`). Before it's made public: remind the
  maintainer that commit emails become visible and offer to switch to their GitHub no-reply
  address, then flip `IsRepositoryPublic`.
- `gh` and GitHub credentials live on the host; from the VS Code Flatpak, run them with
  `flatpak-spawn --host` (e.g. `flatpak-spawn --host gh run list`).
- Compatibility ratings come from IDs only, never device names, and never guess: no data
  means "Unknown".

## Commands

```sh
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH   # if .NET is installed user-locally
dotnet build
dotnet test
dotnet run --project src/GreyHaven.Cli -- scan --sample intel-vmd-laptop
```
