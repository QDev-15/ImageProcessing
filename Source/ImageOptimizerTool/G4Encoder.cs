using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using BitMiracle.LibTiff.Classic;

namespace ImageOptimizerTool;

/// <summary>
/// Baseline for comparison: encodes exactly like the main app's ArchivePageFactory
/// does today for bitonal pages (single-strip CCITT Group 4, MINISWHITE), via the
/// same GDI+ TIFF encoder pattern already validated in OpenImaging.Tests
/// (ExportPipelineTests.MakeCcittTiff). Returns just the raw G4 codestream bytes
/// (what actually gets embedded in the PDF via /CCITTFaxDecode) so the size
/// comparison against JBIG2 is apples-to-apples, not inflated by TIFF container
/// overhead.
/// </summary>
internal static class G4Encoder
{
    public static byte[] EncodeToG4(Bitmap bitonal1Bpp)
    {
        string temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".tif");
        try
        {
            ImageCodecInfo enc = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Tiff.Guid);
            using (var ep = new EncoderParameters(1))
            {
                ep.Param[0] = new EncoderParameter(Encoder.Compression, (long)EncoderValue.CompressionCCITT4);
                bitonal1Bpp.Save(temp, enc, ep);
            }

            using Tiff tiff = Tiff.Open(temp, "r") ?? throw new InvalidOperationException("Could not reopen temp G4 TIFF.");
            int size = (int)tiff.RawStripSize(0);
            var buffer = new byte[size];
            int read = tiff.ReadRawStrip(0, buffer, 0, size);
            if (read != size) Array.Resize(ref buffer, read);
            return buffer;
        }
        finally
        {
            try { File.Delete(temp); } catch { /* best effort */ }
        }
    }
}
