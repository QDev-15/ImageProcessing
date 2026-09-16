using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ImageProcessing
{
    /// <summary>
    /// Doc/ghi anh o dinh dang pho bien (JPEG, PNG, BMP, GIF, TIFF, WebP...).
    /// Khong xu ly file RAW (CR2/NEF/ARW...) - can thu vien native rieng (LibRaw) cho dinh dang do.
    /// </summary>
    public static class ImageIO
    {
        /// <summary>Doc anh tu file, tu dong xoay theo EXIF orientation.</summary>
        public static Image<Rgba32> Load(string path)
        {
            var image = Image.Load<Rgba32>(path);
            image.Mutate(x => x.AutoOrient());
            return image;
        }

        /// <summary>Doc anh tu byte[] (vd: upload tu web request), tu dong xoay theo EXIF.</summary>
        public static Image<Rgba32> Load(byte[] data)
        {
            var image = Image.Load<Rgba32>(data);
            image.Mutate(x => x.AutoOrient());
            return image;
        }

        /// <summary>Doc anh tu Stream (vd: IFormFile.OpenReadStream() trong ASP.NET Core), tu dong xoay theo EXIF.</summary>
        public static Image<Rgba32> Load(Stream stream)
        {
            var image = Image.Load<Rgba32>(stream);
            image.Mutate(x => x.AutoOrient());
            return image;
        }

        /// <summary>Phat hien dinh dang anh tu byte[] ma khong decode toan bo pixel (nhanh).</summary>
        public static IImageFormat? DetectFormat(byte[] data) => Image.DetectFormat(data);

        /// <summary>Doc kich thuoc anh (width/height) ma khong decode toan bo pixel - dung khi chi can metadata.</summary>
        public static Size ReadSize(string path)
        {
            var info = Image.Identify(path);
            return new Size(info.Width, info.Height);
        }

        /// <summary>
        /// Resize anh giu ty le, khong vuot qua maxWidth/maxHeight. Dung Lanczos3 - chat luong cao nhat cho downscale.
        /// Neu anh da nho hon gioi han thi giu nguyen (khong phong to).
        /// </summary>
        public static void SmartResize(Image<Rgba32> image, int maxWidth, int maxHeight)
        {
            double ratio = Math.Min((double)maxWidth / image.Width, (double)maxHeight / image.Height);
            if (ratio >= 1.0) return;

            var newSize = new Size((int)Math.Round(image.Width * ratio), (int)Math.Round(image.Height * ratio));
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = newSize,
                Sampler = KnownResamplers.Lanczos3,
                Mode = ResizeMode.Max
            }));
        }
    }
}
