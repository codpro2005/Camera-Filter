using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Accord.Video.FFMPEG;

namespace Camera_Filter
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			const string mediaInputPath = @"D:\VideoTest\baldy.jpg";
			const string mediaOutputPath = @"D:\VideoTest\pixify-baldy.jpg";
			const ImageFilterTechnique imageFilterTechnique = ImageFilterTechnique.Pixify;
			var additionalParams = new object[] { 40 };

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
				case ImageFilterTechnique.Brightener:
					return ExecuteFilter(Brightener);
				case ImageFilterTechnique.Custom:
					return ExecuteFilter(Custom);
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
					var brightnessColor = (int)(originalBitmap.GetPixel(x, y).GetBrightness() * byte.MaxValue); // shortcut for (pixel.R + pixel.G + pixel.B) / 3 + slightly more refined for human eye
					editedBitmap.SetPixel(x, y, Color.FromArgb(brightnessColor, brightnessColor, brightnessColor));
				}
			}
			return editedBitmap;
		}

		private static Bitmap Pixify(Bitmap originalBitmap, object[] additionalParams)
		{
			var amountOfPixels = (int)additionalParams[0];
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
				var summedUpGroupColor = (MathColor)Color.Black;
				pixelGroup.Value.ForEach(pixel => summedUpGroupColor = summedUpGroupColor.ForEachChannelRgb(pixel, (summedUpGroupChannel, channel) => summedUpGroupChannel + channel));
				pixelGroupsEvaluated.Add(pixelGroup.Key, (Color)summedUpGroupColor.ForEachChannelRgb(channel => channel / pixelGroup.Value.Count));
			}
			var editedBitmap = new Bitmap(originalBitmap.Width, originalBitmap.Height, originalBitmap.PixelFormat);
			for (var y = 0; y < originalBitmap.Height; y++)
			{
				for (var x = 0; x < originalBitmap.Width; x++)
				{
					editedBitmap.SetPixel(x, y, pixelGroupsEvaluated[new Matrix(x / amountOfPixels, y / amountOfPixels)]);
				}
			}
			return editedBitmap;
		}

		private static Bitmap Brightener(Bitmap originalBitmap, object[] additionalParams)
		{
			var brightnessStrength = (float)additionalParams[0];
			var editedBitmap = new Bitmap(originalBitmap.Width, originalBitmap.Height, originalBitmap.PixelFormat);
			for (var y = 0; y < originalBitmap.Height; y++)
			{
				for (var x = 0; x < originalBitmap.Width; x++)
				{
					editedBitmap.SetPixel(x, y, originalBitmap.GetPixel(x, y).ForEachChannelRgb(channel => (byte)(brightnessStrength >= 0 ? channel + (byte.MaxValue - channel) * brightnessStrength : channel * (1 + brightnessStrength))));
				}
			}
			return editedBitmap;
		}

		private static Bitmap Custom(Bitmap orginalBitmap, IReadOnlyList<object> additionalParams)
		{
			var ownFilter = (Func<Bitmap, Bitmap>)additionalParams[0];
			return ownFilter(orginalBitmap);
		}
	}

	internal static class ColorExtensions
	{
		public static Color ForEachChannelRgb(this Color originalColor, Func<byte, byte> channelConverter)
		{
			return Color.FromArgb(channelConverter(originalColor.R), channelConverter(originalColor.G), channelConverter(originalColor.B));
		}

		public static Color ForEachChannel(this Color originalColor, Func<byte, byte> channelConverter)
		{
			return Color.FromArgb(channelConverter(originalColor.A), channelConverter(originalColor.R), channelConverter(originalColor.G), channelConverter(originalColor.B));
		}

		public static Color ForEachChannelRgb(this Color originalColor, Color referenceChannels, Func<byte, byte, byte> channelConverter)
		{
			return Color.FromArgb(channelConverter(originalColor.R, referenceChannels.R), channelConverter(originalColor.G, referenceChannels.G), channelConverter(originalColor.B, referenceChannels.B));
		}

		public static Color ForEachChannel(this Color originalColor, Color referenceChannels, Func<byte, byte, byte> channelConverter)
		{
			return Color.FromArgb(channelConverter(originalColor.A, referenceChannels.A), channelConverter(originalColor.R, referenceChannels.R), channelConverter(originalColor.G, referenceChannels.G), channelConverter(originalColor.B, referenceChannels.B));
		}

		public static Color ForEachChannelRgb(this Color originalColor, MathColor referenceChannels, Func<byte, int, int> channelConverter)
		{
			return Color.FromArgb(channelConverter(originalColor.R, referenceChannels.R), channelConverter(originalColor.G, referenceChannels.G), channelConverter(originalColor.B, referenceChannels.B));
		}

		public static Color ForEachChannel(this Color originalColor, MathColor referenceChannels, Func<byte, int, int> channelConverter)
		{
			return Color.FromArgb(channelConverter(originalColor.A, referenceChannels.A), channelConverter(originalColor.R, referenceChannels.R), channelConverter(originalColor.G, referenceChannels.G), channelConverter(originalColor.B, referenceChannels.B));
		}

		public static MathColor ForEachChannelRgb(this MathColor originalColor, Func<int, int> channelConverter)
		{
			return new MathColor(channelConverter(originalColor.R), channelConverter(originalColor.G), channelConverter(originalColor.B));
		}

		public static MathColor ForEachChannel(this MathColor originalColor, Func<int, int> channelConverter)
		{
			return new MathColor(channelConverter(originalColor.A), channelConverter(originalColor.R), channelConverter(originalColor.G), channelConverter(originalColor.B));
		}

		public static MathColor ForEachChannelRgb(this MathColor originalColor, Color referenceChannels, Func<int, byte, int> channelConverter)
		{
			return new MathColor(channelConverter(originalColor.R, referenceChannels.R), channelConverter(originalColor.G, referenceChannels.G), channelConverter(originalColor.B, referenceChannels.B));
		}

		public static MathColor ForEachChannel(this MathColor originalColor, Color referenceChannels, Func<int, byte, int> channelConverter)
		{
			return new MathColor(channelConverter(originalColor.A, referenceChannels.A), channelConverter(originalColor.R, referenceChannels.R), channelConverter(originalColor.G, referenceChannels.G), channelConverter(originalColor.B, referenceChannels.B));
		}

		public static MathColor ForEachChannelRgb(this MathColor originalColor, MathColor referenceChannels, Func<int, int, int> channelConverter)
		{
			return new MathColor(channelConverter(originalColor.R, referenceChannels.R), channelConverter(originalColor.G, referenceChannels.G), channelConverter(originalColor.B, referenceChannels.B));
		}

		public static MathColor ForEachChannel(this MathColor originalColor, MathColor referenceChannels, Func<int, int, int> channelConverter)
		{
			return new MathColor(channelConverter(originalColor.A, referenceChannels.A), channelConverter(originalColor.R, referenceChannels.R), channelConverter(originalColor.G, referenceChannels.G), channelConverter(originalColor.B, referenceChannels.B));
		}
	}

	internal struct MathColor
	{
		public int A { get; }
		public int R { get; }
		public int G { get; }
		public int B { get; }

		public MathColor(int a, int r, int g, int b)
		{
			this.A = a;
			this.R = r;
			this.G = g;
			this.B = b;
		}
		public MathColor(int r, int g, int b)
		{
			this.A = byte.MaxValue;
			this.R = r;
			this.G = g;
			this.B = b;
		}

		public static explicit operator Color(MathColor mathColor)
		{
			return Color.FromArgb(mathColor.A, mathColor.R, mathColor.G, mathColor.B);
		}

		public static explicit operator MathColor(Color color)
		{
			return new MathColor(color.A, color.R, color.G, color.B);
		}
	}

	internal struct Matrix
	{
		public int X { get; }
		public int Y { get; }

		public Matrix(int x, int y)
		{
			this.X = x;
			this.Y = y;
		}
	}

	internal enum ImageFilterTechnique
	{
		BlackWhite,
		Pixify,
		Brightener,
		Custom
	}
}
