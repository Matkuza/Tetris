# Tetris (WPF)

Arcade Tetris clone in C# / WPF with multiple modes, per-mode highscores, ghost piece, hold, configurable controls, DAS/ARR and ad panel manager.

## Features
- Classic, Survival, Sprint (40 lines), Ultra (120s), Marathon.
- 7-bag randomizer.
- Ghost piece and hold piece.
- Per-mode highscores (JSON persistence).
- Session stats in HUD: APM / PPS / average lock delay / Tetris clears.
- Configurable keybinds and DAS/ARR in settings.
- Colorblind accessibility mode with symbol/pattern overlays on blocks.
- Export session stats/highscores to CSV/JSON.
- First-run tutorial and “What's New” onboarding overlays.

## Controls (default)
- Left / Right: move
- Down: soft drop
- Up: rotate
- Space: hard drop
- C: hold
- P: pause
- Esc: close

## Run locally
### Requirements
- .NET 8 SDK
- Windows (WPF)

### Commands
```bash
dotnet restore Tetris.sln
dotnet build Tetris.sln
dotnet test Tetris.sln
```

## Settings persistence
The app stores data in `Tetris/AdAssets/`:
- `settings.json`
- `highscores.json`
- `ads.json`

## CI
A GitHub Actions workflow is included in `.github/workflows/dotnet-ci.yml` and runs restore/build/test on push and pull request.
