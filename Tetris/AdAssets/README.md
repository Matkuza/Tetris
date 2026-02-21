# AdAssets data policy

This folder stores runtime data and ad media used by the app.

## Files
- `settings.json` — UI/game settings and controls.
- `highscores.json` — per-mode highscores (with legacy list migration support).
- `session-history.json` — last session stats used by trend chart/export.
- `onboarding.json` — first-run tutorial and "what's new" display state.
- `ads.json` + `*.jpg` — ad manifest and ad images.

## Migration policy
- Keep backward compatibility for existing user files whenever possible.
- `highscores.json` legacy list format must continue to migrate into `Classic` mode.
- New JSON fields should be additive and have sensible defaults.
- Corrupted JSON should fail gracefully and fall back to defaults.

## Retention policy
- Keep up to 200 session history entries.
- Keep up to top 5 highscores per mode.

## SemVer and data impact
- Non-breaking data format changes: MINOR/PATCH.
- Breaking data format changes: MAJOR (and provide migration notes).
