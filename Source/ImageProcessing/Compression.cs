using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageProcessing
{
    /// <summary>Ma hoa anh sang cac dinh dang nen (JPEG, PNG, WebP) va nen thich ung theo dung luong muc tieu.</summary>
    public static class Compression
    {
        public static byte[] ToJpeg(Image<Rgba32> image, int quality = 85)
        {
            using var ms = new MemoryStream();
            image.Save(ms, new JpegEncoder
            {
                Quality = quality
            });
            return ms.ToArray();
        }

        public static byte[] ToPng(Image<Rgba32> image, PngCompressionLevel level = PngCompressionLevel.BestCompression)
        {
            using var ms = new MemoryStream();
            image.Save(ms, new PngEncoder { CompressionLevel = level });
            return ms.ToArray();
        }

        public static byte[] ToWebp(Image<Rgba32> image, int quality = 82)
        {
            using var ms = new MemoryStream();
            image.Save(ms, new WebpEncoder
            {
                Quality = quality,
                FileFormat = WebpFileFormatType.Lossy
            });
            return ms.ToArray();
        }

        /// <summary>
        /// Tim quality JPEG lon nhat sao cho dung luong nho hon hoac bang targetSizeKB (tim nhi phan, giong adaptive_compress ben Python).
        /// Tra ve du lieu da nen va quality da dung.
        /// </summary>
        public static (byte[] Data, int Quality) AdaptiveJpegCompress(
            Image<Rgba32> image, int targetSizeKB, int minQuality = 10, int maxQuality = 95)
        {
            int lo = minQuality, hi = maxQuality;
            byte[]? best = null;
            int bestQuality = minQuality;

            while (lo <= hi)
            {
                int mid = lo + (hi - lo) / 2;
                byte[] data = ToJpeg(image, mid);
                double sizeKb = data.Length / 1024.0;

                if (sizeKb <= targetSizeKB)
                {
                    best = data;
                    bestQuality = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            // Neu ngay ca minQuality cung khong dat muc tieu, tra ve ket qua tot nhat co the (nen o minQuality).
            best ??= ToJpeg(image, minQuality);
            return (best, bestQuality);
        }

        /// <summary>Tuong tu AdaptiveJpegCompress nhung cho WebP.</summary>
        public static (byte[] Data, int Quality) AdaptiveWebpCompress(
            Image<Rgba32> image, int targetSizeKB, int minQuality = 10, int maxQuality = 95)
        {
            int lo = minQuality, hi = maxQuality;
            byte[]? best = null;
            int bestQuality = minQuality;

            while (lo <= hi)
            {
                int mid = lo + (hi - lo) / 2;
                byte[] data = ToWebp(image, mid);
                double sizeKb = data.Length / 1024.0;

                if (sizeKb <= targetSizeKB)
                {
                    best = data;
                    bestQuality = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            best ??= ToWebp(image, minQuality);
            return (best, bestQuality);
        }
    }
}
