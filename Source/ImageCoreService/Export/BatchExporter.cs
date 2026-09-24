namespace ImageCoreService;

public enum ExportFormat { Pdf, Tiff }

/// <summary>
/// Settings-driven export of a whole page list: optional document splitting, file naming
/// from the pattern, then one PDF / TIFF per document. Used by the UI; UI-free itself.
/// </summary>
public static class BatchExporter
{
    /// <param name="explicitPath">When not splitting, the exact output file (from a Save
    /// dialog). When splitting, ignored -- files go to <paramref name="folder"/>.</param>
    public static List<string> Export(IReadOnlyList<string> pages, AppSettings settings, ExportFormat format,
        string folder, string? explicitPath, string? profileName,
        IProgress<WorkProgress>? progress = null, CancellationToken cancel = default)
    {
        List<DocumentGroup> docs = DocumentSplitter.Split(pages, settings.SplitMode, settings.SeparatorBarcodePrefix,
            settings.RemoveSeparatorPages, settings.BlankPageInkPercent, progress, cancel);
        string ext = format == ExportFormat.Pdf ? ".pdf" : ".tif";
        DateTime now = DateTime.Now;
        var outputs = new List<string>();

        for (int d = 0; d < docs.Count; d++)
        {
            cancel.ThrowIfCancellationRequested();
            DocumentGroup doc = docs[d];
            string path;
            if (docs.Count == 1 && !string.IsNullOrEmpty(explicitPath))
            {
                path = explicitPath;
            }
            else
            {
                Directory.CreateDirectory(folder);
                string name = FileNamer.Build(settings.FileNamePattern, now, d + 1, doc.Barcode, profileName);
                path = FileNamer.UniquePath(folder, name, ext);
            }

            var docProgress = progress == null ? null : new SyncProgress<WorkProgress>(p =>
                progress.Report(p with { Message = docs.Count > 1 ? $"Tài liệu {d + 1}/{docs.Count}: {p.Message}" : p.Message }));
            ExportOptions options = ExportOptions.FromSettings(settings);
            if (format == ExportFormat.Pdf)
                DocumentExporter.ExportPdf(doc.Pages, options, path, docProgress, cancel);
            else
                DocumentExporter.ExportTiff(doc.Pages, options, path, docProgress, cancel);
            outputs.Add(path);
        }
        return outputs;
    }
}
