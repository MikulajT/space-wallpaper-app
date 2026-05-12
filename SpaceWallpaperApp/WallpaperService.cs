using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace SpaceWallpaperApp;

public sealed class WallpaperService
{
    public string CreateCaptionedWallpaper(string sourceImagePath, NasaImageCandidate candidate, string outputDirectory, WallpaperStyle style)
    {
        Directory.CreateDirectory(outputDirectory);

        var outputPath = Path.Combine(outputDirectory, "current-wallpaper.jpg");

        using var source = Image.FromFile(sourceImagePath);
        var primaryScreen = Screen.PrimaryScreen;
        var screenBounds = primaryScreen?.Bounds ?? new Rectangle(0, 0, 2560, 1440);
        var workingArea = primaryScreen?.WorkingArea ?? screenBounds;
        using var bitmap = new Bitmap(screenBounds.Width, screenBounds.Height);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        graphics.Clear(Color.Black);

        var imageRectangle = CalculateImageRectangle(source.Size, bitmap.Size, style);
        graphics.DrawImage(source, imageRectangle);

        using var titleFont = new Font("Segoe UI Semibold", Math.Max(10, bitmap.Width / 180f), FontStyle.Bold);
        using var descriptionFont = new Font("Segoe UI", Math.Max(7.5f, bitmap.Width / 240f), FontStyle.Regular);
        using var textBrush = new SolidBrush(Color.WhiteSmoke);
        using var shadowBrush = new SolidBrush(Color.FromArgb(180, 0, 0, 0));
        using var backgroundBrush = new SolidBrush(Color.FromArgb(150, 8, 8, 8));

        var workAreaRightInset = Math.Max(0, screenBounds.Right - workingArea.Right);
        var workAreaBottomInset = Math.Max(0, screenBounds.Bottom - workingArea.Bottom);
        var rightMargin = Math.Max(24, bitmap.Width / 70) + workAreaRightInset + 10;
        var bottomMargin = Math.Max(28, bitmap.Height / 40) + workAreaBottomInset + 10;
        var maxTextWidth = Math.Min(bitmap.Width / 3, 600);
        var description = GetWallpaperDescription(candidate.Description);

        using var titleFormat = new StringFormat { FormatFlags = StringFormatFlags.NoClip };
        using var descFormat = new StringFormat { FormatFlags = StringFormatFlags.NoClip };

        var titleSize = graphics.MeasureString(candidate.Title, titleFont, maxTextWidth, titleFormat);
        var descriptionSize = graphics.MeasureString(description, descriptionFont, maxTextWidth, descFormat);

        var boxPadding = 20;
        var boxWidth = (int)Math.Ceiling(Math.Max(titleSize.Width, descriptionSize.Width)) + boxPadding;
        var boxHeight = (int)Math.Ceiling(titleSize.Height + descriptionSize.Height) + 30;
        var boxX = Math.Max(12, workingArea.Right - boxWidth - rightMargin);
        var boxY = Math.Max(12, workingArea.Bottom - boxHeight - bottomMargin);

        using var backgroundPath = CreateRoundedRectanglePath(new Rectangle(boxX, boxY, boxWidth, boxHeight), 12);
        graphics.FillPath(backgroundBrush, backgroundPath);

        var textArea = new RectangleF(boxX + 10, boxY + 8, boxWidth - 20, boxHeight - 16);
        var titleArea = new RectangleF(textArea.X, textArea.Y, textArea.Width, titleSize.Height + 2);
        var descriptionArea = new RectangleF(textArea.X, titleArea.Bottom + 2, textArea.Width, textArea.Height - titleArea.Height - 2);

        DrawShadowedText(graphics, candidate.Title, titleFont, textBrush, shadowBrush, titleArea);
        DrawShadowedText(graphics, description, descriptionFont, textBrush, shadowBrush, descriptionArea);

        var jpegCodec = ImageCodecInfo.GetImageEncoders().FirstOrDefault(codec => codec.FormatID == ImageFormat.Jpeg.Guid);
        if (jpegCodec is null)
        {
            bitmap.Save(outputPath, ImageFormat.Jpeg);
        }
        else
        {
            using var encoderParameters = new EncoderParameters(1);
            encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 95L);
            bitmap.Save(outputPath, jpegCodec, encoderParameters);
        }

        return outputPath;
    }

    public void SetWallpaper(string filePath, WallpaperStyle style)
    {
        using var desktopKey = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", writable: true)
            ?? throw new InvalidOperationException("Could not open the Windows wallpaper settings.");

        var tile = "0";
        var wallpaperStyle = style switch
        {
            WallpaperStyle.Fill => "10",
            WallpaperStyle.Fit => "6",
            WallpaperStyle.Stretch => "2",
            WallpaperStyle.Center => "0",
            WallpaperStyle.Tile => "0",
            WallpaperStyle.Span => "22",
            _ => "10"
        };

        if (style == WallpaperStyle.Tile)
        {
            tile = "1";
        }

        desktopKey.SetValue("TileWallpaper", tile);
        desktopKey.SetValue("WallpaperStyle", wallpaperStyle);

        var applied = SystemParametersInfo(
            0x0014,
            0,
            filePath,
            0x01 | 0x02);

        if (!applied)
        {
            throw new InvalidOperationException("Windows rejected the wallpaper update request.");
        }
    }

    private static string GetWallpaperDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return "Real NASA space image.";
        }

        var normalized = description.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= 1500 ? normalized : $"{normalized[..1497]}...";
    }

    private static void DrawShadowedText(Graphics graphics, string text, Font font, Brush textBrush, Brush shadowBrush, RectangleF layoutRectangle)
    {
        using var stringFormat = new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Near,
            Trimming = StringTrimming.None,
            FormatFlags = StringFormatFlags.NoClip
        };

        var shadowRectangle = new RectangleF(layoutRectangle.X + 2, layoutRectangle.Y + 2, layoutRectangle.Width, layoutRectangle.Height);
        graphics.DrawString(text, font, shadowBrush, shadowRectangle, stringFormat);
        graphics.DrawString(text, font, textBrush, layoutRectangle, stringFormat);
    }

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();

        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        return path;
    }

    private static Rectangle CalculateImageRectangle(Size imageSize, Size canvasSize, WallpaperStyle style)
    {
        if (style == WallpaperStyle.Stretch)
        {
            return new Rectangle(0, 0, canvasSize.Width, canvasSize.Height);
        }

        if (style == WallpaperStyle.Center)
        {
            var centeredX = (canvasSize.Width - imageSize.Width) / 2;
            var centeredY = (canvasSize.Height - imageSize.Height) / 2;
            return new Rectangle(centeredX, centeredY, imageSize.Width, imageSize.Height);
        }

        var widthRatio = (double)canvasSize.Width / imageSize.Width;
        var heightRatio = (double)canvasSize.Height / imageSize.Height;

        var scale = style switch
        {
            WallpaperStyle.Fit => Math.Min(widthRatio, heightRatio),
            WallpaperStyle.Fill => Math.Max(widthRatio, heightRatio),
            WallpaperStyle.Span => Math.Max(widthRatio, heightRatio),
            _ => Math.Max(widthRatio, heightRatio)
        };

        var drawWidth = (int)Math.Ceiling(imageSize.Width * scale);
        var drawHeight = (int)Math.Ceiling(imageSize.Height * scale);
        var x = (canvasSize.Width - drawWidth) / 2;
        var y = (canvasSize.Height - drawHeight) / 2;

        return new Rectangle(x, y, drawWidth, drawHeight);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SystemParametersInfo(int uiAction, int uiParam, string pvParam, int fWinIni);
}
