using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace ImageOptimizerTool;

/// <summary>
/// Standalone comparison bench: current app's codecs (CCITT G4 for bitonal, JPEG2000
/// via CoreJ2K for color) vs. JBIG2 (jbig2enc) and JPEG2000 via OpenJPEG's real
/// rate-distortion encoder. NOT wired into UniversalScanClient/ScanClient-FileOptics --
/// run this, look at the PDFs it writes to your Desktop, and decide if either is worth
/// integrating for real.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
