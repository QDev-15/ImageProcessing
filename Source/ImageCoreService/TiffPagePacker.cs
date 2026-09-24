using BitMiracle.LibTiff.Classic;

namespace ImageCoreService;

public enum TiffPageCodec { CcittG4, JBig2, Jpeg, Jpeg2000 }

/// <summary>One already-encoded page. HSampling/VSampling only matter for JPEG,
/// Components for JPEG / JPEG2000 (1 = gray, 3 = color).</summary>
public sealed record TiffPage(TiffPageCodec Codec, byte[] Bytes, int Width, int Height, int DpiX, int DpiY,
    int HSampling = 2, int VSampling = 2, int Components = 3);

/// <summary>
/// Multi-page TIFF export via BitMiracle, embedding each page's ALREADY-ENCODED bytes
/// verbatim as a raw strip -- same "embed as-is, never re-touch" principle as the PDF
/// builder, so all codecs go through ONE consistent TIFF-assembly mechanism
/// (Compression/Photometric tags + WriteRawStrip + WriteDirectory per page). Pages may mix
/// codecs (a bitonal G4 page next to a color JPEG page) -- each IFD is self-describing.
///
/// JBIG2 (34661) and JPEG2000/JP2 (34712) ARE registered TIFF Compression tag values, but
/// reader support outside specialized tooling is spotty at best -- only G4 and JPEG TIFFs
/// should be handed to arbitrary viewers.
/// </summary>
public static class TiffPagePacker
{
    private const Compression Jbig2Compression = (Compression)34661;
    private const Compression Jp2Compression = (Compression)34712;

    public static void Save(IReadOnlyList<TiffPage> pages, string destPath)
    {
        if (pages.Count == 0) throw new ArgumentException("No pages to export.", nameof(pages));

        string tmp = destPath + ".partial";
        using (Tiff tiff = Tiff.Open(tmp, "w") ?? throw new IOException("Cannot open TIFF for writing: " + destPath))
        {
            for (int i = 0; i < pages.Count; i++)
            {
                TiffPage p = pages[i];
                tiff.SetField(TiffTag.IMAGEWIDTH, p.Width);
                tiff.SetField(TiffTag.IMAGELENGTH, p.Height);
                tiff.SetField(TiffTag.ROWSPERSTRIP, p.Height);
                tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
                tiff.SetField(TiffTag.XRESOLUTION, (float)p.DpiX);
                tiff.SetField(TiffTag.YRESOLUTION, (float)p.DpiY);
                tiff.SetField(TiffTag.RESOLUTIONUNIT, ResUnit.INCH);
                tiff.SetField(TiffTag.SUBFILETYPE, FileType.PAGE);
                tiff.SetField(TiffTag.PAGENUMBER, i, pages.Count);

                switch (p.Codec)
                {
                    case TiffPageCodec.CcittG4:
                    case TiffPageCodec.JBig2:
                        tiff.SetField(TiffTag.BITSPERSAMPLE, 1);
                        tiff.SetField(TiffTag.SAMPLESPERPIXEL, 1);
                        tiff.SetField(TiffTag.COMPRESSION, p.Codec == TiffPageCodec.CcittG4 ? Compression.CCITTFAX4 : Jbig2Compression);
                        tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISWHITE);
                        tiff.SetField(TiffTag.FILLORDER, FillOrder.MSB2LSB);
                        break;

                    case TiffPageCodec.Jpeg:
                        tiff.SetField(TiffTag.BITSPERSAMPLE, 8);
                        tiff.SetField(TiffTag.SAMPLESPERPIXEL, p.Components);
                        tiff.SetField(TiffTag.COMPRESSION, Compression.JPEG);
                        // Full quant/Huffman tables live inside the raw JPEG stream itself (no
                        // shared JPEGTables tag) -- required since we hand libtiff an
                        // already-complete datastream.
                        tiff.SetField(TiffTag.JPEGTABLESMODE, JpegTablesMode.NONE);
                        if (p.Components == 1)
                        {
                            tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISBLACK);
                        }
                        else
                        {
                            tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.YCBCR);
                            tiff.SetField(TiffTag.YCBCRSUBSAMPLING, p.HSampling, p.VSampling);
                        }
                        break;

                    case TiffPageCodec.Jpeg2000:
                        tiff.SetField(TiffTag.BITSPERSAMPLE, 8);
                        tiff.SetField(TiffTag.SAMPLESPERPIXEL, p.Components);
                        tiff.SetField(TiffTag.PHOTOMETRIC, p.Components == 1 ? Photometric.MINISBLACK : Photometric.RGB);
                        tiff.SetField(TiffTag.COMPRESSION, Jp2Compression);
                        break;
                }

                tiff.WriteRawStrip(0, p.Bytes, p.Bytes.Length);
                tiff.WriteDirectory();
            }
        }
        File.Move(tmp, destPath, overwrite: true);
    }
}
