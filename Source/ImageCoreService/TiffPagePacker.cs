using BitMiracle.LibTiff.Classic;

namespace ImageCoreService;

/// <summary>
/// Multi-page TIFF export via BitMiracle, embedding each page's ALREADY-ENCODED bytes
/// verbatim as a raw strip -- same "embed as-is, never re-touch" principle as
/// PdfPagePacker, so all 4 codecs go through ONE consistent TIFF-assembly mechanism
/// (Compression/Photometric tags + WriteRawStrip + WriteDirectory per page) instead of
/// mixing GDI+'s own TIFF encoder (which only knows LZW/CCITT/RLE/None) with a
/// hand-rolled one.
///
/// JBIG2 (34661) and JPEG2000/JP2 (34712) ARE registered TIFF Compression tag values,
/// but reader support outside specialized tooling is spotty at best -- included here
/// for size/comparison testing, not as something to hand to an arbitrary TIFF viewer
/// and expect it to open.
/// </summary>
public static class TiffPagePacker
{
    private const Compression Jbig2Compression = (Compression)34661;
    private const Compression Jp2Compression = (Compression)34712;

    public static void SaveCcittG4(IReadOnlyList<(byte[] Bytes, int Width, int Height, int Dpi)> pages, string destPath) =>
        SaveBitonal(pages, destPath, Compression.CCITTFAX4);

    public static void SaveJbig2(IReadOnlyList<(byte[] Bytes, int Width, int Height, int Dpi)> pages, string destPath) =>
        SaveBitonal(pages, destPath, Jbig2Compression);

    private static void SaveBitonal(IReadOnlyList<(byte[] Bytes, int Width, int Height, int Dpi)> pages, string destPath, Compression compression)
    {
        if (pages.Count == 0) throw new ArgumentException("No pages to export.", nameof(pages));

        using Tiff tiff = Tiff.Open(destPath, "w") ?? throw new IOException("Cannot open TIFF for writing: " + destPath);
        foreach (var p in pages)
        {
            tiff.SetField(TiffTag.IMAGEWIDTH, p.Width);
            tiff.SetField(TiffTag.IMAGELENGTH, p.Height);
            tiff.SetField(TiffTag.BITSPERSAMPLE, 1);
            tiff.SetField(TiffTag.SAMPLESPERPIXEL, 1);
            tiff.SetField(TiffTag.ROWSPERSTRIP, p.Height);
            tiff.SetField(TiffTag.COMPRESSION, compression);
            tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISWHITE);
            tiff.SetField(TiffTag.FILLORDER, FillOrder.MSB2LSB);
            tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
            tiff.SetField(TiffTag.XRESOLUTION, (float)p.Dpi);
            tiff.SetField(TiffTag.YRESOLUTION, (float)p.Dpi);
            tiff.SetField(TiffTag.RESOLUTIONUNIT, ResUnit.INCH);
            tiff.WriteRawStrip(0, p.Bytes, p.Bytes.Length);
            tiff.WriteDirectory();
        }
    }

    public static void SaveJpeg(IReadOnlyList<(byte[] Bytes, int Width, int Height, int Dpi, int HSampling, int VSampling)> pages, string destPath)
    {
        if (pages.Count == 0) throw new ArgumentException("No pages to export.", nameof(pages));

        using Tiff tiff = Tiff.Open(destPath, "w") ?? throw new IOException("Cannot open TIFF for writing: " + destPath);
        foreach (var p in pages)
        {
            tiff.SetField(TiffTag.IMAGEWIDTH, p.Width);
            tiff.SetField(TiffTag.IMAGELENGTH, p.Height);
            tiff.SetField(TiffTag.SAMPLESPERPIXEL, 3);
            tiff.SetField(TiffTag.BITSPERSAMPLE, 8);
            tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.YCBCR);
            tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
            tiff.SetField(TiffTag.ROWSPERSTRIP, p.Height);
            tiff.SetField(TiffTag.COMPRESSION, Compression.JPEG);
            // Full quant/Huffman tables live inside the raw JPEG stream itself (no
            // shared JPEGTables tag) -- required since we're handing libtiff an
            // already-complete datastream, not asking it to encode anything.
            tiff.SetField(TiffTag.JPEGTABLESMODE, JpegTablesMode.NONE);
            tiff.SetField(TiffTag.YCBCRSUBSAMPLING, p.HSampling, p.VSampling);
            tiff.SetField(TiffTag.XRESOLUTION, (float)p.Dpi);
            tiff.SetField(TiffTag.YRESOLUTION, (float)p.Dpi);
            tiff.SetField(TiffTag.RESOLUTIONUNIT, ResUnit.INCH);
            tiff.WriteRawStrip(0, p.Bytes, p.Bytes.Length);
            tiff.WriteDirectory();
        }
    }

    public static void SaveJpeg2000(IReadOnlyList<(byte[] Bytes, int Width, int Height, int Dpi)> pages, string destPath)
    {
        if (pages.Count == 0) throw new ArgumentException("No pages to export.", nameof(pages));

        using Tiff tiff = Tiff.Open(destPath, "w") ?? throw new IOException("Cannot open TIFF for writing: " + destPath);
        foreach (var p in pages)
        {
            tiff.SetField(TiffTag.IMAGEWIDTH, p.Width);
            tiff.SetField(TiffTag.IMAGELENGTH, p.Height);
            tiff.SetField(TiffTag.SAMPLESPERPIXEL, 3);
            tiff.SetField(TiffTag.BITSPERSAMPLE, 8);
            tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.RGB);
            tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
            tiff.SetField(TiffTag.ROWSPERSTRIP, p.Height);
            tiff.SetField(TiffTag.COMPRESSION, Jp2Compression);
            tiff.SetField(TiffTag.XRESOLUTION, (float)p.Dpi);
            tiff.SetField(TiffTag.YRESOLUTION, (float)p.Dpi);
            tiff.SetField(TiffTag.RESOLUTIONUNIT, ResUnit.INCH);
            tiff.WriteRawStrip(0, p.Bytes, p.Bytes.Length);
            tiff.WriteDirectory();
        }
    }
}
