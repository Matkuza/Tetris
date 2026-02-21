# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2026-02-21
### Added
- Marathon mode in gameplay and mode-aware highscore handling.
- Session statistics in HUD (APM, PPS, average lock delay, Tetris count).
- Colorblind accessibility mode with alternate palette and symbol/pattern overlays.
- Session history export to CSV/JSON and highscore export to CSV.
- First-run tutorial overlay and versioned “What’s New” overlay.
- Persistence regression tests and helper utilities for settings/highscore migration.

### Changed
- Right-side HUD layout compacted with split Next/Hold preview cards.
- Persistence flow normalized via dedicated helpers.

## [1.0.0] - 2026-02-18
### Added
- Initial playable WPF Tetris release with core gameplay, audio and ads panel manager.
