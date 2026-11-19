# Synthetic samples

This folder contains **synthetic** GS1 data for demonstrations and self-tests.

- GTIN `00000000000000` is a test value.
- Serial numbers, `91` and `92` values are fictional.

Do **not** commit real production code files. Keep them in `local-data/`
(or anywhere outside the repository) — that folder is ignored by git.

Format: one GS1 code per line. The application accepts both raw `GS` separators
(`0x1D`) and the parenthesized form used in this sample.
