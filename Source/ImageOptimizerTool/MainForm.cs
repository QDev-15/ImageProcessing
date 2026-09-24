using System.Drawing;
using System.Windows.Forms;
using ImageCoreService;
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
    /// applies identically whether exporting to PDF or TIFF (see DocumentExporter).
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

    private ExportCodec SelectedCodec => IsBlackAndWhite
        ? (UseJBig2OrJpeg2000 ? ExportCodec.JBig2 : ExportCodec.CcittG4)
        : (UseJBig2OrJpeg2000 ? ExportCodec.Jpeg2000 : ExportCodec.Jpeg);

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
            DocumentExporter.ExportPdf(_pageFiles, SelectedCodec, dlg.FileName);

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

    private void btnExportTiff_Click(object? sender, EventArgs e)
    {
        if (!EnsureHasPages()) return;

        using var dlg = new SaveFileDialog { Title = "Export TIFF", Filter = "TIFF files (*.tif)|*.tif", FileName = "export.tif" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        Cursor = Cursors.WaitCursor;
        try
        {
            DocumentExporter.ExportTiff(_pageFiles, SelectedCodec, dlg.FileName);

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
