using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinMixer.Controls;

/// <summary>Procedural textures (brushed metal, grain) so the app needs no image assets.</summary>
public static class Textures
{
    static readonly Dictionary<string, ImageBrush> Cache = new();

    public static ImageBrush BrushedMetal(Color baseColor, int seed = 1, double contrast = 0.10, double grain = 0.03, int streak = 14)
    {
        string key = $"{baseColor}{seed}{contrast}{grain}{streak}";
        if (Cache.TryGetValue(key, out var cached)) return cached;

        const int W = 256, H = 256;
        var rnd = new Random(seed);
        var n = new float[W * H];
        for (int i = 0; i < n.Length; i++) n[i] = (float)(rnd.NextDouble() * 2 - 1);

        // horizontal streak blur (wrapping => seamless tiling)
        var b = new float[W * H];
        for (int y = 0; y < H; y++)
        {
            float acc = 0;
            for (int k = -streak; k <= streak; k++) acc += n[y * W + ((k % W) + W) % W];
            for (int x = 0; x < W; x++)
            {
                b[y * W + x] = acc / (float)Math.Sqrt(2 * streak + 1) * 0.55f;
                acc += n[y * W + (x + streak + 1) % W] - n[y * W + ((x - streak) % W + W) % W];
            }
        }
        var rowBias = new float[H];
        for (int y = 0; y < H; y++) rowBias[y] = (float)(rnd.NextDouble() * 2 - 1) * 0.35f;

        var px = new byte[W * H * 4];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                double v = b[i] + rowBias[y];
                double g = (rnd.NextDouble() * 2 - 1) * grain;
                double f = 1 + contrast * v + g;
                px[i * 4 + 0] = Clamp(baseColor.B * f);
                px[i * 4 + 1] = Clamp(baseColor.G * f);
                px[i * 4 + 2] = Clamp(baseColor.R * f);
                px[i * 4 + 3] = 255;
            }

        var bmp = BitmapSource.Create(W, H, 96, 96, PixelFormats.Bgra32, null, px, W * 4);
        bmp.Freeze();
        var brush = new ImageBrush(bmp)
        {
            TileMode = TileMode.Tile,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, W, H),
            Stretch = Stretch.None
        };
        brush.Freeze();
        Cache[key] = brush;
        return brush;
    }

    static byte Clamp(double v) => (byte)Math.Clamp(v, 0, 255);

    public static Brush Steel => BrushedMetal(Color.FromRgb(0x2C, 0x2F, 0x34), 7, 0.13);
    public static Brush Plate => BrushedMetal(Color.FromRgb(0x44, 0x48, 0x4E), 3, 0.11);
    public static Brush Dark => BrushedMetal(Color.FromRgb(0x1B, 0x1D, 0x21), 11, 0.08, 0.04);
}

/// <summary>Markup access: Background="{x:Static ...}" via static properties on a helper.</summary>
public static class Tex
{
    public static Brush Steel => Textures.Steel;
    public static Brush Plate => Textures.Plate;
    public static Brush Dark => Textures.Dark;
}
