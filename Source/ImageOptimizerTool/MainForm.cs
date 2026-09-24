using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using NTwain.Data;

namespace ImageOptimizerTool;

public partial class MainForm : Form
{
    private const string DefaultTempFolder = @"C:\imageTool\Temp";

    private readonly List<string> _pageFiles = new();
    private string _tempFolder = DefaultTempFolder;
    private TwainScanner? _scanner;

    public MainForm()
    {
        InitializeComponent();
        FormClosed += (_, _) => _scanner?.Dispose();
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        txtTempFolder.Text = _tempFolder;
        EnsureTempFolder();
        cboExportType.SelectedIndex = 0; // "B&W" default, per spec
        PopulateCodecOptions();
        UpdatePageCount();
    }

    #region Codec selection

    private void cboExportType_SelectedIndexChanged(object? sender, EventArgs e) => PopulateCodecOptions();

    /// <summary>
    /// Codec choices depend on B&amp;W vs Color, mirroring the main app's own codec
    /// split (bitonal pages: CCITT G4 or JBIG2; color/gray pages: JPEG or JPEG2000) --
    /// applies identically whether exporting to PDF or TIFF (see ExportPdf*/ExportTiff*).
    /// Defaults (JBIG2 / JPEG2000) match this tool's previous always-on behavior before
    /// codec selection existed, so re-running an old comparison without touching the
    /// combo gives the same result as before.
    /// </summary>
    private void PopulateCodecOptions()
    {
        cboCodec.Items.Clear();
        if (IsBlackAndWhite)
        {
            cboCodec.Items.AddRange(new object[] { "CCITT G4", "JBIG2" });
            cboCodec.SelectedIndex = 1;
        }
        else
        {
            cboCodec.Items.AddRange(new object[] { "JPEG", "JPEG2000 (OpenJPEG)" });
            cboCodec.SelectedIndex = 1;
        }
    }

    private string CodecName => cboCodec.SelectedItem as string ?? "";
    private bool UseJBig2OrJpeg2000 => cboCodec.SelectedIndex == 1;

    #endregion

    #region Temp folder

    private void txtTempFolder_Leave(object? sender, EventArgs e)
    {
        ApplyTempFolderFromTextBox();
    }

    private void btnBrowseTempFolder_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Choose the temp folder for imported pages",
            SelectedPath = Directory.Exists(_tempFolder) ? _tempFolder : DefaultTempFolder,
            ShowNewFolderButton = true,
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _tempFolder = dlg.SelectedPath;
            txtTempFolder.Text = _tempFolder;
            EnsureTempFolder();
        }
    }

    private void ApplyTempFolderFromTextBox()
    {
        string candidate = txtTempFolder.Text.Trim();
        if (string.IsNullOrEmpty(candidate))
        {
            txtTempFolder.Text = _tempFolder;
            return;
        }
        _tempFolder = candidate;
        EnsureTempFolder();
    }

    private void EnsureTempFolder()
    {
        try
        {
            Directory.CreateDirectory(_tempFolder);
        }
        catch (Exception ex)
        {
            SetStatus("Could not create temp folder: " + ex.Message);
        }
    }

    #endregion

    #region Import

    private void btnImportImages_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Import page images",
            Multiselect = true,
            Filter = "Image files (*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff)|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff|All files (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        ApplyTempFolderFromTextBox();
        EnsureTempFolder();

        int added = 0;
        foreach (string src in dlg.FileNames)
        {
            try
            {
                string dest = Path.Combine(_tempFolder, Guid.NewGuid().ToString("N") + Path.GetExtension(src));
                File.Copy(src, dest, overwrite: true);
                _pageFiles.Add(dest);
                lstPages.Items.Add(Path.GetFileName(dest));
                added++;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to import '{src}':\n{ex.Message}", "Import Images",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        UpdatePageCount();
        SetStatus($"Imported {added} image(s) into {_tempFolder}.");
    }

    private void btnImportPdf_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Import PDF",
            Filter = "PDF files (*.pdf)|*.pdf",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        ApplyTempFolderFromTextBox();
        EnsureTempFolder();

        Cursor = Cursors.WaitCursor;
        try
        {
            List<string> pages = PdfSplitter.SplitToImages(dlg.FileName, _tempFolder);
            foreach (string p in pages)
            {
                _pageFiles.Add(p);
                lstPages.Items.Add(Path.GetFileName(p));
            }
            UpdatePageCount();
            SetStatus($"Split '{Path.GetFileName(dlg.FileName)}' into {pages.Count} page(s), saved to {_tempFolder}.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Failed to split PDF:\n" + ex.Message, "Import PDF",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    #endregion

    #region Scan

    /// <summary>Lazily opens the TWAIN Data Source Manager on first use -- avoids
    /// spinning up NTwain's background message-pump thread (and failing loudly if no
    /// TWAIN driver is installed at all) for a session that never scans.</summary>
    private void EnsureScannerReady()
    {
        if (_scanner != null) return;

        var scanner = new TwainScanner();
        STS sts = scanner.OpenDsm();
        if (!sts.IsSuccess)
        {
            scanner.Dispose();
            throw new InvalidOperationException(
                $"Could not open the TWAIN Data Source Manager ({sts.RC}). Is a scanner driver installed?");
        }
        _scanner = scanner;
    }

    private void btnScan_Click(object? sender, EventArgs e)
    {
        try
        {
            EnsureScannerReady();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Scan", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        IReadOnlyList<TWIdentityWrapper> sources = _scanner!.GetSources();
        if (sources.Count == 0)
        {
            MessageBox.Show(this, "No TWAIN scan sources found. Is a scanner connected and its driver installed?",
                "Scan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dlg = new ScanOptionsForm(sources);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.SelectedSource == null) return;

        ApplyTempFolderFromTextBox();
        EnsureTempFolder();

        SetScanUiEnabled(false);
        SetStatus("Scanning...");

        _scanner.StartScan(dlg.SelectedSource, dlg.SelectedDpi, dlg.SelectedColorMode, dlg.Duplex, dlg.ShowDriverUi, _tempFolder,
            onPageScanned: path =>
            {
                _pageFiles.Add(path);
                lstPages.Items.Add(Path.GetFileName(path));
                UpdatePageCount();
                SetStatus($"Scanned page {_pageFiles.Count} ({Path.GetFileName(path)})...");
            },
            onError: ex =>
            {
                MessageBox.Show(this, "Scan failed:\n" + ex.Message, "Scan", MessageBoxButtons.OK, MessageBoxIcon.Error);
            },
            onFinished: () =>
            {
                SetScanUiEnabled(true);
                SetStatus($"Scan finished. {_pageFiles.Count} page(s) total.");
            });
    }

    private void SetScanUiEnabled(bool enabled)
    {
        btnScan.Enabled = enabled;
        btnImportImages.Enabled = enabled;
        btnImportPdf.Enabled = enabled;
    }

    #endregion

    #region List management / preview

    private void lstPages_SelectedIndexChanged(object? sender, EventArgs e)
    {
        picPreview.Image?.Dispose();
        picPreview.Image = null;

        int i = lstPages.SelectedIndex;
        if (i < 0 || i >= _pageFiles.Count) return;

        try
        {
            picPreview.Image = new Bitmap(_pageFiles[i]);
        }
        catch (Exception ex)
        {
            SetStatus("Could not preview page: " + ex.Message);
        }
    }

    private void btnRemoveSelected_Click(object? sender, EventArgs e)
    {
        var selected = lstPages.SelectedIndices.Cast<int>().OrderByDescending(x => x).ToList();
        if (selected.Count == 0) return;

        foreach (int i in selected)
        {
            _pageFiles.RemoveAt(i);
            lstPages.Items.RemoveAt(i);
        }
        UpdatePageCount();
        SetStatus($"Removed {selected.Count} page(s) from the list (files kept on disk).");
    }

    private void btnClearAll_Click(object? sender, EventArgs e)
    {
        _pageFiles.Clear();
        lstPages.Items.Clear();
        picPreview.Image?.Dispose();
        picPreview.Image = null;
        UpdatePageCount();
        SetStatus("Cleared the page list (files kept on disk).");
    }

    private void UpdatePageCount() => lblPageCount.Text = $"{_pageFiles.Count} page(s)";

    private bool IsBlackAndWhite => cboExportType.SelectedIndex == 0;

    #endregion

    #region Export

    private void btnExportPdf_Click(object? sender, EventArgs e)
    {
        if (!EnsureHasPages()) return;

        using var dlg = new SaveFileDialog { Title = "Export PDF", Filter = "PDF files (*.pdf)|*.pdf", FileName = "export.pdf" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        Cursor = Cursors.WaitCursor;
        try
        {
            if (IsBlackAndWhite)
                ExportPdfBlackAndWhite(dlg.FileName);
            else
                ExportPdfColor(dlg.FileName);

            long size = new FileInfo(dlg.FileName).Length;
            SetStatus($"Exported PDF ({(IsBlackAndWhite ? "B&W" : "Color")}, {CodecName}): " +
                      $"{dlg.FileName} -- {size / 1024.0:0.0} KB, {_pageFiles.Count} page(s).");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "PDF export failed:\n" + ex.Message, "Export PDF",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void ExportPdfBlackAndWhite(string destPath)
    {
        // Cap to 200dpi first (same fixed cap as the color path -- explicitly
        // requested for B&W too, unlike the main app where bitonal is left
        // untouched). External tools (jbig2.exe) read whichever file path we hand
        // them, so a downsampled page needs to be materialized to a temp file first.
        var (sourcePaths, cappedInfo, tempFiles) = PrepareCappedSources(_pageFiles);
        try
        {
            var builder = new PdfBuilder();
            if (UseJBig2OrJpeg2000) // JBIG2
            {
                // One jbig2.exe invocation over every page: repeated glyphs are
                // recognized and shared ACROSS pages via a single globals dictionary,
                // not just within each page -- the real advantage of batching a whole
                // document together, vs. G4 where every page stands alone regardless.
                JBig2Encoder.Result[] results = JBig2Encoder.EncodeSymbolMultiPage(sourcePaths);
                for (int i = 0; i < sourcePaths.Count; i++)
                {
                    (int width, int height, int dpi) = cappedInfo[i];
                    builder.AddJBig2Page(results[i].PageStream, results[i].GlobalsStream, width, height, dpi);
                }
            }
            else // CCITT G4
            {
                for (int i = 0; i < sourcePaths.Count; i++)
                {
                    (int width, int height, int dpi) = cappedInfo[i];
                    using var src = new Bitmap(sourcePaths[i]);
                    using Bitmap bitonal = ImageUtils.ToBitonal(src);
                    builder.AddCcittG4Page(G4Encoder.EncodeToG4(bitonal), width, height, dpi);
                }
            }
            builder.Save(destPath);
        }
        finally
        {
            foreach (string t in tempFiles) { try { File.Delete(t); } catch { /* best effort */ } }
        }
    }

    /// <summary>
    /// For each input file, applies ImageUtils.CapDpi and, if that actually
    /// downsampled the page, saves the result to a new temp PNG (returned in
    /// sourcePaths in place of the original) so external tools that only take a file
    /// path (jbig2.exe) see the capped pixels. Returns per-page (width, height, dpi)
    /// for PDF page sizing, and the list of temp files the caller must delete.
    /// </summary>
    internal static (List<string> sourcePaths, List<(int, int, int)> info, List<string> tempFiles) PrepareCappedSources(IReadOnlyList<string> files)
    {
        var sourcePaths = new List<string>(files.Count);
        var info = new List<(int, int, int)>(files.Count);
        var tempFiles = new List<string>();

        foreach (string file in files)
        {
            using var original = new Bitmap(file);
            Bitmap capped = ImageUtils.CapDpi(original);
            try
            {
                if (ReferenceEquals(capped, original))
                {
                    sourcePaths.Add(file);
                }
                else
                {
                    string tempPng = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".png");
                    capped.Save(tempPng, ImageFormat.Png);
                    sourcePaths.Add(tempPng);
                    tempFiles.Add(tempPng);
                }
                info.Add((capped.Width, capped.Height, ImageUtils.ResolveDpi(capped)));
            }
            finally
            {
                if (!ReferenceEquals(capped, original)) capped.Dispose();
            }
        }
        return (sourcePaths, info, tempFiles);
    }

    private void ExportPdfColor(string destPath)
    {
        // Cap to 200dpi first, same as the main app's ArchivePageFactory -- a page
        // scanned/imported above that is downsampled before encoding, not embedded
        // at full resolution.
        var (sourcePaths, cappedInfo, tempFiles) = PrepareCappedSources(_pageFiles);
        try
        {
            var builder = new PdfBuilder();
            for (int i = 0; i < sourcePaths.Count; i++)
            {
                (int width, int height, int dpi) = cappedInfo[i];
                if (UseJBig2OrJpeg2000) // JPEG2000
                {
                    byte[] jp2 = OpenJpegEncoder.Encode(sourcePaths[i], compressionRatio: 20.0);
                    builder.AddJpxPage(jp2, width, height, dpi);
                }
                else // JPEG
                {
                    using var src = new Bitmap(sourcePaths[i]);
                    using Bitmap rgb = ImageUtils.To24bpp(src);
                    builder.AddJpegPage(JpegEncoderSimple.Encode(rgb), width, height, dpi);
                }
            }
            builder.Save(destPath);
        }
        finally
        {
            foreach (string t in tempFiles) { try { File.Delete(t); } catch { /* best effort */ } }
        }
    }

    private void btnExportTiff_Click(object? sender, EventArgs e)
    {
        if (!EnsureHasPages()) return;

        using var dlg = new SaveFileDialog { Title = "Export TIFF", Filter = "TIFF files (*.tif)|*.tif", FileName = "export.tif" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        Cursor = Cursors.WaitCursor;
        try
        {
            if (IsBlackAndWhite)
                ExportTiffBlackAndWhite(dlg.FileName);
            else
                ExportTiffColor(dlg.FileName);

            long size = new FileInfo(dlg.FileName).Length;
            SetStatus($"Exported TIFF ({(IsBlackAndWhite ? "B&W" : "Color")}, {CodecName}): " +
                      $"{dlg.FileName} -- {size / 1024.0:0.0} KB, {_pageFiles.Count} page(s).");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "TIFF export failed:\n" + ex.Message, "Export TIFF",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void ExportTiffBlackAndWhite(string destPath)
    {
        var (sourcePaths, cappedInfo, tempFiles) = PrepareCappedSources(_pageFiles);
        try
        {
            if (UseJBig2OrJpeg2000) // JBIG2
            {
                // Per-page generic-region coding (NOT the symbol/shared-globals mode
                // used for PDF): TIFF has no equivalent of PDF's /JBIG2Globals indirect
                // reference, so each strip must be a fully self-contained stream.
                var pages = new List<(byte[] Bytes, int Width, int Height, int Dpi)>(sourcePaths.Count);
                foreach (string path in sourcePaths)
                {
                    JBig2Encoder.Result r = JBig2Encoder.EncodeGeneric(path);
                    using var src = new Bitmap(path);
                    pages.Add((r.PageStream, src.Width, src.Height, ImageUtils.ResolveDpi(src)));
                }
                TiffPagePacker.SaveJbig2(pages, destPath);
            }
            else // CCITT G4
            {
                var pages = new List<(byte[] Bytes, int Width, int Height, int Dpi)>(sourcePaths.Count);
                for (int i = 0; i < sourcePaths.Count; i++)
                {
                    (int width, int height, int dpi) = cappedInfo[i];
                    using var src = new Bitmap(sourcePaths[i]);
                    using Bitmap bitonal = ImageUtils.ToBitonal(src);
                    pages.Add((G4Encoder.EncodeToG4(bitonal), width, height, dpi));
                }
                TiffPagePacker.SaveCcittG4(pages, destPath);
            }
        }
        finally
        {
            foreach (string t in tempFiles) { try { File.Delete(t); } catch { /* best effort */ } }
        }
    }

    private void ExportTiffColor(string destPath)
    {
        var (sourcePaths, cappedInfo, tempFiles) = PrepareCappedSources(_pageFiles);
        try
        {
            if (UseJBig2OrJpeg2000) // JPEG2000
            {
                var pages = new List<(byte[] Bytes, int Width, int Height, int Dpi)>(sourcePaths.Count);
                for (int i = 0; i < sourcePaths.Count; i++)
                {
                    (int width, int height, int dpi) = cappedInfo[i];
                    byte[] jp2 = OpenJpegEncoder.Encode(sourcePaths[i], compressionRatio: 20.0);
                    pages.Add((jp2, width, height, dpi));
                }
                TiffPagePacker.SaveJpeg2000(pages, destPath);
            }
            else // JPEG
            {
                var pages = new List<(byte[] Bytes, int Width, int Height, int Dpi, int HSampling, int VSampling)>(sourcePaths.Count);
                for (int i = 0; i < sourcePaths.Count; i++)
                {
                    (int width, int height, int dpi) = cappedInfo[i];
                    using var src = new Bitmap(sourcePaths[i]);
                    using Bitmap rgb = ImageUtils.To24bpp(src);
                    byte[] jpeg = JpegEncoderSimple.Encode(rgb);
                    (int h, int v) = JpegSofReader.ReadComponent0Sampling(jpeg);
                    pages.Add((jpeg, width, height, dpi, h, v));
                }
                TiffPagePacker.SaveJpeg(pages, destPath);
            }
        }
        finally
        {
            foreach (string t in tempFiles) { try { File.Delete(t); } catch { /* best effort */ } }
        }
    }

    private bool EnsureHasPages()
    {
        if (_pageFiles.Count > 0) return true;
        MessageBox.Show(this, "Import at least one image or PDF first.", "No pages",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        return false;
    }

    #endregion

    private void SetStatus(string text) => lblStatus.Text = text;
}
