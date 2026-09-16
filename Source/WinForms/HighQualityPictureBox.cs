using System.Drawing.Drawing2D;

namespace WinFormsDemo;

/// <summary>
/// PictureBox mac dinh (SizeMode = Zoom) dung GDI+ InterpolationMode.Default (chat luong thap, gan nhu
/// nearest-neighbor) khi scale anh de vua khung - khien anh preview trong nhoe hon du lieu that su,
/// du file da xu ly/luu ra van net. Class nay ep dung noi suy chat luong cao khi ve de xem truoc dung.
/// </summary>
public class HighQualityPictureBox : PictureBox
{
    protected override void OnPaint(PaintEventArgs pe)
    {
        pe.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        pe.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        pe.Graphics.SmoothingMode = SmoothingMode.HighQuality;
        base.OnPaint(pe);
    }
}
