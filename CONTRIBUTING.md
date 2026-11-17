# Contributing

Thanks for helping!

- **Bugs / ideas:** open an issue using the templates. Include your Windows version and display scale.
- **Translations:** all texts live in [`Strings.cs`](Strings.cs), one row per text and one column per language. To fix a translation, edit the string. To add a language, append its code and name, then one string to every row. Run `build.cmd`: it also runs `test-strings.ps1`, which fails if any row is missing a language.
- **Code:** keep it a single small app with no extra dependencies. Build with `build.cmd` (uses the compiler built into Windows) and test on Windows 11 before opening a pull request.
