using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageProcessing
{
    /// <summary>Danh gia chat luong anh sau xu ly/nen bang PSNR va SSIM.</summary>
    public static class QualityMetrics
    {
        /// <summary>
        /// PSNR (dB) giua 2 anh cung kich thuoc. Cang cao cang giong anh goc.
        /// &gt;40dB: xuat sac | 30-40dB: tot | &lt;30dB: suy giam ro.
        /// </summary>
        public static double Psnr(Image<Rgba32> original, Image<Rgba32> compressed)
        {
            EnsureSameSize(original, compressed);

            double sumSquaredError = 0;

            original.ProcessPixelRows(compressed, (a, b) =>
            {
                for (int y = 0; y < a.Height; y++)
                {
                    var rowA = a.GetRowSpan(y);
                    var rowB = b.GetRowSpan(y);
                    for (int x = 0; x < rowA.Length; x++)
                    {
                        var pa = rowA[x];
                        var pb = rowB[x];
                        double dr = pa.R - pb.R;
                        double dg = pa.G - pb.G;
                        double db = pa.B - pb.B;
                        sumSquaredError += dr * dr + dg * dg + db * db;
                    }
                }
            });

            long channelCount = (long)original.Width * original.Height * 3;
            double mse = sumSquaredError / channelCount;
            if (mse <= 0) return double.PositiveInfinity;

            return 10.0 * Math.Log10(255.0 * 255.0 / mse);
        }

        /// <summary>
        /// SSIM [0,1] giua 2 anh cung kich thuoc, tinh theo khoi (block-based, mac dinh 8x8) tren kenh do sang (luminance).
        /// Day la ban xap xi nhe cua SSIM cua so truot chuan (vd skimage) - dung tot de so sanh tuong doi giua cac lan nen,
        /// khong dam bao trung khop tuyet doi voi cac thu vien khac.
        /// &gt;0.95: xuat sac | 0.85-0.95: tot.
        /// </summary>
        public static double Ssim(Image<Rgba32> original, Image<Rgba32> compressed, int blockSize = 8)
        {
            EnsureSameSize(original, compressed);

            int width = original.Width;
            int height = original.Height;
            var lumaA = new double[width * height];
            var lumaB = new double[width * height];

            original.ProcessPixelRows(compressed, (a, b) =>
            {
                for (int y = 0; y < height; y++)
                {
                    var rowA = a.GetRowSpan(y);
                    var rowB = b.GetRowSpan(y);
                    int rowOffset = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        var pa = rowA[x];
                        var pb = rowB[x];
                        lumaA[rowOffset + x] = 0.299 * pa.R + 0.587 * pa.G + 0.114 * pa.B;
                        lumaB[rowOffset + x] = 0.299 * pb.R + 0.587 * pb.G + 0.114 * pb.B;
                    }
                }
            });

            const double c1 = 6.5025;   // (0.01 * 255)^2
            const double c2 = 58.5225;  // (0.03 * 255)^2

            double ssimSum = 0;
            int blockCount = 0;

            for (int by = 0; by < height; by += blockSize)
            {
                int bh = Math.Min(blockSize, height - by);
                for (int bx = 0; bx < width; bx += blockSize)
                {
                    int bw = Math.Min(blockSize, width - bx);
                    int n = bw * bh;

                    double sumA = 0, sumB = 0;
                    for (int y = 0; y < bh; y++)
                    {
                        int rowOffset = (by + y) * width + bx;
                        for (int x = 0; x < bw; x++)
                        {
                            sumA += lumaA[rowOffset + x];
                            sumB += lumaB[rowOffset + x];
                        }
                    }
                    double meanA = sumA / n;
                    double meanB = sumB / n;

                    double varA = 0, varB = 0, covar = 0;
                    for (int y = 0; y < bh; y++)
                    {
                        int rowOffset = (by + y) * width + bx;
                        for (int x = 0; x < bw; x++)
                        {
                            double da = lumaA[rowOffset + x] - meanA;
                            double db = lumaB[rowOffset + x] - meanB;
                            varA += da * da;
                            varB += db * db;
                            covar += da * db;
                        }
                    }
                    varA /= n; varB /= n; covar /= n;

                    double numerator = (2 * meanA * meanB + c1) * (2 * covar + c2);
                    double denominator = (meanA * meanA + meanB * meanB + c1) * (varA + varB + c2);
                    ssimSum += numerator / denominator;
                    blockCount++;
                }
            }

            return ssimSum / blockCount;
        }

        private static void EnsureSameSize(Image<Rgba32> a, Image<Rgba32> b)
        {
            if (a.Width != b.Width || a.Height != b.Height)
                throw new ArgumentException("Hai anh phai cung kich thuoc de so sanh chat luong.");
        }
    }
}
