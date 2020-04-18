using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using Accord.Video.FFMPEG;

namespace Camera_Filter
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			const string mediaInputPath = @"D:\VideoTest\baldy.jpg";
			const string mediaOutputPath = @"D:\VideoTest\pixified-baldy-reworked.jpg";
			const ImageFilterTechnique imageFilterTechnique = ImageFilterTechnique.Pixify;
			var additionalParams = new object[] {10};

			void MediaInputPathToMemoryStreamAction(Action<MemoryStream> action)
			{
				using (var ms = new MemoryStream(File.ReadAllBytes(mediaInputPath)))
				{
					action(ms);
				}
			}
			var mediaIsImage = true;
			try
			{
				MediaInputPathToMemoryStreamAction(ms => new Bitmap(ms));
			}
			catch
			{
				mediaIsImage = false;
			}
			if (mediaIsImage)
			{
				MediaInputPathToMemoryStreamAction(ms => ImageService.Filter(imageFilterTechnique, new Bitmap(ms), additionalParams).Save(mediaOutputPath));
			}
			else
			{
				Accord.Math.Rational rational;
				var originalBitmaps = new List<Bitmap>();
				using (var videoFileReader = new VideoFileReader())
				{
					videoFileReader.Open(mediaInputPath);
					rational = videoFileReader.FrameRate;
					for (var frameIndex = 0; frameIndex < videoFileReader.FrameCount; frameIndex++)
					{
						originalBitmaps.Add(videoFileReader.ReadVideoFrame());
					}
					videoFileReader.Close();
				}
				var firstBitmap = originalBitmaps[0];
				var width = firstBitmap.Width;
				var height = firstBitmap.Height;
				using (var videoFileWriter = new VideoFileWriter())
				{
					videoFileWriter.Open(mediaOutputPath, width, height, rational, VideoCodec.H264);
					VideoService.FilterImageSequence(imageFilterTechnique, originalBitmaps, additionalParams).ForEach(videoFileWriter.WriteVideoFrame);
					videoFileWriter.Close();
				}
			}
		}
	}

	internal static class VideoService
	{
		public static List<Bitmap> FilterImageSequence(ImageFilterTechnique imageFilterTechnique, List<Bitmap> originalBitmaps, object[] additionalParams)
		{
			var editedBitmaps = new List<Bitmap>();
			originalBitmaps.ForEach(originalBitmap => editedBitmaps.Add(ImageService.Filter(imageFilterTechnique, originalBitmap, additionalParams)));
			return editedBitmaps;
		}
	}

	internal static class ImageService
	{
		public static Bitmap Filter(ImageFilterTechnique imageFilterTechnique, Bitmap originalBitmap, object[] additionalParams)
		{
			Bitmap ExecuteFilter(Func<Bitmap, object[], Bitmap> filterAction) => filterAction(originalBitmap, additionalParams);
			switch (imageFilterTechnique)
			{
				case ImageFilterTechnique.BlackWhite:
					return ExecuteFilter(BlackWhite);
				case ImageFilterTechnique.Pixify:
					return ExecuteFilter(Pixify);
				default:
					throw new ArgumentOutOfRangeException(nameof(imageFilterTechnique), imageFilterTechnique, null);
			}
		}
		private static Bitmap BlackWhite(Bitmap originalBitmap, object[] additionalParams)
		{
			var editedBitmap = new Bitmap(originalBitmap.Width, originalBitmap.Height, originalBitmap.PixelFormat);
			for (var y = 0; y < originalBitmap.Height; y++)
			{
				for (var x = 0; x < originalBitmap.Width; x++)
				{
					var brightnessColor = (int)(originalBitmap.GetPixel(x, y).GetBrightness() * 255); // shortcut for (pixel.R + pixel.G + pixel.B) / 3 + slightly more refined for human eye
					editedBitmap.SetPixel(x, y, Color.FromArgb(brightnessColor, brightnessColor, brightnessColor));
				}
			}
			return editedBitmap;
		}

		private static Bitmap Pixify(Bitmap originalBitmap, object[] additionalParams)
		{
			var amountOfPixels = (int) additionalParams[0];
			var editedBitmap = new Bitmap(originalBitmap.Width, originalBitmap.Height, originalBitmap.PixelFormat);
			var pixelGroups = new Dictionary<Matrix, List<Color>>();
			for (var y = 0; y < originalBitmap.Height; y++)
			{
				for (var x = 0; x < originalBitmap.Width; x++)
				{
					var pixelGroupsIndex = new Matrix(x / amountOfPixels, y / amountOfPixels);
					if (x % amountOfPixels == 0 && y % amountOfPixels == 0)
					{
						pixelGroups.Add(pixelGroupsIndex, new List<Color>());
					}
					pixelGroups[pixelGroupsIndex].Add(originalBitmap.GetPixel(x, y));
				}
			}
			var pixelGroupsEvaluated = new Dictionary<Matrix, Color>();
			foreach (var pixelGroup in pixelGroups)
			{
				var r = 0;
				var g = 0;
				var b = 0;
				pixelGroup.Value.ForEach(pixel =>
				{
					r += pixel.R;
					g += pixel.G;
					b += pixel.B;
				});
				var devider = pixelGroup.Value.Count;
				pixelGroupsEvaluated.Add(pixelGroup.Key, Color.FromArgb(r / devider, g / devider, b / devider));
			}
			for (var y = 0; y < originalBitmap.Height; y++)
			{
				for (var x = 0; x < originalBitmap.Width; x++)
				{
					editedBitmap.SetPixel(x, y, pixelGroupsEvaluated[new Matrix(x / amountOfPixels, y / amountOfPixels)]);
				}
			}
			return editedBitmap;
		}
	}

	internal struct Matrix
	{
		public int X { get; set; }
		public int Y { get; set; }

		public Matrix(int x, int y)
		{
			this.X = x;
			this.Y = y;
		}
	}

	internal enum ImageFilterTechnique
	{
		BlackWhite,
		Pixify
	}
}
