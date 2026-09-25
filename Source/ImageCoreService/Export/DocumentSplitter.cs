using System.Drawing;
using ZXing;
using ZXing.Common;

namespace ImageCoreService;

/// <summary>One output document: its pages in order, and the separator barcode that
/// started it (available to the file-name pattern as {barcode}).</summary>
public sealed record DocumentGroup(IReadOnlyList<PageRecord> Pages, string? Barcode);

/// <summary>Barcode reading via ZXing.Net (Apache-2.0): 1D + QR / DataMatrix / PDF417.</summary>
public static class BarcodeDetector
{
    public static string? Find(Bitmap page)
    {
        int dpi = ImageUtils.ResolveDpi(page);
        GrayImage gray = GrayImage.FromBitmap(page);
        // ~150-200 dpi is plenty for separator sheets and much faster than full resolution.
        if (dpi >= 300) gray = gray.Downscale(2);

        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                TryHarder = true,
                PossibleFormats = new[]
                {
                    BarcodeFormat.CODE_128, BarcodeFormat.CODE_39, BarcodeFormat.CODE_93, BarcodeFormat.EAN_13,
                    BarcodeFormat.EAN_8, BarcodeFormat.ITF, BarcodeFormat.QR_CODE, BarcodeFormat.DATA_MATRIX, BarcodeFormat.PDF_417,
                },
            },
        };
        var source = new RGBLuminanceSource(gray.Data, gray.Width, gray.Height, RGBLuminanceSource.BitmapFormat.Gray8);
        return reader.Decode(source)?.Text;
    }
}

/// <summary>
/// Splits one scanned batch into several documents: at blank pages (the blank page is a
/// separator and is dropped) or at pages carrying a separator barcode (optionally dropped).
/// </summary>
public static class DocumentSplitter
{
    public static List<DocumentGroup> Split(IReadOnlyList<string> files, DocumentSplitMode mode, string barcodePrefix,
        bool removeSeparatorPages, double blankInkPercent = PageAnalyzer.DefaultBlankInkPercent,
        IProgress<WorkProgress>? progress = null, CancellationToken cancel = default) =>
        Split(files.Select(f => PageRecord.FromFile(f, Path.GetFileName(f))).ToList(), mode, barcodePrefix,
            removeSeparatorPages, blankInkPercent, progress, cancel);

    public static List<DocumentGroup> Split(IReadOnlyList<PageRecord> pages, DocumentSplitMode mode, string barcodePrefix,
        bool removeSeparatorPages, double blankInkPercent = PageAnalyzer.DefaultBlankInkPercent,
        IProgress<WorkProgress>? progress = null, CancellationToken cancel = default)
    {
        if (mode == DocumentSplitMode.None || pages.Count == 0)
            return new List<DocumentGroup> { new(pages.ToList(), null) };

        var groups = new List<DocumentGroup>();
        var current = new List<PageRecord>();
        string? currentBarcode = null;

        for (int i = 0; i < pages.Count; i++)
        {
            cancel.ThrowIfCancellationRequested();
            progress?.Report(new WorkProgress(i + 1, pages.Count, $"Tìm dấu tách tài liệu {i + 1}/{pages.Count}"));

            // 200 dpi is plenty to find a blank sheet or a barcode, and far cheaper on big pages.
            using Bitmap bmp = PageRenderer.RenderFull(pages[i], 200);
            bool isSeparator;
            string? code = null;
            if (mode == DocumentSplitMode.BlankPage)
            {
                isSeparator = PageAnalyzer.IsBlank(GrayImage.FromBitmap(bmp), ImageUtils.ResolveDpi(bmp), blankInkPercent);
            }
            else
            {
                code = BarcodeDetector.Find(bmp);
                isSeparator = code != null && (barcodePrefix.Length == 0 || code.StartsWith(barcodePrefix, StringComparison.OrdinalIgnoreCase));
            }

            if (!isSeparator)
            {
                current.Add(pages[i]);
                continue;
            }

            if (current.Count > 0) groups.Add(new DocumentGroup(current, currentBarcode));
            current = new List<PageRecord>();
            currentBarcode = code;
            // Blank separators are always dropped; barcode sheets only when asked.
            if (mode == DocumentSplitMode.Barcode && !removeSeparatorPages) current.Add(pages[i]);
        }
        if (current.Count > 0) groups.Add(new DocumentGroup(current, currentBarcode));
        return groups;
    }
}
