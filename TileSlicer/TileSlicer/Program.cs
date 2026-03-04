using SkiaSharp;
using Svg.Skia;
using System.Text.RegularExpressions;


namespace SvgTileGenerator {
	internal class ImageZoomInfo {
		public string ImagePath { get; init; } = "";
		public int MinZoom { get; set; }
		public int MaxZoom { get; set; }
	}

	internal static class Program {
		private static long _processedTileCount;

		private static bool TryGetTileGrid(float svgWidth, float svgHeight, int tileSize, int zoom,
			out int cols, out int rows, out float scale, out float sourceTileSize) {
			scale = (float)Math.Pow(2, zoom);
			sourceTileSize = tileSize / scale;
			if (sourceTileSize <= 0 || svgWidth <= 0 || svgHeight <= 0) {
				cols = 0;
				rows = 0;
				return false;
			}
			cols = (int)Math.Ceiling(svgWidth / sourceTileSize);
			rows = (int)Math.Ceiling(svgHeight / sourceTileSize);
			return cols >= 1 && rows >= 1;
		}

		private static void Main() {
			try {
				try {
					Console.Clear();
				} catch {
					// Console.Clear() may fail in non-interactive environments
				}
				PrintHeader();

				var numImages = GetNumberOfImages();
				List<ImageZoomInfo> images = GetImagePaths(numImages);
				var outputDir = GetOutputDirectory();
				Console.WriteLine();
				var tileSize = GetTileSize();
				Console.WriteLine();
				GetZoomLevels(images);
				Console.WriteLine();
				var totalTileCount = ComputeTotalTileCount(images, tileSize);
				Console.WriteLine($"Total tiles to generate: {totalTileCount}");
				Console.WriteLine();
				Console.WriteLine("-----------------------------------------");
				Console.WriteLine();
				var startTime = DateTime.Now;
				_processedTileCount = 0;

				var progressTimer = StartProgressTimer(totalTileCount, startTime);

				ProcessImages(images, outputDir, tileSize);

				progressTimer.Dispose();
				var totalElapsed = DateTime.Now - startTime;
				Console.WriteLine();
				Console.WriteLine($@"Tile generation complete. Total time: {totalElapsed:hh\:mm\:ss}.");
				Console.ReadLine();
			} catch (Exception ex) {
				Console.Error.WriteLine($"An unexpected error occurred: {ex}");
			}
		}

		private static void PrintHeader() {
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine("========================================");
			Console.WriteLine("          SVG Tile Generator            ");
			Console.WriteLine("========================================\n");
			Console.ResetColor();
		}

		private static int GetNumberOfImages() {
			Console.Write("Enter the number of images to process: ");
			return int.Parse(Console.ReadLine() ?? "0");
		}

		private static List<ImageZoomInfo> GetImagePaths(int numImages) {
			List<ImageZoomInfo> images = [];
			for(var i = 0;i < numImages;i++) {
				Console.Write($"Enter path for image #{i + 1}: ");
				var imagePath = Console.ReadLine()?.Replace('"', ' ').Trim() ?? "";
				if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath)) {
					Console.WriteLine("Invalid image path. Exiting.");
					Environment.Exit(1);
				}
				images.Add(new ImageZoomInfo { ImagePath = imagePath });
			}
			return images;
		}

		private static string GetOutputDirectory() {
			Console.Write("Enter the output directory: ");
			var outputDir = Console.ReadLine()?.Replace('"', ' ').Trim() ?? "tiles";
			Directory.CreateDirectory(outputDir);
			return outputDir;
		}

		private static int GetTileSize() {
			var tileSize = 512;
			Console.Write("Use default tile size of 512? (Y/n): ");
			var useDefault = Console.ReadLine()?.Trim().ToLower();
			if (useDefault != "n") return tileSize;
			Console.Write("Enter desired tile size (in pixels): ");
			tileSize = int.Parse(Console.ReadLine() ?? "512");
			return tileSize;
		}

		private static void GetZoomLevels(List<ImageZoomInfo> images) {
			var rangeRegex = new Regex("^\\s*(-?\\d+)\\s*-\\s*(-?\\d+)\\s*$", RegexOptions.Compiled);
			var lastMaxZoom = -1;
			foreach(var img in images) {
				var fileName = Path.GetFileName(img.ImagePath);
				while(true) {
					Console.Write($"Enter minimum zoom level for image '{fileName}': ");
					var input = Console.ReadLine()?.Trim() ?? "";
					int minZoom, maxZoom;
					var rangeMatch = rangeRegex.Match(input);
					if (rangeMatch.Success) {
						minZoom = int.Parse(rangeMatch.Groups[1].Value);
						maxZoom = int.Parse(rangeMatch.Groups[2].Value);
						Console.WriteLine($"Maximum zoom level for image '{fileName}': {maxZoom}");
					} else {
						if (!int.TryParse(input, out minZoom)) {
							Console.WriteLine("Invalid input. Please enter a valid number or range like -4--1.");
							continue;
						}
						Console.Write($"Enter maximum zoom level for image '{fileName}': ");
						var maxInput = Console.ReadLine()?.Trim() ?? "";
						if (!int.TryParse(maxInput, out maxZoom)) {
							Console.WriteLine("Invalid maximum zoom level. Please try again.");
							continue;
						}
					}
					if (minZoom > maxZoom) {
						Console.WriteLine("Minimum zoom level cannot be greater than maximum zoom level. Please re-enter.");
						continue;
					}
					if (lastMaxZoom != -1 && minZoom <= lastMaxZoom) {
						Console.WriteLine(
							$"Zoom levels must not overlap. Previous maximum zoom was {lastMaxZoom}. Please enter a minimum zoom greater than {lastMaxZoom}.");
						continue;
					}
					img.MinZoom = minZoom;
					img.MaxZoom = maxZoom;
					lastMaxZoom = maxZoom;
					break;
				}
			}
		}

		private static long ComputeTotalTileCount(List<ImageZoomInfo> images, int tileSize) {
			long totalTileCount = 0;
			foreach(var img in images) {
				var svg = new SKSvg();
				svg.Load(img.ImagePath);
				var picture = svg.Picture;
				if (picture == null) {
					Console.WriteLine($"Failed to load SVG: {img.ImagePath}. Skipping.");
					continue;
				}
				var cullRect = picture.CullRect;
				var svgWidth = cullRect.Width;
				var svgHeight = cullRect.Height;
				for(var zoom = img.MinZoom;zoom <= img.MaxZoom;zoom++) {
					if (!TryGetTileGrid(svgWidth, svgHeight, tileSize, zoom, out var cols, out var rows, out _, out _)) {
						Console.WriteLine(
							$"Zoom level {zoom} has less than 1 tile; skipping until a zoom with >= 1 tile is reached.");
						continue;
					}
					totalTileCount += (long)cols * rows;
				}
			}
			return totalTileCount;
		}

		private static Timer StartProgressTimer(long totalTileCount, DateTime startTime) {
			var progressTimer = new Timer(_ => {
				var processed = Interlocked.Read(ref _processedTileCount);
				var elapsed = DateTime.Now - startTime;
				var progressFraction = (double)processed / totalTileCount;
				var percent = progressFraction * 100;
				var tilesPerSecond = processed / elapsed.TotalSeconds;
				var secondsRemaining = (tilesPerSecond > 0) ? (totalTileCount - processed) / tilesPerSecond : 0;
				var remaining = TimeSpan.FromSeconds(secondsRemaining);
				Console.Write("\rProcessed {0}/{1} tiles ({2:F1}%). Elapsed: {3:hh\\:mm\\:ss}. Estimated remaining: {4:hh\\:mm\\:ss}.",
					processed, totalTileCount, percent, elapsed, remaining);
			}, null, 1000, 1000);
			return progressTimer;
		}

		private static void ProcessImages(List<ImageZoomInfo> images, string outputDir, int tileSize) {
			foreach(var img in images) {
				var svg = new SKSvg();
				svg.Load(img.ImagePath);
				var picture = svg.Picture;
				if (picture == null) {
					Console.WriteLine($"Failed to load SVG: {img.ImagePath}. Skipping.");
					continue;
				}
				var cullRect = picture.CullRect;
				var svgWidth = cullRect.Width;
				var svgHeight = cullRect.Height;
				for(var zoom = img.MinZoom;zoom <= img.MaxZoom;zoom++) {
					if (!TryGetTileGrid(svgWidth, svgHeight, tileSize, zoom, out var cols, out var rows,
						out var scale, out var sourceTileSize)) {
						Console.WriteLine(
							$"Zoom level {zoom} has less than 1 tile; skipping until a zoom with >= 1 tile is reached.");
						continue;
					}
					Console.WriteLine();
					Console.ForegroundColor = ConsoleColor.Yellow;
					Console.WriteLine($"Zoom level {zoom}: calculated {cols} columns x {rows} rows (scale: {scale}).");
					Console.WriteLine();
					Console.ResetColor();

					var zoomDir = Path.Combine(outputDir, zoom.ToString());
					Directory.CreateDirectory(zoomDir);

					List<(int x, int y)> tiles = [];
					for(var y = 0;y < rows;y++) {
						for(var x = 0;x < cols;x++) {
							tiles.Add((x, y));
						}
					}

					Parallel.ForEach(tiles,
						new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
						tile => {
							var x = tile.x;
							var y = tile.y;
							var srcX = x * sourceTileSize;
							var srcY = y * sourceTileSize;
							var xDir = Path.Combine(zoomDir, x.ToString());
							using (var surface =
							       SKSurface.Create(new SKImageInfo(tileSize, tileSize, SKColorType.Rgba8888, SKAlphaType.Premul))) {
								var canvas = surface.Canvas;
								canvas.Clear(SKColors.Transparent);
								var scaleX = tileSize / sourceTileSize;
								var scaleY = tileSize / sourceTileSize;
								canvas.Save();
								canvas.Translate(-srcX * scaleX, -srcY * scaleY);
								canvas.Scale(scaleX, scaleY);
								canvas.DrawPicture(picture);
								canvas.Restore();
								Directory.CreateDirectory(xDir);
								var tileFile = Path.Combine(xDir, $"{y}.png");
								using (var image = surface.Snapshot())
								using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
								using (var stream = File.OpenWrite(tileFile)) {
									data.SaveTo(stream);
								}
							}
							Interlocked.Increment(ref _processedTileCount);
						});
				}
			}
		}
	}
}