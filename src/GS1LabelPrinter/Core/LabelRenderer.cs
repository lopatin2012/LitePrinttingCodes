using System.Globalization;
using System.Text;
using ZXing.Common;

namespace Gs1LabelPrinter.Core;

public sealed class LabelLayout
{
    public int DmLeft { get; init; }
    public int DmTop { get; init; }
    public int DmSizePx { get; init; }
    public int TextLeft { get; init; }
    public int TextTop { get; init; }
    public int TextWidth { get; init; }
    public int TextHeight { get; init; }
    public List<string> Warnings { get; init; } = new();
    public bool HasWarnings => Warnings.Count > 0;
}

public sealed class LabelRenderResult : IDisposable
{
    public BitMatrix Matrix { get; }
    public TextBlock Text { get; }
    public string RawCode { get; }
    public string HumanCode { get; }
    public string ProductText { get; }
    public int SymbolSize => Matrix.Width;

    public LabelRenderResult(
        BitMatrix matrix,
        TextBlock text,
        string rawCode,
        string humanCode,
        string productText)
    {
        Matrix = matrix;
        Text = text;
        RawCode = rawCode;
        HumanCode = humanCode;
        ProductText = productText;
    }

    public LabelLayout GetLayout(LabelSettings s)
    {
        var dmSize = SymbolSize * s.ModuleSizeDots;
        var textTop = s.DmY + dmSize + s.TextGapDots;

        var warnings = new List<string>();
        var dmRight = s.DmX + dmSize;
        var dmBottom = s.DmY + dmSize;

        if (s.DmX < 0 || s.DmY < 0 || dmRight > s.LabelWidthDots || dmBottom > s.LabelHeightDots)
        {
            warnings.Add(
                $"DataMatrix выходит за пределы этикетки ({dmSize}×{dmSize} точек, правый нижний угол {dmRight},{dmBottom}).");
        }

        if (Text.HeightPx > 0)
        {
            var textBottom = textTop + Text.HeightPx;
            var limit = s.LabelHeightDots - s.BottomMarginDots;
            if (textBottom > limit)
            {
                warnings.Add(
                    $"Текст не помещается по высоте: нужен ещё {textBottom - limit} точ. Поднимите DataMatrix или уменьшите шрифт.");
            }

            if (s.TextLeftDots + Text.LogicalWidth > s.LabelWidthDots)
                warnings.Add("Текст выходит за правую границу этикетки.");

            if (Text.ProductOverflow)
                warnings.Add("Надпись продукта не помещается в одну строку даже минимальным шрифтом.");

            if (Text.CodeOverflow)
                warnings.Add("Содержимое кода не помещается в одну строку даже минимальным шрифтом.");
        }

        if (s.FixSymbolSize && SymbolSize > 49)
            warnings.Add($"Символ {SymbolSize}×{SymbolSize} больше 49 модулей — размер не фиксируется в ZPL.");

        var textLeft = Math.Clamp(s.DmX, 0, Math.Max(0, s.LabelWidthDots - 40));

        return new LabelLayout
        {
            DmLeft = s.DmX,
            DmTop = s.DmY,
            DmSizePx = dmSize,
            TextLeft = textLeft,
            TextTop = textTop,
            TextWidth = Text.LogicalWidth,
            TextHeight = Text.HeightPx,
            Warnings = warnings
        };
    }

    public string BuildZpl(LabelSettings s)
    {
        var layout = GetLayout(s);
        var bx = BuildBxCommand(s);

        var sb = new StringBuilder(16_000);
        sb.Append("^XA\n");
        sb.Append("^PW").Append(s.LabelWidthDots.ToString(CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("^LL").Append(s.LabelHeightDots.ToString(CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("^CI28\n");

        sb.Append("^FO")
            .Append(s.DmX.ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append(s.DmY.ToString(CultureInfo.InvariantCulture))
            .Append(bx)
            .Append("^FD")
            .Append(ZplFieldEncoder.Encode(RawCode))
            .Append("^FS\n");

        if (Text.HeightPx > 0 && Text.PackedBits.Length > 0)
        {
            var totalBytes = Text.PackedBits.Length;
            var hex = Convert.ToHexString(Text.PackedBits);
            sb.Append("^FO")
                .Append(layout.TextLeft.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(layout.TextTop.ToString(CultureInfo.InvariantCulture))
                .Append("^GFA,")
                .Append(totalBytes.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(totalBytes.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(Text.BytesPerRow.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(hex)
                .Append("^FS\n");
        }

        sb.Append("^XZ\n");
        return sb.ToString();
    }

    private string BuildBxCommand(LabelSettings s)
    {
        var fields = new List<string>
        {
            "N",
            s.ModuleSizeDots.ToString(CultureInfo.InvariantCulture),
            "200"
        };

        if (s.FixSymbolSize && SymbolSize <= 49)
        {
            fields.Add(SymbolSize.ToString(CultureInfo.InvariantCulture));
            fields.Add(SymbolSize.ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            fields.Add("");
            fields.Add("");
        }

        fields.Add("");     // f — не используется при quality 200
        fields.Add("\\");   // g — escape-символ для FNC1
        fields.Add("1");    // a — квадратные модули

        return "^BX" + string.Join(",", fields);
    }

    public void Dispose() => Text.Dispose();
}

public static class LabelRenderer
{
    public static LabelRenderResult Build(
        LabelFile file,
        int codeIndex,
        string productText,
        LabelSettings settings)
    {
        if (codeIndex < 0 || codeIndex >= file.Codes.Count)
            throw new ArgumentOutOfRangeException(nameof(codeIndex));

        var raw = CodeFileParser.NormalizeGs1(file.Codes[codeIndex]);
        var human = CodeFileParser.HumanizeGs1(raw);
        var matrix = DataMatrixEncoder.Encode(raw);

        // Текст идёт под DataMatrix: при горизонтальном перемещении кода
        // текст начинается там же и переносится/уменьшается по ширине.
        var textLeft = Math.Clamp(settings.DmX, 0, Math.Max(0, settings.LabelWidthDots - 40));
        var textWidth = Math.Max(
            40,
            settings.LabelWidthDots - textLeft - settings.RightMarginDots);

        var codeLine = settings.ShowCodeText
            ? WrapBalanced(human, settings.CodeBreaks)
            : "";
        var text = TextBlockRenderer.Render(productText, codeLine, textWidth, settings);

        return new LabelRenderResult(matrix, text, raw, human, productText);
    }

    /// <summary>
    /// Делит строку кода на примерно равные части.
    /// 0 переносов — одна строка, 1 — две части (пополам), 2 — три и т.д.
    /// </summary>
    public static string WrapBalanced(string text, int breaks)
    {
        if (string.IsNullOrEmpty(text) || breaks <= 0)
            return text;

        var parts = Math.Min(breaks + 1, text.Length);
        if (parts <= 1)
            return text;

        var sb = new StringBuilder(text.Length + parts);
        var start = 0;

        for (var i = 0; i < parts; i++)
        {
            var remainingParts = parts - i;
            var take = (text.Length - start + remainingParts - 1) / remainingParts;
            if (take <= 0)
                break;

            if (i > 0)
                sb.Append('\n');

            sb.Append(text, start, take);
            start += take;
        }

        return sb.ToString();
    }
}
