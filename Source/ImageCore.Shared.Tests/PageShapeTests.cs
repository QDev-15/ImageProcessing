using ImageCoreService;

namespace ImageCore.Shared.Tests;

/// <summary>True page proportions under perspective, outline refinement at high resolution, curved-page straightening.</summary>
public class PageShapeTests
{
    /// <summary>A w x h rectangle in 3D, tilted by pitch / yaw / roll, at distance z in front of a camera of focal f,
    /// projected into an image of size W x H (principal point at the center).</summary>
    private static Quad Photograph(double w, double h, double pitchDeg, double yawDeg, double rollDeg, double z, double f, int W, int H)
    {
        double p = pitchDeg * Math.PI / 180, yw = yawDeg * Math.PI / 180, r = rollDeg * Math.PI / 180;
        PointD Project(double x, double y)
        {
            // rotate: roll (z), pitch (x), yaw (y)
            double x1 = x * Math.Cos(r) - y * Math.Sin(r), y1 = x * Math.Sin(r) + y * Math.Cos(r), z1 = 0;
            double y2 = y1 * Math.Cos(p) - z1 * Math.Sin(p), z2 = y1 * Math.Sin(p) + z1 * Math.Cos(p);
            double x3 = x1 * Math.Cos(yw) + z2 * Math.Sin(yw), z3 = -x1 * Math.Sin(yw) + z2 * Math.Cos(yw);
            double zz = z + z3;
            return new PointD(W / 2.0 + f * x3 / zz, H / 2.0 + f * y2 / zz);
        }
        return new Quad(Project(-w / 2, -h / 2), Project(w / 2, -h / 2), Project(w / 2, h / 2), Project(-w / 2, h / 2));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(30, 0, 0)]      // top edge far away: the naive side ratio is badly off
    [InlineData(35, 20, 10)]
    [InlineData(-25, -15, -30)]
    [InlineData(10, 40, 5)]
    public void The_true_aspect_of_a_photographed_A4_sheet_is_found_whatever_the_angle(double pitch, double yaw, double roll)
    {
        const int W = 3000, H = 4000;
        double f = 0.8 * Math.Sqrt(W * W + H * H);
        Quad q = Photograph(210, 297, pitch, yaw, roll, 420, f, W, H);
        double aspect = PageGeometry.TrueAspect(q, W, H);
        // Tilted about one axis only, two sides stay parallel and the focal length cannot be measured: the typical one
        // (0.75 x diagonal, here 0.8 in truth) leaves ~1.5% error there, still well inside the A4 tolerance.
        Assert.InRange(aspect, 210 / 297.0 * 0.975, 210 / 297.0 * 1.025);
        Assert.True(PageGeometry.IsA4(PageGeometry.OutputAspect(q, W, H, a4: true)));
    }

    [Fact]
    public void A_letter_page_or_a_receipt_keeps_its_own_shape_in_A4_mode()
    {
        const int W = 3000, H = 4000;
        double f = 0.8 * Math.Sqrt(W * W + H * H);
        Quad letter = Photograph(216, 279, 25, 10, 5, 420, f, W, H);
        Assert.InRange(PageGeometry.OutputAspect(letter, W, H, a4: true), 216 / 279.0 * 0.985, 216 / 279.0 * 1.015);
        Quad receipt = Photograph(80, 250, 20, 0, 0, 400, f, W, H);
        Assert.InRange(PageGeometry.OutputAspect(receipt, W, H, a4: true), 0.32 * 0.97, 0.32 * 1.03);
    }

    [Fact]
    public void The_refiner_pulls_a_coarse_outline_onto_the_paper_border()
    {
        var truth = new Quad(new PointD(300, 180), new PointD(1250, 230), new PointD(1180, 1420), new PointD(260, 1380));
        RgbImage scene = SceneBuilder.Render(1600, 1600, truth, new SceneBuilder.Options((60, 50, 40), (238, 236, 228), Stripes: true, Noise: 5));
        // The detector's coarse answer, ~12 px off on every side (inward on some, outward on others).
        var coarse = new Quad(new PointD(312, 170), new PointD(1238, 242), new PointD(1192, 1408), new PointD(250, 1392));
        PageOutlineRefiner.Result r = PageOutlineRefiner.Refine(scene, coarse);
        Assert.Equal(4, r.SidesRefined);
        PointD[] got = r.Outline.ToArray(), want = truth.ToArray();
        for (int i = 0; i < 4; i++)
            Assert.True(Math.Abs(got[i].X - want[i].X) < 3 && Math.Abs(got[i].Y - want[i].Y) < 3, $"corner {i}: {got[i]} vs {want[i]}");
        Assert.True(r.Bends == null || r.Bends.IsFlat); // a flat sheet stays straight
    }

    [Fact]
    public void A_bent_page_straightened_with_its_bends_has_no_background_on_its_top_border()
    {
        // Paper whose top edge sags into the page by up to 60 px (a sheet held at its corners), on red.
        const int W = 1200, H = 1600;
        var img = new RgbImage(W, H);
        var outline = new Quad(new PointD(200, 200), new PointD(1000, 200), new PointD(1000, 1400), new PointD(200, 1400));
        var bends = new PageBends(new SideBend(-0.3, 0), default, default, default); // inward: 0.25 * -0.3 * 800 = -60 px
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                bool paper = x >= 200 && x < 1000 && y < 1400;
                if (paper)
                {
                    double t = (x + 0.5 - 200) / 800.0;
                    double top = 200 + 0.3 * t * (1 - t) * 800; // outward normal of the top side points up: inward = down
                    paper = y + 0.5 >= top;
                }
                int o = (y * W + x) * 3;
                img.Data[o] = paper ? (byte)240 : (byte)200;
                img.Data[o + 1] = paper ? (byte)240 : (byte)20;
                img.Data[o + 2] = paper ? (byte)240 : (byte)20;
            }

        static int RedInTopRows(RgbImage page) =>
            Enumerable.Range(0, 3).Sum(y => Enumerable.Range(4, page.Width - 8).Count(x => page.Data[(y * page.Width + x) * 3 + 1] < 100));

        Assert.True(RedInTopRows(PerspectiveWarp.Warp(img, outline, 400, 600)) > 100);        // straight chord: background shows
        Assert.Equal(0, RedInTopRows(PerspectiveWarp.Warp(img, outline, 400, 600, bends)));   // with the bend: clean border
        Assert.Equal(PerspectiveWarp.Warp(img, outline, 400, 600).Data, PerspectiveWarp.Warp(img, outline, 400, 600, PageBends.Flat).Data);
    }

    [Fact]
    public void Bends_follow_a_quarter_turn_of_the_outline()
    {
        var b = new PageBends(new SideBend(0.1, 0), new SideBend(0.2, 0), new SideBend(0.3, 0), new SideBend(0.4, 0));
        PageBends turned = b.RotateClockwise();
        Assert.Equal(0.4, turned.Top.A);   // the old left side is the new top
        Assert.Equal(0.1, turned.Right.A);
        Assert.Equal(b, turned.RotateClockwise().RotateClockwise().RotateClockwise());
        Assert.Equal(b, PageBends.FromValues(b.ToValues()));
    }
}
