# Contributing

Thanks for your interest in GS1 Label Printer.

## Requirements

- Windows 10/11 x64
- .NET SDK 9 for building
- A ZPL-compatible Zebra printer for real print tests (optional for UI work)

## Build

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

The self-contained executable is written to `dist\GS1LabelPrinter.exe`.

## Self-test

The built app has a non-UI self-test:

```powershell
.\dist\GS1LabelPrinter.exe --selftest .\samples
```

It parses the sample file, builds ZPL/DataMatrix, checks GS1 escaping and the
TXT job-file append behaviour. Exit code `0` means success.

## Pull requests

- Keep changes focused and describe the user-visible behaviour.
- Follow the existing code style (`.editorconfig`).
- The UI supports Russian, English and Simplified Chinese. New user-facing
  strings use the Russian text as the key; add translations to
  `Core/Localization.Dictionaries.cs` for both `En` and `Zh`.
- Check the UI in all three languages before submitting (the language selector
  is at the top of the settings panel).
- Do not add real GTINs, serial numbers, customer names or production code files.
  Use synthetic data in `samples/`.
- Do not break legacy `Задание_*.txt` job files: new files use `Job_*.txt`, and
  both prefixes plus Russian/English metadata keys must be readable.
- If you change label generation, verify that the DataMatrix still decodes as
  GS1 and check the generated ZPL (the app can save it via “Сохранить ZPL…”).

## License

By contributing, you agree that your changes are licensed under the MIT
License (see `LICENSE`).
