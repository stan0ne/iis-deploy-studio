#:package System.Drawing.Common@10.0.0
#:property OutputType=Exe
#:property TargetFramework=net10.0-windows

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
int designSize = 256;
string outputPath = args.Length > 0 ? args[0] : "src/UI/app.ico";

Color accentTop    = Color.FromArgb(255,  76, 194, 255); // #4CC2FF
Color accentBottom = Color.FromArgb(255,  33, 150, 243); // #2196F3
Color lidColor     = Color.FromArgb(255,  21, 101, 192); // #1565C0
Color arrowColor   = Color.FromArgb(255, 255, 255, 255);

// 1. Draw the 256x256 master design.
using var masterBmp = new Bitmap(designSize, designSize);
using (var g = Graphics.FromImage(masterBmp))
{
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    g.Clear(Color.Transparent);

    // Box: rounded rect (32,48) -> (224,224), radius 20.
    var boxRect = new Rectangle(32, 48, 192, 176);
    int radius = 20;
    using var boxPath = new GraphicsPath();
    boxPath.AddArc(boxRect.X, boxRect.Y, radius, radius, 180, 90);
    boxPath.AddArc(boxRect.X + boxRect.Width - radius, boxRect.Y, radius, radius, 270, 90);
    boxPath.AddArc(boxRect.X + boxRect.Width - radius, boxRect.Y + boxRect.Height - radius, radius, radius, 0, 90);
    boxPath.AddArc(boxRect.X, boxRect.Y + boxRect.Height - radius, radius, radius, 90, 90);
    boxPath.CloseFigure();
    using var boxBrush = new LinearGradientBrush(boxRect, accentTop, accentBottom, LinearGradientMode.Vertical);
    g.FillPath(boxBrush, boxPath);

    // Lid: darker strip across the top of the box (192 x 24).
    using var lidBrush = new SolidBrush(lidColor);
    g.FillRectangle(lidBrush, new Rectangle(32, 48, 192, 24));

    // Upward arrow (white), centered in the box.
    // Head (128,88)-(152,112)-(140,112); shaft (140,184)-(116,184)-(116,112)-(104,112).
    var arrowPoints = new[]
    {
        new Point(128,  88),
        new Point(152, 112),
        new Point(140, 112),
        new Point(140, 184),
        new Point(116, 184),
        new Point(116, 112),
        new Point(104, 112)
    };
    using var arrowBrush = new SolidBrush(arrowColor);
    g.FillPolygon(arrowBrush, arrowPoints);
}

// 2. Scale down to every target size with high-quality interpolation.
var scaled = new Dictionary<int, Bitmap>();
foreach (int size in sizes)
{
    if (size == designSize)
    {
        scaled[size] = masterBmp;
        continue;
    }
    var dst = new Bitmap(size, size);
    using (var sg = Graphics.FromImage(dst))
    {
        sg.SmoothingMode = SmoothingMode.AntiAlias;
        sg.InterpolationMode = InterpolationMode.HighQualityBicubic;
        sg.PixelOffsetMode = PixelOffsetMode.HighQuality;
        sg.DrawImage(masterBmp, 0, 0, size, size);
    }
    scaled[size] = dst;
}

// 3. Pack into a single multi-resolution .ico (PNG-encoded, Vista+).
using var ms = new MemoryStream();
using var bw = new BinaryWriter(ms);

// ICONDIR header
bw.Write((ushort)0);              // Reserved
bw.Write((ushort)1);              // Type (1 = icon)
bw.Write((ushort)sizes.Length);   // Image count

var pngData = new Dictionary<int, byte[]>();
int offset = 6 + (16 * sizes.Length);

foreach (int size in sizes)
{
    using var pngMs = new MemoryStream();
    scaled[size].Save(pngMs, ImageFormat.Png);
    pngData[size] = pngMs.ToArray();

    // ICONDIRENTRY: width/height byte = 0 when the slot is 256.
    byte width  = (byte)(size >= 256 ? 0 : size);
    byte height = (byte)(size >= 256 ? 0 : size);

    bw.Write(width);
    bw.Write(height);
    bw.Write((byte)0);            // Color count (0 for >=8bpp)
    bw.Write((byte)0);            // Reserved
    bw.Write((ushort)1);          // Planes
    bw.Write((ushort)32);         // Bit count (32-bit RGBA)
    bw.Write((uint)pngData[size].Length);
    bw.Write((uint)offset);

    offset += pngData[size].Length;
}

foreach (int size in sizes)
{
    bw.Write(pngData[size]);
}

bw.Flush();
File.WriteAllBytes(outputPath, ms.ToArray());

var absPath = Path.GetFullPath(outputPath);
var bytes   = new FileInfo(outputPath).Length;
Console.WriteLine($"Icon generated: {absPath}");
Console.WriteLine($"  File size : {bytes:N0} bytes");
Console.WriteLine($"  Sizes     : {string.Join(", ", sizes)}");
