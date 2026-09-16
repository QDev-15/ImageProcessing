using System;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ImageProcessing
{
    /// <summary>Cac thuat toan khu nhieu va lam net anh.</summary>
    public static class Sharpening
    {
        /// <summary>
        /// Khu nhieu nhe bang lam min Gaussian truoc khi lam net.
        /// Day la khu nhieu dua tren lam min (khong phai Non-local Means/BM3D) nhung khong can thu vien native.
        /// </summary>
        public static void Denoise(Image<Rgba32> image, float sigma = 0.6f)
        {
            image.Mutate(x => x.GaussianBlur(sigma));
        }

        /// <summary>
        /// Unsharp Masking: sharpened = original + strength * (original - GaussianBlur(original, sigma)).
        /// sigma cang lon -> lam net vung rong hon; strength cang lon -> net hon nhung de ra artifact (vien trang quanh canh).
        /// </summary>
        public static void UnsharpMask(Image<Rgba32> image, float sigma = 1.0f, float strength = 1.5f)
        {
            using var blurred = image.Clone(x => x.GaussianBlur(sigma));

            int width = image.Width;

            // PixelAccessor cua ProcessPixelRows la ref struct nen khong the dung truc tiep trong Parallel.For.
            // DangerousTryGetSinglePixelMemory tra ve Memory<T> (khong phai ref struct) khi bo dem anh lien tuc
            // trong RAM (hau het truong hop) -> cho phep xu ly song song theo dong de tan dung nhieu nhan CPU
            // (cac buoc build-in cua ImageSharp nhu GaussianBlur/Resize da tu song song hoa noi bo).
            if (image.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> originalMemory) &&
                blurred.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> blurredMemory))
            {
                Parallel.For(0, image.Height, y =>
                {
                    var originalRow = originalMemory.Span.Slice(y * width, width);
                    var blurredRow = blurredMemory.Span.Slice(y * width, width);
                    ApplyUnsharpRow(originalRow, blurredRow, strength);
                });
            }
            else
            {
                // Fallback hiem gap: anh qua lon de luu lien tuc trong 1 bo nho -> xu ly tuan tu.
                image.ProcessPixelRows(blurred, (originalAccessor, blurredAccessor) =>
                {
                    for (int y = 0; y < originalAccessor.Height; y++)
                    {
                        ApplyUnsharpRow(originalAccessor.GetRowSpan(y), blurredAccessor.GetRowSpan(y), strength);
                    }
                });
            }
        }

        private static void ApplyUnsharpRow(Span<Rgba32> originalRow, Span<Rgba32> blurredRow, float strength)
        {
            for (int x = 0; x < originalRow.Length; x++)
            {
                ref var o = ref originalRow[x];
                var b = blurredRow[x];

                o.R = ClampToByte(o.R + strength * (o.R - b.R));
                o.G = ClampToByte(o.G + strength * (o.G - b.G));
                o.B = ClampToByte(o.B + strength * (o.B - b.B));
                // Kenh Alpha giu nguyen
            }
        }

        /// <summary>Bo loc lam net Gaussian dung san co cua ImageSharp - nhanh, it tuy chinh hon UnsharpMask.</summary>
        public static void GaussianSharpen(Image<Rgba32> image, float sigma = 3f)
        {
            image.Mutate(x => x.GaussianSharpen(sigma));
        }

        private static byte ClampToByte(float value)
        {
            // +0.5f de lam tron toi gia tri gan nhat thay vi cat cut ve 0 (tranh anh bi toi/giam tuong phan nhe co he thong)
            value += 0.5f;
            if (value <= 0) return 0;
            if (value >= 255) return 255;
            return (byte)value;
        }
    }
}
