using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Accord.Video.FFMPEG;

namespace Camera_Filter
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			const string mediaInputPath = @"D:\VideoTest\baldy.jpg";
			const string mediaOutputPath = @"D:\VideoTest\baldy-pixify-20.jpg";
			var imageFilter = new Pixify(20);

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
				MediaInputPathToMemoryStreamAction(ms => imageFilter.Filter(new Bitmap(ms)).Save(mediaOutputPath));
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
					VideoService.FilterImageSequence(originalBitmaps, imageFilter).ForEach(videoFileWriter.WriteVideoFrame);
					videoFileWriter.Close();
				}
			}
		}
	}

	internal static class VideoService
	{
		public static List<Bitmap> FilterImageSequence(List<Bitmap> originalBitmaps, ImageFilter imageFilter)
		{
			return originalBitmaps.Select(imageFilter.Filter).ToList();
		}
	}

	internal abstract class ImageFilter
	{
		public abstract Bitmap Filter(Bitmap originalBitmap);
	}

	internal class BlackWhite : ImageFilter
	{
		public BlackWhite()
		{

		}

		public override Bitmap Filter(Bitmap originalBitmap)
		{
			var editedBitmap = new Bitmap(originalBitmap.Width, originalBitmap.Height, originalBitmap.PixelFormat);
			for (var y = 0; y < originalBitmap.Height; y++)
			{
				for (var x = 0; x < originalBitmap.Width; x++)
				{
					var originalPixel = originalBitmap.GetPixel(x, y);
					var brightnessColor = (byte)(originalPixel.GetBrightness() * byte.MaxValue); // shortcut for (pixel.R + pixel.G + pixel.B) / 3 + slightly more refined for human eye
					editedBitmap.SetPixel(x, y, originalPixel.ForEachChannelRgb(channel => brightnessColor));
				}
			}
			return editedBitmap;
		}
	}

	internal class Pixify : ImageFilter
	{
		public int AmountOfPixels { get; set; }

		public Pixify(int amountOfPixels)
		{
			AmountOfPixels = amountOfPixels;
		}

		public override Bitmap Filter(Bitmap originalBitmap)
		{
			var pixelGroups = new Dictionary<Matrix, List<Color>>();
			for (var y = 0; y < originalBitmap.Height; y++)
			{
				for (var x = 0; x < originalBitmap.Width; x++)
				{
					var pixelGroupsIndex = new Matrix(x / AmountOfPixels, y / AmountOfPixels);
					if (x % AmountOfPixels == 0 && y % AmountOfPixels == 0)
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
					editedBitmap.SetPixel(x, y, pixelGroupsEvaluated[new Matrix(x / AmountOfPixels, y / AmountOfPixels)]);
				}
			}
			return editedBitmap;
		}
	}

	internal class Brightener : ImageFilter
	{
		public double BrightnessStrength { get; set; }

		public Brightener(double brightnessStrength)
		{
			BrightnessStrength = brightnessStrength;
		}

		public override Bitmap Filter(Bitmap originalBitmap)
		{
			var editedBitmap = new Bitmap(originalBitmap.Width, originalBitmap.Height, originalBitmap.PixelFormat);
			var brightenerFunction = BrightnessStrength >= 0 ? (Func<byte, byte>)(channel => (byte)(channel + (byte.MaxValue - channel) * BrightnessStrength)) : (channel => (byte)(channel * (1 + BrightnessStrength)));
			for (var y = 0; y < originalBitmap.Height; y++)
			{
				for (var x = 0; x < originalBitmap.Width; x++)
				{
					editedBitmap.SetPixel(x, y, originalBitmap.GetPixel(x, y).ForEachChannelRgb(brightenerFunction));
				}
			}
			return editedBitmap;
		}
	}

	internal class Custom : ImageFilter
	{
		public Func<Bitmap, Bitmap> OwnFilter { get; set; }

		public Custom(Func<Bitmap, Bitmap> ownFilter)
		{
			OwnFilter = ownFilter;
		}

		public override Bitmap Filter(Bitmap originalBitmap)
		{
			return OwnFilter(originalBitmap);
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
			A = a;
			R = r;
			G = g;
			B = b;
		}
		public MathColor(int r, int g, int b)
		{
			A = byte.MaxValue;
			R = r;
			G = g;
			B = b;
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
			X = x;
			Y = y;
		}
	}
}
