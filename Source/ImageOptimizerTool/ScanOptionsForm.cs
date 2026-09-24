using System.Windows.Forms;
using ImageCoreService;

namespace ImageOptimizerTool;

/// <summary>
/// Pick a scanner + profile and start scanning, or (forScan = false) just manage the saved
/// scan profiles. Profiles are edited in place in <see cref="AppSettings.ScanProfiles"/>;
/// the caller saves settings when <see cref="ProfilesChanged"/> is true.
/// </summary>
internal partial class ScanOptionsForm : Form
{
    private readonly AppSettings _settings;
    private readonly IReadOnlyList<ScanSource> _sources;
    private readonly bool _forScan;

    public ScanSource? SelectedSource { get; private set; }
    public ScanProfile? SelectedProfile { get; private set; }
    public bool ProfilesChanged { get; private set; }

    public ScanOptionsForm(AppSettings settings, IReadOnlyList<ScanSource> sources, bool forScan)
    {
        InitializeComponent();
        _settings = settings;
        _sources = sources;
        _forScan = forScan;

        if (_settings.ScanProfiles.Count == 0) _settings.ScanProfiles.Add(new ScanProfile());

        if (forScan)
        {
            foreach (ScanSource s in sources) cboSource.Items.Add(s);
            if (cboSource.Items.Count == 0)
            {
                cboSource.Items.Add("(Không tìm thấy máy scan TWAIN / WIA)");
                cboSource.Enabled = false;
                btnScan.Enabled = false;
            }
            cboSource.SelectedIndex = 0;
        }
        else
        {
            Text = "Profile máy scan";
            lblSource.Visible = cboSource.Visible = false;
            btnScan.Visible = false;
            AcceptButton = btnClose;
        }

        ReloadProfiles(_settings.ScanProfiles.FirstOrDefault(p => p.Name == _settings.DefaultProfileName) ?? _settings.ScanProfiles[0]);
    }

    private ScanProfile? CurrentProfile => cboProfile.SelectedItem as ScanProfile;

    private void ReloadProfiles(ScanProfile? select)
    {
        cboProfile.Items.Clear();
        foreach (ScanProfile p in _settings.ScanProfiles) cboProfile.Items.Add(p);
        cboProfile.SelectedItem = select ?? _settings.ScanProfiles.FirstOrDefault();
        btnDeleteProfile.Enabled = _settings.ScanProfiles.Count > 1;
    }

    private void cboProfile_SelectedIndexChanged(object? sender, EventArgs e)
    {
        ScanProfile? p = CurrentProfile;
        propertyGrid.SelectedObject = p;
        if (p == null || !_forScan || !cboSource.Enabled) return;

        // Pre-select the scanner this profile remembers (name match, driver if set).
        for (int i = 0; i < cboSource.Items.Count; i++)
        {
            if (cboSource.Items[i] is ScanSource s && s.Name == p.SourceName &&
                (p.Driver == ScanDriverPreference.Auto || p.Driver == s.Driver))
            {
                cboSource.SelectedIndex = i;
                break;
            }
        }
    }

    private void propertyGrid_PropertyValueChanged(object? s, PropertyValueChangedEventArgs e)
    {
        ProfilesChanged = true;
        if (CurrentProfile is { } p)
        {
            p.Dpi = Math.Clamp(p.Dpi, 50, 1200);
            if (string.IsNullOrWhiteSpace(p.Name)) p.Name = "Profile";
            ReloadProfiles(p); // refresh the combo text after a rename
        }
    }

    private void btnNewProfile_Click(object? sender, EventArgs e)
    {
        ScanProfile basis = CurrentProfile ?? new ScanProfile();
        var p = new ScanProfile
        {
            Name = UniqueName(basis.Name + " (mới)"),
            SourceName = basis.SourceName,
            Driver = basis.Driver,
            Dpi = basis.Dpi,
            ColorMode = basis.ColorMode,
            Duplex = basis.Duplex,
            UseFeeder = basis.UseFeeder,
            ShowDriverUi = basis.ShowDriverUi,
        };
        _settings.ScanProfiles.Add(p);
        ProfilesChanged = true;
        ReloadProfiles(p);
    }

    private void btnDeleteProfile_Click(object? sender, EventArgs e)
    {
        if (CurrentProfile is not { } p || _settings.ScanProfiles.Count <= 1) return;
        if (MessageBox.Show(this, $"Xoá profile \"{p.Name}\"?", "Profile", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _settings.ScanProfiles.Remove(p);
        ProfilesChanged = true;
        ReloadProfiles(null);
    }

    private string UniqueName(string name)
    {
        string n = name;
        for (int i = 2; _settings.ScanProfiles.Any(p => p.Name == n); i++) n = $"{name} {i}";
        return n;
    }

    private void btnScan_Click(object? sender, EventArgs e)
    {
        if (cboSource.SelectedItem is not ScanSource source || CurrentProfile is not { } profile)
        {
            MessageBox.Show(this, "Không có máy scan. Kiểm tra máy scan đã bật, cắm cáp và cài driver TWAIN / WIA.",
                "Scan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Remember the choice for next time.
        if (profile.SourceName != source.Name || _settings.DefaultProfileName != profile.Name)
        {
            profile.SourceName = source.Name;
            _settings.DefaultProfileName = profile.Name;
            ProfilesChanged = true;
        }
        SelectedSource = source;
        SelectedProfile = profile;
        DialogResult = DialogResult.OK;
        Close();
    }
}
