using System.Text;

namespace Gs1LabelPrinter.Core;

/// <summary>
/// Готовит данные для ^FD с escape-символом ^BX "\":
///  - в начале стоит FNC1 (GS1 DataMatrix);
///  - разделители GS (0x1D) превращаются в FNC1;
///  - служебные для ZPL символы не ломают команду.
/// Символ "_" в GS1-данных (CSET 82) является обычным и передаётся как есть.
/// </summary>
public static class ZplFieldEncoder
{
    public static string Encode(string rawCode)
    {
        var sb = new StringBuilder(rawCode.Length + 16);
        sb.Append("\\1"); // FNC1 в первой позиции

        foreach (var ch in rawCode)
        {
            switch (ch)
            {
                case CodeFileParser.GroupSeparator:
                    sb.Append("\\1");
                    break;

                case '\\':
                    sb.Append("\\\\");
                    break;

                case '^':
                    sb.Append("\\d094");
                    break;

                case '~':
                    sb.Append("\\d126");
                    break;

                default:
                    if (ch is >= ' ' and <= '~')
                    {
                        sb.Append(ch);
                    }
                    else
                    {
                        foreach (var b in Encoding.UTF8.GetBytes(ch.ToString()))
                            sb.Append("\\d").Append(b.ToString("D3"));
                    }
                    break;
            }
        }

        return sb.ToString();
    }
}
