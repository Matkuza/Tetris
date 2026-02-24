# Release Checklist

Use this checklist before creating a release tag.

## 1) Versioning (SemVer)
- [ ] Pick release type:
  - `MAJOR` for breaking changes
  - `MINOR` for backward-compatible features
  - `PATCH` for fixes only
- [ ] Update `Tetris/Tetris.csproj` `<Version>` and `<InformationalVersion>`.
- [ ] Add a new entry in `CHANGELOG.md`.

## 2) Build & test
- [ ] `dotnet restore Tetris.sln`
- [ ] `dotnet build Tetris.sln --configuration Release --no-restore`
- [ ] `dotnet test Tetris.sln --configuration Release --no-build`
- [ ] CI workflow green on target branch (`.github/workflows/dotnet-ci.yml`).

## 3) Smoke test (manual)
- [ ] Start game and verify all modes (Classic/Survival/Sprint/Ultra/Marathon).
- [ ] Verify keybinds + DAS/ARR settings save/load.
- [ ] Verify highscores save/load and per-mode filtering.
- [ ] Verify colorblind mode toggle and visibility of overlays.
- [ ] Verify export buttons produce valid CSV/JSON files.
- [ ] Verify tutorial/what's-new overlays behavior.

## 4) Data/assets readiness
- [ ] Confirm `Tetris/AdAssets/README.md` policy is still valid.
- [ ] Verify migration compatibility for old `highscores.json` (legacy list -> per-mode map).
- [ ] Ensure no temporary/debug assets are accidentally committed.

## 5) Publish
- [ ] Create release commit and push.
- [ ] Tag release (`git tag vX.Y.Z` and push tag).
- [ ] Create GitHub release notes from `CHANGELOG.md`.
