namespace Gs1LabelPrinter.Core;

/// <summary>
/// Небольшой справочник GS1 Application Identifiers, достаточный для
/// человекочитаемого вида типовых GS1-кодов (01, 21, 91, 92 и т.п.).
/// </summary>
public static class AiCatalog
{
    private static readonly Dictionary<string, int> FixedLength = new(StringComparer.Ordinal)
    {
        // 2-значные AI
        ["00"] = 18,
        ["01"] = 14,
        ["02"] = 14,
        ["03"] = 14,
        ["04"] = 16,
        ["11"] = 6,
        ["12"] = 6,
        ["13"] = 6,
        ["14"] = 6,
        ["15"] = 6,
        ["16"] = 6,
        ["17"] = 6,
        ["18"] = 6,
        ["19"] = 6,
        ["20"] = 2,
        ["31"] = 6,
        ["32"] = 6,
        ["33"] = 6,
        ["34"] = 6,
        ["35"] = 6,
        ["36"] = 6,

        // 3-значные AI
        ["410"] = 13,
        ["411"] = 13,
        ["412"] = 13,
        ["413"] = 13,
        ["414"] = 13,
        ["415"] = 13,
        ["416"] = 13,
        ["417"] = 13,

        // 4-значные AI
        ["7001"] = 13,
        ["7003"] = 10,
        ["7005"] = 12,
        ["7006"] = 6,
        ["7009"] = 10,
        ["7010"] = 2,
        ["8005"] = 6,
        ["8006"] = 18,
        ["8011"] = 12,
        ["8017"] = 18,
        ["8018"] = 18,
        ["8019"] = 10,
        ["8026"] = 18
    };

    private static readonly HashSet<string> VariableLength = new(StringComparer.Ordinal)
    {
        "10", "21", "22", "30", "37", "39",
        "90", "91", "92", "93", "94", "95", "96", "97", "98", "99",

        "235", "240", "241", "242", "243", "250", "251", "253", "254", "255",
        "400", "401", "402", "403",
        "420", "421", "422", "423", "424", "425", "426", "427",
        "710", "711", "712", "713", "714", "715",

        "7002", "7004", "7007", "7008", "7011",
        "7020", "7021", "7022", "7023", "7040",
        "8001", "8002", "8003", "8004", "8007", "8008", "8009", "8010",
        "8012", "8013", "8020", "8110", "8111", "8112", "8200"
    };

    public static bool IsVariable(string ai) => VariableLength.Contains(ai);

    public static bool TryReadAi(string s, int index, out string ai, out int? fixedLength)
    {
        ai = "";
        fixedLength = null;

        foreach (var length in new[] { 4, 3, 2 })
        {
            if (index + length > s.Length)
                continue;

            var candidate = s.Substring(index, length);
            if (!IsAsciiDigits(candidate))
                continue;

            if (FixedLength.TryGetValue(candidate, out var fl))
            {
                ai = candidate;
                fixedLength = fl;
                return true;
            }

            if (VariableLength.Contains(candidate))
            {
                ai = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool IsAsciiDigits(string s)
    {
        foreach (var c in s)
        {
            if (c is < '0' or > '9')
                return false;
        }
        return true;
    }
}
