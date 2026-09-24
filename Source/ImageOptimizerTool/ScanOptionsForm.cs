using NTwain.Data;
using System.Windows.Forms;

namespace ImageOptimizerTool;

internal partial class ScanOptionsForm : Form
{
    private readonly IReadOnlyList<TWIdentityWrapper> _sources;

    public TWIdentityWrapper? SelectedSource { get; private set; }
    public int SelectedDpi { get; private set; } = 200;
    public ScanColorMode SelectedColorMode { get; private set; } = ScanColorMode.BlackAndWhite;
    public bool Duplex { get; private set; }
    public bool ShowDriverUi { get; private set; }

    public ScanOptionsForm(IReadOnlyList<TWIdentityWrapper> sources)
    {
        InitializeComponent();
        _sources = sources;

        foreach (TWIdentityWrapper s in _sources)
            cboSource.Items.Add(s.ProductName.ToString());
        if (cboSource.Items.Count > 0)
            cboSource.SelectedIndex = 0;

        cboDpi.SelectedItem = "200";
        cboColorMode.SelectedIndex = 0; // Black & White, per spec default
    }

    private void btnScan_Click(object? sender, EventArgs e)
    {
        if (cboSource.SelectedIndex < 0)
        {
            MessageBox.Show(this, "No scan source available. Is a scanner connected and its TWAIN driver installed?",
                "Scan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SelectedSource = _sources[cboSource.SelectedIndex];
        SelectedDpi = int.TryParse(cboDpi.Text, out int dpi) ? dpi : 200;
        SelectedColorMode = cboColorMode.SelectedIndex switch
        {
            0 => ScanColorMode.BlackAndWhite,
            1 => ScanColorMode.Gray,
            _ => ScanColorMode.Color,
        };
        Duplex = chkDuplex.Checked;
        ShowDriverUi = chkShowDriverUi.Checked;

        DialogResult = DialogResult.OK;
        Close();
    }
}
