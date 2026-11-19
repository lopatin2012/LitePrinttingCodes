using System.Text;

namespace Gs1LabelPrinter.Core;

public static class SelfTest
{
    public static int Run(string directory)
    {
        var log = new StringBuilder();
        var errors = 0;

        void Ok(string message) => log.AppendLine("OK   " + message);
        void Fail(string message)
        {
            errors++;
            log.AppendLine("FAIL " + message);
        }

        try
        {
            log.AppendLine("GS1 Label Printer self-test");
            log.AppendLine("Directory: " + directory);
            log.AppendLine();

            var files = Directory.GetFiles(directory, "DataMatrixGS1_*.txt");
            Array.Sort(files, StringComparer.CurrentCultureIgnoreCase);

            if (files.Length == 0)
            {
                Fail("Не найдены файлы DataMatrixGS1_*.txt");
            }

            string? firstZpl = null;
            string? underscoreZpl = null;
            LabelFile? firstFile = null;

            foreach (var path in files)
            {
                try
                {
                    var file = CodeFileParser.Load(path);
                    firstFile ??= file;
                    log.AppendLine($"Файл: {file.FileName}");
                    log.AppendLine($"  продукт: {file.FullProductName}");
                    log.AppendLine($"  короткая надпись: {CodeFileParser.MakeShortProductName(file.FullProductName)}");
                    log.AppendLine($"  GTIN: {file.Gtin ?? "-"}");
                    log.AppendLine($"  кодов: {file.Codes.Count}, в имени: {file.DeclaredQuantity}");

                    if (file.Codes.Count != file.DeclaredQuantity)
                        Fail($"{file.FileName}: количество строк не совпадает с числом в имени файла");
                    else
                        Ok($"{file.FileName}: количество кодов совпадает");

                    if (file.Codes.Any(c => c.Length < 10))
                        Fail($"{file.FileName}: есть подозрительно короткие коды");
                    else
                        Ok($"{file.FileName}: длины кодов в порядке");

                    var human = CodeFileParser.HumanizeGs1(file.Codes[0]);
                    if (!human.StartsWith("(01)", StringComparison.Ordinal))
                        Fail($"{file.FileName}: человекочитаемый вид не начинается с (01): {human}");
                    else
                        Ok($"{file.FileName}: человекочитаемый вид: {Trim(human, 110)}");

                    var settings = new LabelSettings();
                    var product = CodeFileParser.MakeShortProductName(file.FullProductName);
                    using var result = LabelRenderer.Build(file, 0, product, settings);
                    var layout = result.GetLayout(settings);
                    log.AppendLine($"  DataMatrix: {result.SymbolSize}×{result.SymbolSize} модулей, {layout.DmSizePx}×{layout.DmSizePx} точек");
                    log.AppendLine($"  текст: {layout.TextWidth}×{layout.TextHeight}, верх {layout.TextTop}");
                    log.AppendLine($"  шрифты: продукт {result.Text.ProductFontUsed:0.#}, код {result.Text.CodeFontUsed:0.#}");

                    if (result.SymbolSize is < 10 or > 144)
                        Fail($"{file.FileName}: недопустимый размер DataMatrix {result.SymbolSize}");
                    else
                        Ok($"{file.FileName}: размер DataMatrix допустим");

                    if (!result.Text.PackedBits.Any(b => b != 0))
                        Fail($"{file.FileName}: текстовая картинка пустая");
                    else
                        Ok($"{file.FileName}: текстовая картинка не пустая");

                    foreach (var warning in layout.Warnings)
                        log.AppendLine("  WARN " + warning);

                    var zpl = result.BuildZpl(settings);
                    if (!zpl.Contains("^FD\\1", StringComparison.Ordinal))
                        Fail($"{file.FileName}: в ZPL нет FNC1");
                    if (zpl.Contains("^FH_", StringComparison.Ordinal))
                        Fail($"{file.FileName}: в ZPL не должно быть ^FH_");
                    if (!zpl.Contains("^GFA,", StringComparison.Ordinal))
                        Fail($"{file.FileName}: в ZPL нет текстовой графики");
                    if (file.Codes[0].Contains('_') && !zpl.Contains('_'))
                        Fail($"{file.FileName}: потерян символ '_' в данных");

                    firstZpl ??= zpl;

                    var underscoreIndex = file.Codes.FindIndex(c => c.Contains('_'));
                    if (underscoreIndex >= 0 && underscoreZpl is null)
                    {
                        using var underscoreResult = LabelRenderer.Build(
                            file, underscoreIndex, product, settings);
                        underscoreZpl = underscoreResult.BuildZpl(settings);
                        log.AppendLine($"  underscore ZPL: код #{underscoreIndex + 1}");
                    }

                    log.AppendLine();
                }
                catch (Exception ex)
                {
                    Fail($"{Path.GetFileName(path)}: {ex.Message}");
                }
            }

            if (firstFile is not null)
            {
                TestJobFile(firstFile, log, ref errors);

                var product = CodeFileParser.MakeShortProductName(firstFile.FullProductName);
                var normalSettings = new LabelSettings();
                var wrappedSettings = new LabelSettings { CodeBreaks = 1 };

                using var normal = LabelRenderer.Build(firstFile, 0, product, normalSettings);
                using var wrapped = LabelRenderer.Build(firstFile, 0, product, wrappedSettings);

                log.AppendLine($"Wrap test: высота текста {normal.Text.HeightPx} → {wrapped.Text.HeightPx} точек");
                if (wrapped.Text.HeightPx <= normal.Text.HeightPx)
                    Fail("Wrap test: перенос не увеличил высоту текста");
                else
                    Ok("Wrap test: текст переносится и становится выше");

                var split1 = LabelRenderer.WrapBalanced("abcdef", 1);
                var split2 = LabelRenderer.WrapBalanced("abcdef", 2);
                if (split1 != "abc\ndef" || split2 != "ab\ncd\nef")
                    Fail($"Wrap test: неверное деление '{split1}' / '{split2}'");
                else
                    Ok("Wrap test: деление пополам и на три части");

                var wrapZplPath = Path.Combine(directory, "selftest_wrap.zpl");
                File.WriteAllText(wrapZplPath, wrapped.BuildZpl(wrappedSettings), new UTF8Encoding(false));
                log.AppendLine("ZPL с переносом сохранён: " + wrapZplPath);
            }

            var expanded = CodeFileParser.NormalizeGs1(
                "(01)00000000000000(21)ABC(91)EE12(92)XYZ");
            log.AppendLine("Parentheses test: " + expanded.Replace("\u001D", "<GS>"));
            if (!expanded.StartsWith("0100000000000000", StringComparison.Ordinal) ||
                !expanded.Contains("21ABC\u001D91EE12\u001D92XYZ", StringComparison.Ordinal))
            {
                Fail("Parentheses test не прошёл");
            }
            else
            {
                Ok("Parentheses test");
            }

            var escaped = ZplFieldEncoder.Encode("01AB_CD\\EF^GH~IJ\u001D21X");
            log.AppendLine("Escape test: " + escaped);
            if (!escaped.StartsWith("\\1", StringComparison.Ordinal) ||
                !escaped.Contains("01AB_CD", StringComparison.Ordinal) ||
                !escaped.Contains("\\\\EF", StringComparison.Ordinal) ||
                !escaped.Contains("\\d094", StringComparison.Ordinal) ||
                !escaped.Contains("\\d126", StringComparison.Ordinal) ||
                !escaped.Contains("\\121X", StringComparison.Ordinal))
            {
                Fail("Escape test не прошёл");
            }
            else
            {
                Ok("Escape test");
            }

            if (firstZpl is not null)
            {
                var zplPath = Path.Combine(directory, "selftest_label.zpl");
                File.WriteAllText(zplPath, firstZpl, new UTF8Encoding(false));
                log.AppendLine("ZPL сохранён: " + zplPath);
            }

            if (underscoreZpl is not null)
            {
                var zplPath = Path.Combine(directory, "selftest_underscore.zpl");
                File.WriteAllText(zplPath, underscoreZpl, new UTF8Encoding(false));
                log.AppendLine("ZPL с '_' сохранён: " + zplPath);
            }
        }
        catch (Exception ex)
        {
            errors++;
            log.AppendLine("FATAL " + ex);
        }

        log.AppendLine();
        log.AppendLine(errors == 0 ? "RESULT: OK" : $"RESULT: FAIL ({errors})");

        try
        {
            var logPath = Path.Combine(directory, "selftest.log");
            File.WriteAllText(logPath, log.ToString(), new UTF8Encoding(false));
            Console.WriteLine(log.ToString());
            Console.WriteLine("Log: " + logPath);
        }
        catch
        {
            // ignore
        }

        return errors == 0 ? 0 : 1;
    }

    private static void TestJobFile(LabelFile file, StringBuilder log, ref int errors)
    {
        var dir = Path.Combine(Path.GetTempPath(), "gs1_label_printer_selftest");
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);

            using (var job = JobFileWriter.OpenOrCreate(
                       dir, file, "Тестовый продукт", "TEST-PRINTER", 1, "все коды", 2))
            {
                job.AppendSent(new[] { file.Codes[0], file.Codes[0] });
                job.Finish("первая печать");
            }

            var jobPath = JobFileWriter.GetJobFilePath(dir, file);
            if (!File.Exists(jobPath))
            {
                errors++;
                log.AppendLine("FAIL файл задания не создан");
                return;
            }

            using (var job = JobFileWriter.OpenOrCreate(
                       dir, file, "Тестовый продукт", "TEST-PRINTER", 1, "текущий код", 1))
            {
                job.AppendSent(new[] { file.Codes[1] });
                job.Finish("вторая печать");
            }

            var allLines = File.ReadAllLines(jobPath);
            var codeLines = allLines
                .Where(l => l.Length > 0 && !l.StartsWith('#'))
                .ToArray();

            if (codeLines.Length != 3)
            {
                errors++;
                log.AppendLine($"FAIL задание: ожидалось 3 кода, получено {codeLines.Length}");
            }
            else if (codeLines[0] != file.Codes[0] ||
                     codeLines[1] != file.Codes[0] ||
                     codeLines[2] != file.Codes[1])
            {
                errors++;
                log.AppendLine("FAIL задание: содержимое строк не совпало с отправленными кодами");
            }
            else
            {
                log.AppendLine("OK   задание: существующий файл дополнен, коды совпадают (3)");
            }

            var reloaded = CodeFileParser.Load(jobPath);
            if (reloaded.Codes.Count != 3)
            {
                errors++;
                log.AppendLine($"FAIL задание: повторное чтение дало {reloaded.Codes.Count} кодов");
            }
            else
            {
                log.AppendLine("OK   задание: повторное чтение и метаданные");
            }

            if (reloaded.FullProductName != file.FullProductName)
            {
                errors++;
                log.AppendLine("FAIL задание: продукт из метаданных не совпал");
            }

            if (!string.IsNullOrWhiteSpace(file.Gtin) && reloaded.Gtin != file.Gtin)
            {
                errors++;
                log.AppendLine("FAIL задание: GTIN из метаданных не совпал");
            }

            log.AppendLine("  файл задания: " + jobPath);
            log.AppendLine("  строк кода: " + reloaded.Codes.Count);
        }
        finally
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
            catch
            {
                // ignore
            }
        }
    }

    private static string Trim(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
