using ImageProcessing;

namespace WinFormsDemo;

/// <summary>
/// App WinForms de test truc quan thu vien ImageProcessing: chon anh, tuy chinh tham so,
/// xem truoc/sau va so sanh dung luong/PSNR/SSIM.
/// Giao dien nam trong MainForm.Designer.cs - mo bang Visual Studio Designer (Shift+F7 hoac
/// chuot phai file .cs -> View Designer) de keo tha chinh sua binh thuong.
/// </summary>
public partial class MainForm : Form
{
    private byte[]? _processedData;
    private string _processedExtension = ".jpg";
    private byte[] input;

    public MainForm()
    {
        InitializeComponent();
    }

    private async void ButtonPickAndProcess_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Anh|*.jpg;*.jpeg;*.png;*.bmp;*.gif|Tat ca file|*.*"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            input = File.ReadAllBytes(dialog.FileName);
            btnRefresh.Enabled = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Khong doc duoc file: {ex.Message}", "Loi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        ProcessingImage();

    }
    private async void ProcessingImage()
    {
        SetBusy(true);
        try
        {
            var options = BuildOptionsFromUi();

            var result = await Task.Run(() => ImagePipeline.Process(input, options, computeMetrics: true));

            _processedData = result.Data;
            _processedExtension = options.Format switch
            {
                OutputFormat.Png => ".png",
                OutputFormat.Webp => ".webp",
                _ => ".jpg"
            };

            ShowImage(pictureBoxBefore, input);
            ShowImage(pictureBoxAfter, result.Data);

            textBoxResult.Text =
                $"Kich thuoc sau xu ly : {result.Width} x {result.Height}\r\n" +
                $"Dung luong           : {result.OriginalBytes / 1024.0:F1} KB -> {result.OutputBytes / 1024.0:F1} KB (ty le {result.CompressionRatio:P1})\r\n" +
                $"Quality da dung      : {result.UsedQuality}\r\n" +
                $"PSNR / SSIM          : {result.Psnr:F2} dB / {result.Ssim:F4}";

            buttonSave.Enabled = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Xu ly that bai: {ex.Message}", "Loi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ButtonSave_Click(object? sender, EventArgs e)
    {
        if (_processedData is null) return;

        using var dialog = new SaveFileDialog
        {
            Filter = $"Anh da xu ly|*{_processedExtension}",
            FileName = "processed" + _processedExtension
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        File.WriteAllBytes(dialog.FileName, _processedData);
        MessageBox.Show(this, "Da luu thanh cong.", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
        btnRefresh.Enabled = false;
    }

    private void CheckBoxTargetSize_CheckedChanged(object? sender, EventArgs e)
    {
        numericUpDownTargetKb.Enabled = checkBoxTargetSize.Checked;
        numericUpDownQuality.Enabled = !checkBoxTargetSize.Checked;
    }

    private ProcessOptions BuildOptionsFromUi()
    {
        var format = comboBoxFormat.SelectedItem?.ToString() switch
        {
            "PNG" => OutputFormat.Png,
            "WebP" => OutputFormat.Webp,
            _ => OutputFormat.Jpeg
        };

        return new ProcessOptions
        {
            MaxWidth = 1920,
            MaxHeight = 1080,
            Denoise = checkBoxDenoise.Checked,
            Sharpen = checkBoxSharpen.Checked,
            SharpenSigma = (float)numericUpDownSigma.Value,
            SharpenStrength = (float)numericUpDownStrength.Value,
            Format = format,
            Quality = (int)numericUpDownQuality.Value,
            TargetSizeKB = checkBoxTargetSize.Checked ? (int)numericUpDownTargetKb.Value : null
        };
    }

    private void SetBusy(bool busy)
    {
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        buttonPickAndProcess.Enabled = !busy;
    }

    private static void ShowImage(PictureBox box, byte[] data)
    {
        var bitmap = LoadBitmap(data);
        var old = box.Image;
        box.Image = bitmap;
        old?.Dispose();
    }

    private static Bitmap LoadBitmap(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var img = Image.FromStream(ms);
        return new Bitmap(img);
    }

    private void btnRefresh_Click(object sender, EventArgs e)
    {
        ProcessingImage();
    }
}
