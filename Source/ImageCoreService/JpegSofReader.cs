namespace ImageCoreService;

/// <summary>
/// Minimal JPEG SOF0 parser: just the first component's H/V sampling factors,
/// needed to set TIFF's YCbCrSubSampling tag to match an embedded raw JPEG strip
/// exactly -- a mismatched tag is a real spec violation some readers reject outright
/// (the main app's IMIP.OpenImaging.Internal.TiffUtils.WriteColorJpegDirectory hit
/// exactly this with its own libjpeg-turbo-based encoder, which happened to emit
/// 4:4:4 instead of the "obvious" 4:2:0 default -- so this reads the real bytes
/// rather than assuming any particular sampling).
/// </summary>
public static class JpegSofReader
{
    public static (int H, int V) ReadComponent0Sampling(byte[] jpeg)
    {
        int i = 2;
        while (i + 4 <= jpeg.Length)
        {
            if (jpeg[i] != 0xFF) { i++; continue; }
            byte marker = jpeg[i + 1];
            if (marker == 0xD9 || marker == 0xDA) break;         // EOI / start of scan
            if (marker == 0xFF) { i++; continue; }                // fill byte
            if (marker >= 0xD0 && marker <= 0xD7) { i += 2; continue; } // RSTn (no length)

            int len = (jpeg[i + 2] << 8) | jpeg[i + 3];
            if (len < 2 || i + 2 + len > jpeg.Length) break;
            int seg = i + 4;

            bool isSof = marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
            if (isSof && len >= 8)
            {
                // seg+5 = component count, seg+6 = component 0 id, seg+7 = Hi<<4|Vi.
                byte sampling = jpeg[seg + 7];
                return (sampling >> 4, sampling & 0x0F);
            }
            i += 2 + len;
        }
        return (2, 2); // fallback: standard 4:2:0 if SOF wasn't found (shouldn't happen here).
    }
}
