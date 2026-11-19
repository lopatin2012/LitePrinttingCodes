using System.Text;
using System.Text.RegularExpressions;

namespace Gs1LabelPrinter.Core;

public static class CodeFileParser
{
    public const char GroupSeparator = '\u001D';

    private static readonly Regex NamedFilePattern = new(
        @"^DataMatrixGS1_(?<name>.+)_(?<gtin>\d{14})_(?<qty>\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex GenericFilePattern = new(
        @"^(?<name>.+)_(?<gtin>\d{14})_(?<qty>\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ParenthesizedAiPattern = new(
        @"\((?<ai>\d{2,4})\)",
        RegexOptions.CultureInvariant);

    public static LabelFile Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Файл не найден.", path);

        var bytes = File.ReadAllBytes(path);
        var text = DecodeText(bytes);

        var codes = new List<string>();
        string? metadataProduct = null;
        string? metadataGtin = null;
        var metadataQuantity = 0;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r').Trim();
            if (line.Length == 0)
                continue;

            if (line.StartsWith('#'))
            {
                // Комментарии файла задания; заодно читаем метаданные для повторной печати.
                if (TryReadMetadata(line, "# Продукт:", out var productValue))
                    metadataProduct = productValue;
                else if (TryReadMetadata(line, "# GTIN:", out var gtinValue))
                    metadataGtin = gtinValue;
                else if (TryReadMetadata(line, "# Ожидается этикеток:", out var qtyValue) &&
                         int.TryParse(qtyValue, out var qtyMeta) && qtyMeta > 0)
                    metadataQuantity = qtyMeta;

                continue;
            }

            var normalized = NormalizeGs1(line);
            if (normalized.Length == 0)
                continue;

            codes.Add(normalized);
        }

        if (codes.Count == 0)
            throw new InvalidDataException(
                "В файле нет ни одного кода. Ожидается обычный текст: один GS1-код в строке.");

        var fileName = Path.GetFileName(path);
        var stem = Path.GetFileNameWithoutExtension(path);

        var match = NamedFilePattern.Match(stem);
        if (!match.Success)
            match = GenericFilePattern.Match(stem);

        string fullName;
        string? gtin = null;
        var declaredQty = codes.Count;

        if (match.Success)
        {
            fullName = match.Groups["name"].Value.Trim();
            gtin = match.Groups["gtin"].Value;
            if (int.TryParse(match.Groups["qty"].Value, out var qty) && qty > 0)
                declaredQty = qty;
        }
        else
        {
            fullName = stem.Trim();
        }

        if (!string.IsNullOrWhiteSpace(metadataProduct))
            fullName = metadataProduct.Trim();
        if (!string.IsNullOrWhiteSpace(metadataGtin))
            gtin = metadataGtin.Trim();
        if (metadataQuantity > 0)
            declaredQty = metadataQuantity;

        if (string.IsNullOrWhiteSpace(fullName))
            fullName = "Без названия";

        return new LabelFile
        {
            FilePath = Path.GetFullPath(path),
            FileName = fileName,
            FullProductName = fullName,
            Gtin = gtin,
            DeclaredQuantity = declaredQty,
            Codes = codes
        };
    }

    private static bool TryReadMetadata(string line, string prefix, out string value)
    {
        value = "";
        if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        value = line[prefix.Length..].Trim();
        return true;
    }

    private static string DecodeText(byte[] bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

        var offset = bytes.Length >= 3 &&
                     bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF
            ? 3
            : 0;

        try
        {
            var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            return strictUtf8.GetString(bytes, offset, bytes.Length - offset);
        }
        catch (DecoderFallbackException)
        {
            // В таких файлах коды всё равно ASCII. Latin-1 сохраняет байты 1:1.
            return Encoding.Latin1.GetString(bytes, offset, bytes.Length - offset);
        }
    }

    /// <summary>
    /// Приводит строку к GS1 element string: убирает скобки, если они есть,
    /// и расставляет FNC1/GS между полями переменной длины.
    /// Ведущий FNC1 здесь не добавляется — его добавляет построитель ZPL.
    /// </summary>
    public static string NormalizeGs1(string input)
    {
        var s = input.Trim().Trim(GroupSeparator);
        if (s.Length == 0)
            return s;

        if (s[0] == '(' && ParenthesizedAiPattern.IsMatch(s))
        {
            var matches = ParenthesizedAiPattern.Matches(s);
            if (matches.Count > 0)
            {
                var sb = new StringBuilder();
                for (var i = 0; i < matches.Count; i++)
                {
                    var m = matches[i];
                    var ai = m.Groups["ai"].Value;
                    var valueStart = m.Index + m.Length;
                    var valueEnd = i + 1 < matches.Count ? matches[i + 1].Index : s.Length;
                    if (valueEnd < valueStart)
                        continue;

                    var value = s[valueStart..valueEnd].Trim();
                    sb.Append(ai).Append(value);

                    // GS ставится после поля переменной длины, если дальше есть ещё поля.
                    if (i + 1 < matches.Count && AiCatalog.IsVariable(ai))
                        sb.Append(GroupSeparator);
                }

                if (sb.Length > 0)
                    return sb.ToString();
            }
        }

        return s;
    }

    /// <summary>
    /// Человекочитаемое представление: (01)0460...(21)...
    /// </summary>
    public static string HumanizeGs1(string input)
    {
        var s = NormalizeGs1(input);
        if (s.Length == 0)
            return s;

        var sb = new StringBuilder(s.Length + 16);
        var i = 0;

        while (i < s.Length)
        {
            if (s[i] == GroupSeparator)
            {
                i++;
                continue;
            }

            if (!AiCatalog.TryReadAi(s, i, out var ai, out var fixedLength))
            {
                sb.Append(s[i]);
                i++;
                continue;
            }

            i += ai.Length;
            if (i > s.Length)
                break;

            sb.Append('(').Append(ai).Append(')');

            if (fixedLength is > 0)
            {
                var take = Math.Min(fixedLength.Value, s.Length - i);
                sb.Append(s, i, take);
                i += take;
            }
            else
            {
                var start = i;
                while (i < s.Length && s[i] != GroupSeparator)
                    i++;
                sb.Append(s, start, i - start);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Короткая надпись для этикетки, выводимая из имени файла.
    /// </summary>
    public static string MakeShortProductName(string fullName)
    {
        var s = Regex.Replace(fullName, @"\s*\([^)]*\)", " ");
        s = Regex.Replace(s, @"\s+для чистых помещений\b,?", " ",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        s = Regex.Replace(s, @"\s+", " ").Trim();
        s = Regex.Replace(s, @"\s*,\s*", ", ");
        s = s.Trim(' ', ',', ';', '-', '—');

        return string.IsNullOrWhiteSpace(s) ? fullName.Trim() : s;
    }
}
