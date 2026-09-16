using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageProcessing
{
    public enum OutputFormat
    {
        Jpeg,
        Png,
        Webp
    }

    /// <summary>Tuy chon cho pipeline xu ly anh hoan chinh.</summary>
    public class ProcessOptions
    {
        public int MaxWidth { get; set; } = 1920;
        public int MaxHeight { get; set; } = 1080;

        public bool Denoise { get; set; } = false;
        public float DenoiseSigma { get; set; } = 0.6f;

        public bool Sharpen { get; set; } = true;
        public float SharpenSigma { get; set; } = 1.0f;
        public float SharpenStrength { get; set; } = 0.8f;

        public OutputFormat Format { get; set; } = OutputFormat.Jpeg;

        /// <summary>Quality co dinh (1-100). Bo qua neu TargetSizeKB duoc dat.</summary>
        public int Quality { get; set; } = 85;

        /// <summary>Neu dat, se tu dong tim quality lon nhat sao cho dung luong &lt;= gia tri nay (KB).</summary>
        public int? TargetSizeKB { get; set; }

        /// <summary>
        /// Xoa EXIF/ICC/IPTC/XMP metadata khoi anh dau ra (mac dinh true). Giam dung luong file
        /// (anh chup dien thoai/may anh co the chua vai chuc KB metadata + thumbnail nhung) va
        /// tranh lo du lieu nhay cam (vd toa do GPS) khi phan phoi lai anh qua web.
        /// </summary>
        public bool StripMetadata { get; set; } = true;

        /// <summary>
        /// Gioi han so megapixel (Width*Height/1_000_000) cua anh dau vao truoc khi decode toan bo pixel.
        /// Bao ve khoi "decompression bomb": file nen rat nho nhung khai bao kich thuoc khong lo,
        /// khien decode toan bo vao RAM gay OOM/DoS - quan trong khi nhan anh upload tu nguoi dung
        /// khong tin cay (vd endpoint ASP.NET Core). Mac dinh 40MP (~8000x5000) du cho anh chup thuc te.
        /// </summary>
        public double MaxInputMegapixels { get; set; } = 40.0;
    }

    /// <summary>Nem ra khi anh dau vao vuot qua <see cref="ProcessOptions.MaxInputMegapixels"/>.</summary>
    public sealed class ImageTooLargeException : Exception
    {
        public ImageTooLargeException(string message) : base(message)
        {
        }
    }

    /// <summary>Ket qua tra ve cua ImagePipeline.Process.</summary>
    public class ProcessResult
    {
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public int UsedQuality { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public long OriginalBytes { get; set; }
        public long OutputBytes { get; set; }
        public double? Psnr { get; set; }
        public double? Ssim { get; set; }

        public double CompressionRatio => OriginalBytes > 0 ? (double)OutputBytes / OriginalBytes : 0;
    }

    /// <summary>
    /// Pipeline hoan chinh: kiem tra kich thuoc -> resize -> (denoise) -> (sharpen) -> (xoa metadata) -> nen -> (do PSNR/SSIM).
    /// Dung chung duoc cho ASP.NET Core, ASP.NET Web Forms, WinForms, UWP vi chi phu thuoc netstandard2.0 + ImageSharp
    /// (khong dung System.Drawing, khong native dependency).
    /// </summary>
    public static class ImagePipeline
    {
        public static ProcessResult Process(byte[] input, ProcessOptions options, bool computeMetrics = false)
        {
            GuardInputSize(input, options.MaxInputMegapixels);

            using var image = ImageIO.Load(input);

            // Resize truoc roi moi denoise/sharpen: vua nhanh hon nhieu tren anh nguon lon
            // (khu nhieu tren anh 24MP roi lai vut di phan lon khi resize xuong 1920x1080 la lang phi),
            // vua giup DenoiseSigma/SharpenSigma cho ket qua nhat quan bat ke kich thuoc anh dau vao.
            ImageIO.SmartResize(image, options.MaxWidth, options.MaxHeight);

            if (options.Denoise)
                Sharpening.Denoise(image, options.DenoiseSigma);

            if (options.Sharpen)
                Sharpening.UnsharpMask(image, options.SharpenSigma, options.SharpenStrength);

            if (options.StripMetadata)
            {
                image.Metadata.ExifProfile = null;
                image.Metadata.IccProfile = null;
                image.Metadata.IptcProfile = null;
                image.Metadata.XmpProfile = null;
            }

            // Chup lai anh ngay truoc khi nen (sau resize/denoise/sharpen) de do PSNR/SSIM phan anh
            // dung "muc suy giam do nen gay ra", tach biet voi thay doi co chu dich tu sharpen/resize.
            using var preCompression = computeMetrics ? image.Clone() : null;

            byte[] outputData;
            int usedQuality = options.Quality;

            switch (options.Format)
            {
                case OutputFormat.Png:
                    outputData = Compression.ToPng(image);
                    break;

                case OutputFormat.Webp:
                    if (options.TargetSizeKB.HasValue)
                    {
                        var (data, q) = Compression.AdaptiveWebpCompress(image, options.TargetSizeKB.Value);
                        outputData = data;
                        usedQuality = q;
                    }
                    else
                    {
                        outputData = Compression.ToWebp(image, options.Quality);
                    }
                    break;

                default: // Jpeg
                    if (options.TargetSizeKB.HasValue)
                    {
                        var (data, q) = Compression.AdaptiveJpegCompress(image, options.TargetSizeKB.Value);
                        outputData = data;
                        usedQuality = q;
                    }
                    else
                    {
                        outputData = Compression.ToJpeg(image, options.Quality);
                    }
                    break;
            }

            var result = new ProcessResult
            {
                Data = outputData,
                UsedQuality = usedQuality,
                Width = image.Width,
                Height = image.Height,
                OriginalBytes = input.Length,
                OutputBytes = outputData.Length
            };

            if (computeMetrics && preCompression is not null)
            {
                using var outputDecoded = ImageIO.Load(outputData);

                result.Psnr = QualityMetrics.Psnr(preCompression, outputDecoded);
                result.Ssim = QualityMetrics.Ssim(preCompression, outputDecoded);
            }

            return result;
        }

        /// <summary>
        /// Kiem tra kich thuoc anh qua metadata header (khong decode toan bo pixel) truoc khi goi ImageIO.Load,
        /// de tranh "decompression bomb" lam OOM khi xu ly anh tu nguon khong tin cay.
        /// </summary>
        private static void GuardInputSize(byte[] input, double maxMegapixels)
        {
            var info = Image.Identify(input);
            if (info is null) return; // de ImageIO.Load tu bao loi dinh dang ro rang hon

            double megapixels = (double)info.Width * info.Height / 1_000_000.0;
            if (megapixels > maxMegapixels)
            {
                throw new ImageTooLargeException(
                    $"Anh {info.Width}x{info.Height} ({megapixels:F1} MP) vuot gioi han {maxMegapixels:F0} MP cho phep.");
            }
        }
    }
}
