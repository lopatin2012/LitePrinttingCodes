using ZXing;
using ZXing.Common;
using ZXing.Datamatrix;
using ZXing.Datamatrix.Encoder;

namespace Gs1LabelPrinter.Core;

public static class DataMatrixEncoder
{
    /// <summary>
    /// Кодирует GS1 element string в ECC200 DataMatrix.
    /// ZXing сам добавляет FNC1 в начало и превращает 0x1D в FNC1.
    /// </summary>
    public static BitMatrix Encode(string rawGs1)
    {
        if (string.IsNullOrWhiteSpace(rawGs1))
            throw new ArgumentException(L.T("Пустой код DataMatrix."), nameof(rawGs1));

        var hints = new Dictionary<EncodeHintType, object>
        {
            [EncodeHintType.DATA_MATRIX_COMPACT] = true,
            [EncodeHintType.GS1_FORMAT] = true,
            [EncodeHintType.DATA_MATRIX_SHAPE] = SymbolShapeHint.FORCE_SQUARE,
            [EncodeHintType.MARGIN] = 0
        };

        var writer = new DataMatrixWriter();
        var matrix = writer.encode(
            rawGs1,
            BarcodeFormat.DATA_MATRIX,
            width: 0,
            height: 0,
            hints: hints);

        if (matrix.Width != matrix.Height)
            throw new InvalidOperationException(L.F(
                "Получился неквадратный DataMatrix {0}×{1}.", matrix.Width, matrix.Height));

        if (matrix.Width > 144)
            throw new InvalidOperationException(L.F(
                "Слишком большой DataMatrix ({0}×{1}). Максимум — 144×144.",
                matrix.Width, matrix.Height));

        return matrix;
    }
}
