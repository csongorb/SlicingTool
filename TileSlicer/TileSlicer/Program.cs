using SkiaSharp;
using Svg.Skia;

namespace SvgTileGenerator {
    internal class ImageZoomInfo {
        public string ImagePath { get; init; } = "";
        public int MinZoom { get; set; }
        public int MaxZoom { get; set; }
    }

    internal static class Program {
        private static long _processedTileCount;
        private static int _currentZoom;
        private static long _totalTileCount;
        private static DateTime _startTime;

        private static void Main() {
            Console.Clear();
            PrintHeader();

            var numImages = ReadInt("Enter the number of images to process: ");
            List<ImageZoomInfo> images = [];
            for (var i = 0; i < numImages; i++) {
                var imagePath = ReadString($"Enter path for image #{i + 1}: ");
                if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath)) {
                    PrintError("Invalid image path. Exiting.");
                    return;
                }
                images.Add(new ImageZoomInfo { ImagePath = imagePath });
            }

            var outputDir = ReadString("Enter the output directory: ");
            if (string.IsNullOrWhiteSpace(outputDir)) {
                outputDir = "tiles";
            }
            Directory.CreateDirectory(outputDir);

            var tileSize = 512;
            var useDefault = ReadString("Use default tile size of 512? (Y/n): ");
            if (useDefault.Trim().Equals("n", StringComparison.CurrentCultureIgnoreCase)) {
                tileSize = ReadInt("Enter desired tile size (in pixels): ");
            }

            var lastMaxZoom = -1;
            foreach (var img in images) {
                var fileName = Path.GetFileName(img.ImagePath);
                var valid = false;
                while (!valid) {
                    var input = ReadString($"Enter minimum zoom level for image '{fileName}': ");
                    int minZoom, maxZoom;
                    if (input.Contains('-')) {
                        var parts = input.Split('-');
                        if (parts.Length != 2 ||
                            !int.TryParse(parts[0].Trim(), out minZoom) ||
                            !int.TryParse(parts[1].Trim(), out maxZoom)) {
                            PrintWarning("Invalid format. Please re-enter.");
                            continue;
                        }
                        Console.WriteLine($"Maximum zoom level for image '{fileName}': {maxZoom}");
                    }
                    else {
                        if (!int.TryParse(input, out minZoom)) {
                            PrintWarning("Invalid input. Please enter a valid number.");
                            continue;
                        }
                        maxZoom = ReadInt($"Enter maximum zoom level for image '{fileName}': ");
                    }
                    if (minZoom > maxZoom) {
                        PrintWarning("Minimum zoom level cannot be greater than maximum zoom level. Please re-enter.");
                        continue;
                    }
                    if (lastMaxZoom != -1 && minZoom <= lastMaxZoom) {
                        PrintWarning($"Zoom levels must not overlap. Previous maximum zoom was {lastMaxZoom}. Please enter a minimum zoom greater than {lastMaxZoom}.");
                        continue;
                    }
                    img.MinZoom = minZoom;
                    img.MaxZoom = maxZoom;
                    lastMaxZoom = maxZoom;
                    valid = true;
                }
            }

            foreach (var img in images) {
                var svg = new SKSvg();
                svg.Load(img.ImagePath);
                var picture = svg.Picture;
                if (picture == null) {
                    PrintWarning($"Failed to load SVG: {img.ImagePath}. Skipping.");
                    continue;
                }
                var cullRect = picture.CullRect;
                var svgWidth = cullRect.Width;
                var svgHeight = cullRect.Height;
                for (var zoom = img.MinZoom; zoom <= img.MaxZoom; zoom++) {
                    var scale = Math.Pow(2, zoom);
                    var sourceTileSize = tileSize / scale;
                    var cols = Math.Ceiling(svgWidth / sourceTileSize);
                    var rows = Math.Ceiling(svgHeight / sourceTileSize);
                    _totalTileCount += (long)cols * (long)rows;
                }
            }
            Console.WriteLine($"Total tiles to generate: {_totalTileCount}");

            _startTime = DateTime.Now;
            var numberWidth = _totalTileCount.ToString().Length;
            using var progressTimer = new Timer(_ => UpdateProgress(numberWidth), null, 1000, 1000);

            foreach (var img in images) {
                var svg = new SKSvg();
                svg.Load(img.ImagePath);
                var picture = svg.Picture;
                if (picture == null) {
                    PrintWarning($"Failed to load SVG: {img.ImagePath}. Skipping.");
                    continue;
                }
                var cullRect = picture.CullRect;
                var svgWidth = cullRect.Width;
                var svgHeight = cullRect.Height;
                for (var zoom = img.MinZoom; zoom <= img.MaxZoom; zoom++) {
                    _currentZoom = zoom;
                    var scale = Math.Pow(2, zoom);
                    var sourceTileSize = tileSize / scale;
                    var cols = (int)Math.Ceiling(svgWidth / sourceTileSize);
                    var rows = (int)Math.Ceiling(svgHeight / sourceTileSize);
                    var zoomDir = Path.Combine(outputDir, zoom.ToString());
                    Directory.CreateDirectory(zoomDir);

                    for (var y = 0; y < rows; y++) {
                        for (var x = 0; x < cols; x++) {
                            var srcX = x * sourceTileSize;
                            var srcY = y * sourceTileSize;
                            using var surface = SKSurface.Create(new SKImageInfo(tileSize, tileSize, SKColorType.Rgba8888, SKAlphaType.Premul));
                            var canvas = surface.Canvas;
                            canvas.Clear(SKColors.Transparent);
                            var scaleX = tileSize / sourceTileSize;
                            var scaleY = tileSize / sourceTileSize;
                            canvas.Save();
                            canvas.Translate((float)(-srcX * scaleX), (float)(-srcY * scaleY));
                            canvas.Scale((float)scaleX, (float)scaleY);
                            canvas.DrawPicture(picture);
                            canvas.Restore();

                            var xDir = Path.Combine(zoomDir, x.ToString());
                            Directory.CreateDirectory(xDir);
                            var tileFile = Path.Combine(xDir, $"{y}.png");
                            using var imageSnapshot = surface.Snapshot();
                            using var data = imageSnapshot.Encode(SKEncodedImageFormat.Png, 100);
                            using var stream = File.OpenWrite(tileFile);
                            data.SaveTo(stream);

                            Interlocked.Increment(ref _processedTileCount);
                        }
                    }
                }
            }
            
            var totalElapsed = DateTime.Now - _startTime;
            Console.WriteLine("\r" + $@"Tile generation complete. Total time: {totalElapsed:hh\:mm\:ss}".PadRight(90));
            Console.ResetColor();
            Console.ReadLine();
        }

        private static void UpdateProgress(int numberWidth) {
            var processed = Interlocked.Read(ref _processedTileCount);
            var elapsed = DateTime.Now - _startTime;
            var tilesPerSecond = processed / elapsed.TotalSeconds;
            var secondsRemaining = (tilesPerSecond > 0) ? (_totalTileCount - processed) / tilesPerSecond : 0;
            var remaining = TimeSpan.FromSeconds(secondsRemaining);

            var zoomSection = $"Zoom: {_currentZoom,2}";
            var processedSection = $"Processed: {processed.ToString().PadLeft(numberWidth)}/{_totalTileCount.ToString().PadLeft(numberWidth)}";
            var elapsedSection = $@"Elapsed: {elapsed:hh\:mm\:ss}";
            var remainingSection = $@"Remaining: {remaining:hh\:mm\:ss}";
            var update = $"[{zoomSection}] | [{processedSection}] | [{elapsedSection}] | [{remainingSection}]";

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("\r" + update.PadRight(90));
            Console.ResetColor();
        }

        private static string ReadString(string prompt) {
            Console.Write(prompt);
            var input = Console.ReadLine();
            return input?.Replace("\"", "").Trim() ?? "";
        }

        private static int ReadInt(string prompt) {
            while (true) {
                var input = ReadString(prompt);
                if (int.TryParse(input, out var value))
                    return value;
                PrintWarning("Invalid number. Please try again.");
            }
        }

        private static void PrintHeader() {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("========================================");
            Console.WriteLine("          SVG Tile Generator            ");
            Console.WriteLine("========================================\n");
            Console.ResetColor();
        }

        private static void PrintWarning(string message) {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        private static void PrintError(string message) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}
