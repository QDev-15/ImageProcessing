namespace ImageCoreService;

/// <summary>
/// The true proportions of a photographed rectangular sheet. Under perspective the outline's side lengths say little
/// about the sheet's shape (the far edge is shorter, and so are the sides running away from the camera), which is what
/// made straightened pages look squashed or stretched. The rectangle's aspect ratio follows from its four image corners
/// and the camera's focal length, and the focal length itself from the corners (Zhang & He, "Whiteboard scanning and
/// image enhancement", Digital Signal Processing 17, 2007): two vanishing directions of a rectangle must be orthogonal.
/// Pure geometry, no patents, no dependencies.
/// </summary>
public static class PageGeometry
{
    /// <summary>A typical phone main camera: focal length ~0.75 x the image diagonal (26 mm equivalent). Used when the
    /// outline is too close to a front view for the focal length to be measured (then it barely matters).</summary>
    public const double TypicalFocalPerDiagonal = 0.75;

    /// <summary>How close to sqrt 2 (relative) the measured shape must be to be squared up to exactly A4. Real sheets
    /// measure within a few percent; a letter page (1.29) or a receipt stays as it is instead of being stretched.</summary>
    public const double A4Tolerance = 0.08;

    /// <summary>Width / height of the real rectangle whose photo is <paramref name="q"/> (TL, TR, BR, BL, in pixels of a
    /// photo of <paramref name="imageWidth"/> x <paramref name="imageHeight"/>, principal point at its center).</summary>
    public static double TrueAspect(Quad q, int imageWidth, int imageHeight)
    {
        double cx = imageWidth / 2.0, cy = imageHeight / 2.0;
        double diag = Math.Sqrt((double)imageWidth * imageWidth + (double)imageHeight * imageHeight);
        // Homogeneous corners centered on the principal point: m1 TL, m2 TR, m3 BL, m4 BR (the paper's numbering).
        var m1 = new V3(q.TopLeft.X - cx, q.TopLeft.Y - cy, 1);
        var m2 = new V3(q.TopRight.X - cx, q.TopRight.Y - cy, 1);
        var m3 = new V3(q.BottomLeft.X - cx, q.BottomLeft.Y - cy, 1);
        var m4 = new V3(q.BottomRight.X - cx, q.BottomRight.Y - cy, 1);

        double d2 = V3.Dot(V3.Cross(m2, m4), m3), d3 = V3.Dot(V3.Cross(m3, m4), m2);
        if (Math.Abs(d2) < 1e-9 || Math.Abs(d3) < 1e-9) return SideRatio(q);
        double k2 = V3.Dot(V3.Cross(m1, m4), m3) / d2;
        double k3 = V3.Dot(V3.Cross(m1, m4), m2) / d3;
        V3 n2 = k2 * m2 - m1, n3 = k3 * m3 - m1;

        // Focal length from the orthogonality of the two vanishing directions; the typical one when the view is (nearly)
        // frontal, where it cannot be measured and hardly matters, or when the measurement is implausible.
        double f2 = Math.Pow(TypicalFocalPerDiagonal * diag, 2);
        double denominator = n2.Z * n3.Z;
        if (Math.Abs(n2.Z) > 1e-6 && Math.Abs(n3.Z) > 1e-6 && Math.Abs(denominator) > 1e-12)
        {
            double measured = -(n2.X * n3.X + n2.Y * n3.Y) / denominator;
            if (measured > Math.Pow(0.35 * diag, 2) && measured < Math.Pow(3.0 * diag, 2)) f2 = measured;
        }

        double width2 = n2.X * n2.X + n2.Y * n2.Y + f2 * n2.Z * n2.Z;
        double height2 = n3.X * n3.X + n3.Y * n3.Y + f2 * n3.Z * n3.Z;
        if (width2 <= 0 || height2 <= 0) return SideRatio(q);
        double aspect = Math.Sqrt(width2 / height2);
        return double.IsFinite(aspect) && aspect > 0.05 && aspect < 20 ? aspect : SideRatio(q);
    }

    /// <summary>The aspect (width / height) to straighten the outline into. In A4 mode a sheet whose true shape is
    /// within <see cref="A4Tolerance"/> of sqrt 2 becomes exactly A4 (portrait or landscape as measured); any other
    /// shape keeps its true proportions, so nothing is ever stretched.</summary>
    public static double OutputAspect(Quad q, int imageWidth, int imageHeight, bool a4)
    {
        double aspect = TrueAspect(q, imageWidth, imageHeight);
        if (!a4) return aspect;
        double longShort = aspect >= 1 ? aspect : 1 / aspect;
        if (Math.Abs(longShort / PerspectiveWarp.A4Ratio - 1) > A4Tolerance) return aspect;
        return aspect >= 1 ? PerspectiveWarp.A4Ratio : 1 / PerspectiveWarp.A4Ratio;
    }

    /// <summary>True when <paramref name="aspect"/> is (to rounding) an A4 sheet, either way round.</summary>
    public static bool IsA4(double aspect) =>
        Math.Abs((aspect >= 1 ? aspect : 1 / aspect) - PerspectiveWarp.A4Ratio) < 0.005;

    /// <summary>Plain ratio of the average horizontal to the average vertical side (no perspective correction).</summary>
    public static double SideRatio(Quad q)
    {
        double w = (Dist(q.TopLeft, q.TopRight) + Dist(q.BottomLeft, q.BottomRight)) / 2;
        double h = (Dist(q.TopLeft, q.BottomLeft) + Dist(q.TopRight, q.BottomRight)) / 2;
        return h < 1e-9 ? 1 : w / h;
    }

    /// <summary>
    /// Output size for straightening <paramref name="q"/> into a rectangle of the given <paramref name="aspect"/>
    /// (width / height): no less resolution than the outline's longest horizontal and vertical sides offer, then shrunk
    /// (keeping the aspect) to fit <paramref name="maxLongEdge"/> and <paramref name="maxPixels"/>.
    /// </summary>
    public static (int Width, int Height) SizeFor(Quad q, double aspect, int maxLongEdge, long maxPixels)
    {
        double hw = Math.Max(Dist(q.TopLeft, q.TopRight), Dist(q.BottomLeft, q.BottomRight));
        double hv = Math.Max(Dist(q.TopLeft, q.BottomLeft), Dist(q.TopRight, q.BottomRight));
        double h = Math.Max(hv, hw / aspect), w = h * aspect;
        double scale = Math.Min(1.0, Math.Min(maxLongEdge / Math.Max(w, h), Math.Sqrt(maxPixels / (w * h))));
        return (Math.Max(16, (int)Math.Round(w * scale)), Math.Max(16, (int)Math.Round(h * scale)));
    }

    private static double Dist(PointD a, PointD b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private readonly record struct V3(double X, double Y, double Z)
    {
        public static V3 operator *(double k, V3 v) => new(k * v.X, k * v.Y, k * v.Z);
        public static V3 operator -(V3 a, V3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static V3 Cross(V3 a, V3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public static double Dot(V3 a, V3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    }
}
