using System.Drawing;
using System.Drawing.Imaging;

namespace ImageCoreService;

/// <summary>
/// Turns any supported input file into page image files inside a project's pages folder:
/// PDF -> one PNG per page (PdfSplitter), multi-page TIFF -> one lossless file per frame,
/// other images -> copied byte-for-byte (so an original JPEG can later be embedded as-is).
/// </summary>
public static class PageImporter
{
    public static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".jpe", ".png", ".bmp", ".tif", ".tiff", ".gif" };

    public static bool IsSupported(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext == ".pdf" || ImageExtensions.Contains(ext);
    }

    public static List<string> Import(string file, string pagesFolder)
    {
        Directory.CreateDirectory(pagesFolder);
        string ext = Path.GetExtension(file).ToLowerInvariant();

        if (ext == ".pdf")
        {
            // Render into a private temp folder, then move with unique names: PdfSplitter
            // names pages after the PDF, so importing the same PDF twice would collide.
            string tmp = Path.Combine(pagesFolder, "_pdf_" + Guid.NewGuid().ToString("N"));
            try
            {
                var result = new List<string>();
                foreach (string page in PdfSplitter.SplitToImages(file, tmp))
                {
                    string dest = Path.Combine(pagesFolder, Guid.NewGuid().ToString("N") + ".png");
                    File.Move(page, dest);
                    result.Add(dest);
                }
                return result;
            }
            finally
            {
                try { Directory.Delete(tmp, true); } catch { /* best effort */ }
            }
        }

        if (ext is ".tif" or ".tiff" or ".gif")
        {
            using Bitmap bmp = ImageUtils.Load(file);
            int frames = bmp.GetFrameCount(FrameDimension.Page);
            if (frames > 1)
            {
                var result = new List<string>(frames);
                for (int i = 0; i < frames; i++)
                {
                    bmp.SelectActiveFrame(FrameDimension.Page, i);
                    using Bitmap frame = bmp.Clone(new Rectangle(0, 0, bmp.Width, bmp.Height), bmp.PixelFormat); // active frame only, same format
                    frame.SetResolution(bmp.HorizontalResolution, bmp.VerticalResolution);
                    result.Add(ImageUtils.SaveLossless(frame, Path.Combine(pagesFolder, Guid.NewGuid().ToString("N"))));
                }
                return result;
            }
        }

        string copy = Path.Combine(pagesFolder, Guid.NewGuid().ToString("N") + ext);
        File.Copy(file, copy);
        return new List<string> { copy };
    }
}
