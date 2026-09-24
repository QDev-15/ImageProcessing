using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;

namespace ImageCoreService;

/// <summary>
/// Plain baseline JPEG via GDI+'s own encoder (no external tool/library needed --
/// unlike OpenJpegEncoder's opj_compress.exe wrapper). Good enough for a comparison
/// bench; the main app uses a separate libjpeg-turbo P/Invoke wrapper for its own
/// JPEG path (IMIP.OpenImaging.Internal.JpegEncoder), not ported here since GDI+'s
/// output is what most tooling "just has available" without vendoring anything extra.
/// </summary>
public static class JpegEncoderSimple
{
    public static byte[] Encode(Bitmap rgb24, int quality = 85)
    {
        ImageCodecInfo jpegCodec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        using var ep = new EncoderParameters(1);
        ep.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);
        using var ms = new MemoryStream();
        rgb24.Save(ms, jpegCodec, ep);
        return ms.ToArray();
    }
}
