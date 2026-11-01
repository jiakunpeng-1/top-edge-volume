using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: IconGenerator <output.ico>");
    return 1;
}

var outputPath = Path.GetFullPath(args[0]);
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

using var bitmap = new Bitmap(256, 256, PixelFormat.Format32bppArgb);
using (var graphics = Graphics.FromImage(bitmap))
{
    graphics.SmoothingMode = SmoothingMode.AntiAlias;
    graphics.Clear(Color.Transparent);

    using var background = new SolidBrush(Color.FromArgb(30, 58, 138));
    graphics.FillEllipse(background, 6, 6, 244, 244);

    using var topEdgePen = new Pen(Color.FromArgb(103, 232, 249), 13)
    {
        StartCap = LineCap.Round,
        EndCap = LineCap.Round
    };
    graphics.DrawLine(topEdgePen, 30, 33, 226, 33);

    using var speaker = new SolidBrush(Color.FromArgb(248, 250, 252));
    graphics.FillPolygon(speaker, new[]
    {
        new Point(35, 105), new Point(60, 105), new Point(96, 78),
        new Point(96, 178), new Point(60, 151), new Point(35, 151)
    });

    using var soundPen = new Pen(Color.FromArgb(165, 243, 252), 12)
    {
        StartCap = LineCap.Round,
        EndCap = LineCap.Round
    };
    graphics.DrawBezier(soundPen, new Point(118, 105), new Point(147, 118), new Point(147, 138), new Point(118, 151));
    graphics.DrawBezier(soundPen, new Point(134, 84), new Point(186, 112), new Point(186, 144), new Point(134, 172));
}

bitmap.Save(Path.ChangeExtension(outputPath, ".png"), ImageFormat.Png);

var iconHandle = bitmap.GetHicon();
try
{
    using var icon = Icon.FromHandle(iconHandle);
    using var stream = File.Create(outputPath);
    icon.Save(stream);
}
finally
{
    DestroyIcon(iconHandle);
}

return 0;

[DllImport("user32.dll", SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
static extern bool DestroyIcon(nint iconHandle);
