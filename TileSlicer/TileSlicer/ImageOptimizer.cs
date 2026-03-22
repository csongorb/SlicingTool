using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace SvgTileGenerator;

internal static class ImageOptimizer
{
    public static void OptimizeAndSave(Stream inputStream, string outputPath)
    {
        if (inputStream.CanSeek)
            inputStream.Position = 0;

        using var image = Image.Load<Rgba32>(inputStream);

        var (isSimple, colorCount) = IsSimple(image);

        PngEncoder encoder;

        if (isSimple)
        {
            encoder = new PngEncoder
            {
                SkipMetadata = true,
                TransparentColorMode = PngTransparentColorMode.Preserve,
                ColorType = PngColorType.Palette,
                Quantizer = new WuQuantizer(new QuantizerOptions { MaxColors = colorCount }),
                CompressionLevel = PngCompressionLevel.BestCompression
            };
        }
        else
        {
            encoder = new PngEncoder
            {
                SkipMetadata = true,
                TransparentColorMode = PngTransparentColorMode.Preserve,
                ColorType = PngColorType.RgbWithAlpha,
                CompressionLevel = PngCompressionLevel.DefaultCompression,
                Quantizer = null
            };
        }

        var dir = Path.GetDirectoryName(outputPath);
        if (dir != null) Directory.CreateDirectory(dir);

        using var fs = new FileStream(outputPath, FileMode.Create);
        image.SaveAsPng(fs, encoder);
        fs.Dispose();

        RunOxiPng(outputPath);
    }

    private static (bool isSimple, int colorCount) IsSimple(Image<Rgba32> image)
    {
        const int MaxColors = 256;

        var uniqueColors = new HashSet<Rgba32>();

        bool exceedLimit = false;

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    byte bucketR = (byte)(row[x].R / 32 * 32);
                    byte bucketG = (byte)(row[x].G / 32 * 32);
                    byte bucketB = (byte)(row[x].B / 32 * 32);
                    byte bucketA = (byte)(row[x].A / 32 * 32);

                    var bucketPixel = new Rgba32(bucketR, bucketG, bucketB, bucketA);

                    uniqueColors.Add(bucketPixel);
                    if (uniqueColors.Count > MaxColors)
                    {
                        exceedLimit = true;
                        return;
                    }
                }
                if (exceedLimit) return;
            }
        });

        int QuantizerMaxColors = 256;

        if (uniqueColors.Count <= 8)
        {
            QuantizerMaxColors = 8;
        }
        else if (uniqueColors.Count <= 16)
        {
            QuantizerMaxColors = 16;
        }
        else if (uniqueColors.Count <= 32)
        {
            QuantizerMaxColors = 32;
        }
        else if (uniqueColors.Count <= 64)
        {
            QuantizerMaxColors = 64;
        }
        else if (uniqueColors.Count <= 128)
        {
            QuantizerMaxColors = 128;
        }
        else 
        {
            QuantizerMaxColors = 256;
        }

        return (!exceedLimit, QuantizerMaxColors);
    }


    private static void RunOxiPng(string filePath)
    {
        string appDir = AppContext.BaseDirectory;
        string oxipng = Path.Combine(appDir, "oxipng.exe");

        if (!File.Exists(oxipng))
        {
            Console.WriteLine($"Error: Optimizer tool not found at {oxipng}");
            Console.WriteLine("Please ensure oxipng is placed next to the application executable.");
            Environment.Exit(1);
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = oxipng,
                Arguments = $"-o 6 --strip all \"{filePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        string error = process.StandardError.ReadToEnd();

        process.WaitForExit();

        if (!string.IsNullOrEmpty(error))
        {
            Console.WriteLine("Error: " + error);
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            Environment.Exit(1);
        }

    }
}
