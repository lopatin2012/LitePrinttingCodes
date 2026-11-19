# Changelog

## 1.2.0 — 2026-11-19

- Added interface languages: Russian, English and Simplified Chinese.
  The language is selectable in the app and persisted; changing it restarts the app.
- Default language follows the OS UI language (`ru`, `en` or `zh`).
- Job files now use the neutral `Job_` prefix and an English technical header.
  Existing `Задание_` files and Russian metadata are still read and appended.
- The file path is shown as a file name; the job folder shows `~` instead of the
  full user profile path. Full paths remain in tooltips.
- Added banner and localized screenshots for the README.

## 1.1.0 — 2026-11-19

- Added configurable line breaks for the human-readable code text
  (`0` — no breaks, `1` — split in half, `2` — three parts, etc.).
- Added TXT job files: every code sent to the printer is recorded; existing
  job files are appended, new ones are created.
- Job file path/state is shown before printing; the folder is configurable.
- Text now follows the DataMatrix horizontally and is rebuilt for the new width.
- Added a self-test mode (`--selftest`) covering parsing, GS1, ZPL, DataMatrix,
  wrapping and job-file append behaviour.
- Verified GS1 encoding of codes containing `_` (CSET 82) and FNC1 separators.
- Added self-contained single-file publishing (`dist\GS1LabelPrinter.exe`).

## 1.0.0 — 2026-11-19

- Initial Windows WinForms application.
- GS1 DataMatrix ECC200 printing via raw ZPL.
- Product name and human-readable code text under the DataMatrix.
- Draggable/resizable DataMatrix position (X/Y and module size).
- TXT input: one GS1 code per line; raw GS separators or parenthesized AIs.
- Printer selection with ZD421 auto-detection; raw `winspool` printing.
- Cyrillic text rendered as a `^GF` graphic, independent of printer fonts.
