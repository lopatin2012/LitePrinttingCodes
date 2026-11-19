using System.Text;

namespace Gs1LabelPrinter.Core;

/// <summary>
/// TXT-задание: в него построчно попадают коды, реально отправленные на принтер.
/// Если файл задания уже существует — новые коды добавляются в конец.
/// </summary>
public sealed class JobFileWriter : IDisposable
{
    private readonly StreamWriter _writer;
    private bool _closed;

    public string FilePath { get; }
    public bool AppendedToExisting { get; }
    public int WrittenCodes { get; private set; }

    private JobFileWriter(StreamWriter writer, string filePath, bool appendedToExisting)
    {
        _writer = writer;
        FilePath = filePath;
        AppendedToExisting = appendedToExisting;
    }

    public static string GetJobFilePath(string? folder, LabelFile file)
    {
        var directory = string.IsNullOrWhiteSpace(folder)
            ? AppData.DefaultJobFolder
            : folder.Trim();

        var product = Sanitize(CodeFileParser.MakeShortProductName(file.FullProductName));
        if (string.IsNullOrWhiteSpace(product))
            product = Sanitize(L.T("Без названия"));

        var key = !string.IsNullOrWhiteSpace(file.Gtin)
            ? file.Gtin.Trim()
            : Sanitize(Path.GetFileNameWithoutExtension(file.FileName));

        if (key.Length > 70)
            key = key[..70];
        if (product.Length > 55)
            product = product[..55].TrimEnd(' ', ',', '.', '-');

        if (Directory.Exists(directory))
        {
            try
            {
                // Если задание уже создавалось (возможно, с другой короткой надписью),
                // продолжаем именно его, а не создаём второй файл для того же GTIN.
                // Job_ — новый нейтральный префикс; Задание_ — старые файлы.
                foreach (var filePrefix in new[] { "Job_", "Задание_" })
                {
                    var exact = Path.Combine(directory, $"{filePrefix}{key}.txt");
                    if (File.Exists(exact))
                        return exact;

                    var prefix = $"{filePrefix}{key}_";
                    var existing = Directory
                        .EnumerateFiles(directory, prefix + "*.txt", SearchOption.TopDirectoryOnly)
                        .OrderBy(p => p, StringComparer.CurrentCultureIgnoreCase)
                        .FirstOrDefault();

                    if (existing is not null)
                        return existing;
                }
            }
            catch
            {
                // Если папка недоступна, ниже вернём новый путь — ошибка проявится при печати.
            }
        }

        var name = $"Job_{key}_{product}.txt";
        return Path.Combine(directory, name);
    }

    public static JobFileWriter OpenOrCreate(
        string? folder,
        LabelFile file,
        string printedProduct,
        string printer,
        int copies,
        string mode,
        int expectedCodes)
    {
        var path = GetJobFilePath(folder, file);
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
            throw new IOException(L.T("Не удалось определить папку для файла задания."));

        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception ex)
        {
            throw new IOException(
                L.F("Не удалось создать папку заданий:{0}{1}{0}{2}",
                    Environment.NewLine, directory, ex.Message),
                ex);
        }

        var exists = File.Exists(path);

        FileStream stream;
        try
        {
            stream = new FileStream(
                path,
                exists ? FileMode.Append : FileMode.Create,
                FileAccess.Write,
                FileShare.Read);
        }
        catch (Exception ex)
        {
            throw new IOException(
                L.F("Не удалось открыть файл задания:{0}{1}{0}{2}",
                    Environment.NewLine, path, ex.Message),
                ex);
        }

        if (!exists)
        {
            var bom = Encoding.UTF8.GetPreamble();
            stream.Write(bom, 0, bom.Length);
        }

        var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true
        };

        var job = new JobFileWriter(writer, path, exists);
        job.WriteHeaders(file, printedProduct, printer, copies, mode, expectedCodes);
        return job;
    }

    private void WriteHeaders(
        LabelFile file,
        string printedProduct,
        string printer,
        int copies,
        string mode,
        int expectedCodes)
    {
        var now = DateTime.Now;

        if (!AppendedToExisting)
        {
            _writer.WriteLine("# GS1 Label Printer — job file");
            _writer.WriteLine("# Codes sent to the printer are appended below, one per line.");
            _writer.WriteLine("# Created: " + now.ToString("yyyy-MM-dd HH:mm:ss"));
            _writer.WriteLine("# Source: " + file.FilePath);
            _writer.WriteLine("# Product: " + file.FullProductName);
            _writer.WriteLine("# GTIN: " + (file.Gtin ?? ""));
            _writer.WriteLine();
        }
        else
        {
            _writer.WriteLine();
        }

        _writer.WriteLine("# ---------------------------------------------------------------");
        _writer.WriteLine("# Print: " + now.ToString("yyyy-MM-dd HH:mm:ss"));
        _writer.WriteLine("# Printer: " + printer);
        _writer.WriteLine("# Mode: " + mode);
        _writer.WriteLine("# Copies per code: " + copies);
        _writer.WriteLine("# Expected labels: " + expectedCodes);
        _writer.WriteLine("# Label text: " + printedProduct);
        _writer.WriteLine("# ---------------------------------------------------------------");
    }

    public void AppendSent(IEnumerable<string> codes)
    {
        if (_closed)
            throw new ObjectDisposedException(nameof(JobFileWriter));

        var count = 0;
        foreach (var code in codes)
        {
            _writer.WriteLine(code);
            count++;
        }

        if (count > 0)
        {
            WrittenCodes += count;
            _writer.Flush();
        }
    }

    public void Finish(string? footer)
    {
        if (_closed)
            return;

        _closed = true;

        try
        {
            _writer.WriteLine("# Sent in this print: " + WrittenCodes);
            if (!string.IsNullOrWhiteSpace(footer))
                _writer.WriteLine("# " + footer.ReplaceLineEndings(" "));
            _writer.WriteLine();
            _writer.Flush();
        }
        finally
        {
            _writer.Dispose();
        }
    }

    public void Dispose()
    {
        if (!_closed)
            Finish(null);
    }

    private static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (invalid.Contains(ch))
                sb.Append(' ');
            else
                sb.Append(ch);
        }

        var result = string.Join(" ", sb.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return result.Trim(' ', '.', ',', '-', '_');
    }
}
