using System.Windows.Forms;
using ImageCoreService;

namespace ImageOptimizerTool;

/// <summary>
/// App settings editor. Every option comes from <see cref="AppSettings"/>' attributes through
/// the property grid, so new settings appear here automatically. Codecs are fixed, not settings:
/// bitonal pages always use CCITT G4, gray/color pages always JPEG (JBIG2 and JPEG2000 were both
/// removed from the app, owner's decision 2026-09-29 -- they added real export time for a size
/// win the owner decided was not worth it). Edits a copy; the caller saves <see cref="Result"/>
/// on OK.
/// </summary>
internal partial class SettingsForm : Form
{
    public AppSettings Result { get; private set; }

    public SettingsForm(AppSettings current)
    {
        InitializeComponent();
        Result = current.Clone();
        Bind();
        lblFile.Text = "File cấu hình: " + AppPaths.SettingsFile;
    }

    private void Bind()
    {
        propertyGrid.SelectedObject = Result;
    }

    private void btnDefaults_Click(object? sender, EventArgs e)
    {
        if (MessageBox.Show(this, "Khôi phục toàn bộ cài đặt về mặc định (profile máy scan được giữ lại)?", "Cài đặt",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        List<ScanProfile> profiles = Result.ScanProfiles;
        Result = SettingsStore.LoadDefaults();
        Result.ScanProfiles = profiles;
        Bind();
    }

    private void btnOK_Click(object? sender, EventArgs e)
    {
        Result.Normalize();

        if (Result.Ocr && !OcrEngine.IsLanguageAvailable(Result.OcrLanguages))
        {
            MessageBox.Show(this, $"Không tìm thấy dữ liệu ngôn ngữ OCR '{Result.OcrLanguages}' trong\n{OcrEngine.TessDataPath}\n\nCó sẵn: vie, eng.",
                "Cài đặt", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!string.IsNullOrWhiteSpace(Result.WorkFolder))
        {
            try { Directory.CreateDirectory(Result.WorkFolder); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Thư mục làm việc không hợp lệ:\n" + ex.Message, "Cài đặt", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }
        DialogResult = DialogResult.OK;
        Close();
    }
}
