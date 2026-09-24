using System.ComponentModel;
using System.Xml.Serialization;

namespace ImageCoreService;

[TypeConverter(typeof(EnumDescriptionConverter))]
public enum ScanDriverPreference
{
    [Description("Tự động (TWAIN, không có thì WIA)")] Auto,
    [Description("Chỉ TWAIN")] Twain,
    [Description("Chỉ WIA")] Wia,
}

[TypeConverter(typeof(EnumDescriptionConverter))]
public enum ColorOutputMode
{
    /// <summary>Per page: detect bitonal / gray / color and pick the codec for each.</summary>
    [Description("Tự động từng trang")] Auto,
    [Description("Trắng đen")] BlackAndWhite,
    [Description("Xám")] Gray,
    [Description("Màu")] Color,
}

[TypeConverter(typeof(EnumDescriptionConverter))]
public enum JBig2Mode
{
    /// <summary>Symbol dictionary (text-region) coding: 3-10x smaller than G4, but a
    /// pattern-matching codec -- a very similar glyph CAN be substituted (the 2013 Xerox
    /// "6 vs 8" problem). Threshold 0.92+ keeps that risk low.</summary>
    [Description("Symbol (nhỏ nhất)")] Symbol,
    /// <summary>Generic region coding: lossless, no substitution risk, still smaller than G4.</summary>
    [Description("Lossless (an toàn tuyệt đối)")] Lossless,
}

[TypeConverter(typeof(EnumDescriptionConverter))]
public enum DocumentSplitMode
{
    [Description("Không tách")] None,
    /// <summary>A blank page starts a new document (the blank page itself is dropped).</summary>
    [Description("Theo trang trắng")] BlankPage,
    /// <summary>A page carrying a barcode (optionally matching a prefix) starts a new document.</summary>
    [Description("Theo barcode")] Barcode,
}

/// <summary>
/// Every user-tunable option of the app. Serialized to XML (<see cref="SettingsStore"/>).
/// Category / DisplayName / Description drive the settings form's property grid, so a new
/// option only needs a property here to show up in the UI.
/// </summary>
[XmlRoot("AppSettings")]
public sealed class AppSettings
{
    // ---- Codec (shown as explicit checkboxes on the settings form) ----

    /// <summary>Unchecked -> CCITT G4 for black &amp; white pages.</summary>
    [Browsable(false)] public bool UseJBig2 { get; set; }

    /// <summary>Unchecked -> JPEG for gray / color pages.</summary>
    [Browsable(false)] public bool UseJpeg2000 { get; set; }

    // ---- Chung ----

    [Category("1. Chung"), DisplayName("Thư mục làm việc (temp)"),
     Description("Nơi lưu trang scan / import của phiên hiện tại (khi chưa lưu thành dự án). Để trống = %LocalAppData%\\ImageOptimizerTool\\Work.")]
    public string WorkFolder { get; set; } = "";

    public string GetWorkFolder() => string.IsNullOrWhiteSpace(WorkFolder) ? Path.Combine(AppPaths.DataFolder, "Work") : WorkFolder;

    [Category("1. Chung"), DisplayName("Thư mục xuất mặc định"),
     Description("Thư mục gợi ý khi xuất PDF / TIFF. Để trống = thư mục Documents.")]
    public string OutputFolder { get; set; } = "";

    [Category("1. Chung"), DisplayName("Kích thước thumbnail (px)"),
     Description("Chiều rộng ảnh thu nhỏ trong danh sách trang (60-300).")]
    public int ThumbnailSize { get; set; } = 120;

    [Category("1. Chung"), DisplayName("Số bước undo tối đa")]
    public int MaxUndoSteps { get; set; } = 50;

    // ---- Scan ----

    [Category("2. Scan"), DisplayName("Driver ưu tiên"),
     Description("Auto: dùng TWAIN, nếu máy không có nguồn TWAIN thì dùng WIA.")]
    public ScanDriverPreference ScanDriver { get; set; } = ScanDriverPreference.Auto;

    [Category("2. Scan"), DisplayName("Profile mặc định"),
     Description("Tên profile máy scan chọn sẵn khi mở hộp thoại Scan.")]
    public string DefaultProfileName { get; set; } = "Mặc định";

    [Browsable(false)]
    public List<ScanProfile> ScanProfiles { get; set; } = new();

    // ---- Xử lý ảnh ----

    [Category("3. Xử lý ảnh"), DisplayName("Tự xử lý khi scan / import"),
     Description("Chạy các bước xử lý bật bên dưới cho mỗi trang ngay khi scan / import.")]
    public bool AutoProcessOnImport { get; set; } = true;

    [Category("3. Xử lý ảnh"), DisplayName("Chỉnh nghiêng (deskew)")]
    public bool Deskew { get; set; } = true;

    [Category("3. Xử lý ảnh"), DisplayName("Cắt viền đen")]
    public bool CropBlackBorders { get; set; } = true;

    [Category("3. Xử lý ảnh"), DisplayName("Tự xoay đúng chiều"),
     Description("Dùng Tesseract OSD để phát hiện trang bị lộn ngược / nằm ngang.")]
    public bool AutoOrient { get; set; } = true;

    [Category("3. Xử lý ảnh"), DisplayName("Bỏ trang trắng"),
     Description("Tự bỏ trang trắng khi scan / import (vd. mặt sau trống khi scan 2 mặt).")]
    public bool RemoveBlankPages { get; set; } = true;

    [Category("3. Xử lý ảnh"), DisplayName("Ngưỡng trang trắng (% mực)"),
     Description("Trang có tỷ lệ mực nhỏ hơn giá trị này được coi là trắng. Mặc định 0.03.")]
    public double BlankPageInkPercent { get; set; } = PageAnalyzer.DefaultBlankInkPercent;

    [Category("3. Xử lý ảnh"), DisplayName("Khử đốm (despeckle)"),
     Description("Xoá chấm nhiễu nhỏ trên trang trắng đen khi xuất.")]
    public bool Despeckle { get; set; } = true;

    [Category("3. Xử lý ảnh"), DisplayName("Thuật toán trắng đen"),
     Description("Sauvola: ngưỡng thích nghi theo vùng (tốt cho giấy ngả màu, sáng không đều). Otsu: ngưỡng toàn trang.")]
    public BinarizationMethod Binarization { get; set; } = BinarizationMethod.Sauvola;

    [Category("3. Xử lý ảnh"), DisplayName("Sauvola k"),
     Description("0.2-0.5. Lớn hơn = chữ mảnh hơn, nền sạch hơn. Mặc định 0.34.")]
    public double SauvolaK { get; set; } = Binarizer.DefaultSauvolaK;

    // ---- Xuất file ----

    [Category("4. Xuất file"), DisplayName("Chế độ màu"),
     Description("Auto: tự nhận biết từng trang trắng đen / xám / màu và chọn codec phù hợp.")]
    public ColorOutputMode ColorMode { get; set; } = ColorOutputMode.Auto;

    [Category("4. Xuất file"), DisplayName("Chất lượng JPEG (1-100)")]
    public int JpegQuality { get; set; } = 90;

    [Category("4. Xuất file"), DisplayName("Giữ nguyên JPEG gốc"),
     Description("Trang là file JPEG chưa chỉnh sửa thì nhúng nguyên byte vào PDF (không nén lại lần 2).")]
    public bool PassThroughOriginalJpeg { get; set; } = true;

    [Category("4. Xuất file"), DisplayName("JPEG2000: tỷ lệ nén"),
     Description("OpenJPEG -r. 20 = khoảng 1/20 kích thước gốc. 0 = không mất dữ liệu.")]
    public double Jpeg2000Ratio { get; set; } = 20;

    [Category("4. Xuất file"), DisplayName("JBIG2: chế độ"),
     Description("Symbol: nhỏ nhất, có rủi ro thay nhầm ký tự rất giống nhau. Lossless: an toàn tuyệt đối.")]
    public JBig2Mode JBig2Mode { get; set; } = JBig2Mode.Symbol;

    [Category("4. Xuất file"), DisplayName("JBIG2: ngưỡng symbol"),
     Description("0.85-0.97. Cao hơn = an toàn hơn (ít gộp ký tự), file lớn hơn. Mặc định 0.92.")]
    public double JBig2Threshold { get; set; } = 0.92;

    [Category("4. Xuất file"), DisplayName("PDF/A-2b"),
     Description("Xuất PDF chuẩn lưu trữ PDF/A-2b (ISO 19005-2).")]
    public bool PdfA { get; set; } = true;

    [Category("4. Xuất file"), DisplayName("OCR (PDF tìm kiếm được)"),
     Description("Nhận dạng chữ bằng Tesseract, thêm lớp chữ ẩn dưới ảnh để tìm kiếm / copy.")]
    public bool Ocr { get; set; } = true;

    [Category("4. Xuất file"), DisplayName("Ngôn ngữ OCR"),
     Description("Mã Tesseract, nối bằng '+'. Có sẵn: vie, eng.")]
    public string OcrLanguages { get; set; } = "vie+eng";

    [Category("4. Xuất file"), DisplayName("Quy tắc đặt tên file"),
     Description("Biến: {date} {time} {yyyy} {MM} {dd} {HH} {mm} {ss} {counter} {barcode} {profile}. Vd: Scan_{date}_{time}_{counter}")]
    public string FileNamePattern { get; set; } = "Scan_{date}_{time}_{counter}";

    [Category("4. Xuất file"), DisplayName("Tách tài liệu"),
     Description("None: một file. BlankPage: trang trắng là dấu tách. Barcode: trang có barcode là dấu tách.")]
    public DocumentSplitMode SplitMode { get; set; } = DocumentSplitMode.None;

    [Category("4. Xuất file"), DisplayName("Barcode tách: tiền tố"),
     Description("Chỉ barcode bắt đầu bằng chuỗi này mới là dấu tách. Để trống = mọi barcode.")]
    public string SeparatorBarcodePrefix { get; set; } = "";

    [Category("4. Xuất file"), DisplayName("Barcode tách: bỏ trang tách"),
     Description("Bỏ trang chứa barcode tách khỏi tài liệu xuất ra.")]
    public bool RemoveSeparatorPages { get; set; } = true;

    // ---- Metadata ----

    [Category("5. Metadata PDF"), DisplayName("Tác giả")]
    public string MetaAuthor { get; set; } = "";

    [Category("5. Metadata PDF"), DisplayName("Tiêu đề"),
     Description("Để trống = dùng tên file.")]
    public string MetaTitle { get; set; } = "";

    [Category("5. Metadata PDF"), DisplayName("Chủ đề")]
    public string MetaSubject { get; set; } = "";

    [Category("5. Metadata PDF"), DisplayName("Từ khoá")]
    public string MetaKeywords { get; set; } = "";

    public AppSettings Clone()
    {
        var s = new XmlSerializer(typeof(AppSettings));
        using var ms = new MemoryStream();
        s.Serialize(ms, this);
        ms.Position = 0;
        return (AppSettings)s.Deserialize(ms)!;
    }

    /// <summary>Clamp values a hand-edited XML might have pushed out of range.</summary>
    public void Normalize()
    {
        ThumbnailSize = Math.Clamp(ThumbnailSize, 60, 300);
        MaxUndoSteps = Math.Clamp(MaxUndoSteps, 1, 500);
        JpegQuality = Math.Clamp(JpegQuality, 1, 100);
        Jpeg2000Ratio = Math.Clamp(Jpeg2000Ratio, 0, 200);
        JBig2Threshold = Math.Clamp(JBig2Threshold, 0.4, 0.99);
        SauvolaK = Math.Clamp(SauvolaK, 0.05, 1.0);
        BlankPageInkPercent = Math.Clamp(BlankPageInkPercent, 0, 5);
        if (string.IsNullOrWhiteSpace(OcrLanguages)) OcrLanguages = "vie+eng";
        if (string.IsNullOrWhiteSpace(FileNamePattern)) FileNamePattern = "Scan_{date}_{time}_{counter}";
        if (ScanProfiles.Count == 0) ScanProfiles.Add(new ScanProfile());
    }
}

/// <summary>A saved scanner configuration.</summary>
public sealed class ScanProfile
{
    [Category("Profile"), DisplayName("Tên profile")]
    public string Name { get; set; } = "Mặc định";

    [Category("Profile"), DisplayName("Máy scan"),
     Description("Tên nguồn TWAIN / WIA. Để trống = máy đầu tiên tìm thấy.")]
    public string SourceName { get; set; } = "";

    [Category("Profile"), DisplayName("Driver")]
    public ScanDriverPreference Driver { get; set; } = ScanDriverPreference.Auto;

    [Category("Profile"), DisplayName("DPI")]
    public int Dpi { get; set; } = 300;

    [Category("Profile"), DisplayName("Chế độ màu")]
    public ScanColorMode ColorMode { get; set; } = ScanColorMode.Color;

    [Category("Profile"), DisplayName("Scan 2 mặt (duplex)")]
    public bool Duplex { get; set; }

    [Category("Profile"), DisplayName("Dùng khay nạp giấy (ADF)")]
    public bool UseFeeder { get; set; } = true;

    [Category("Profile"), DisplayName("Hiện giao diện driver")]
    public bool ShowDriverUi { get; set; }

    public override string ToString() => Name;
}
