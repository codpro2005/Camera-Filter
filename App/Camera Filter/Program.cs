using System;
using System.Collections.Generic;
using System.Drawing;
using Accord.Video.FFMPEG;

namespace Camera_Filter
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			using (var videoFileReader = new VideoFileReader())
			{
				videoFileReader.Open(@"D:\VideoTest\test_Trim.mp4");
				var rational = videoFileReader.FrameRate;
				var width = 0;
				var height = 0;
				var originalBitmaps = new List<Bitmap>();
				for (var frameIndex = 0; frameIndex < videoFileReader.FrameCount; frameIndex++)
				{
					originalBitmaps.Add(videoFileReader.ReadVideoFrame());
				}
				videoFileReader.Close();
				var firstBitmap = originalBitmaps[0];
				if (firstBitmap != null)
				{
					width = firstBitmap.Width;
					height = firstBitmap.Height;
				}
				using (var videoFileWriter = new VideoFileWriter())
				{
					videoFileWriter.Open(@"D:\VideoTest\test_Trim_output.mp4", width, height, rational, VideoCodec.H264);
					VideoFilter.FilterImageSequence(originalBitmaps, ImageFilter.FilterBlackWhite).ForEach(editedFrame => videoFileWriter.WriteVideoFrame(editedFrame));
				}
			}
		}
	}

	internal static class VideoFilter
	{
		public static List<Bitmap> FilterImageSequence(List<Bitmap> originalBitmaps, Func<Bitmap, object[], Bitmap> bitmapFilterAction, params object[] additionalParams)
		{
			var editedBitmaps = new List<Bitmap>();
			originalBitmaps.ForEach(originalBitmap =>
			{
				editedBitmaps.Add(bitmapFilterAction(originalBitmap, additionalParams));
			});
			return editedBitmaps;
		}
	}

	internal static class ImageFilter
	{
		public static Bitmap FilterBlackWhite(Bitmap originalBitmap, params object[] additionalParams)
		{
			var editedFrame = new Bitmap(originalBitmap.Width, originalBitmap.Height, originalBitmap.PixelFormat);
			for (var y = 0; y < originalBitmap.Height; y++)
			{
				for (var x = 0; x < originalBitmap.Width; x++)
				{
					var brightnessColor = (int)(originalBitmap.GetPixel(x, y).GetBrightness() * 255); // shortcut for (pixel.R + pixel.B + pixel.G) / 3 + slightly more refined for human eye
					editedFrame.SetPixel(x, y, Color.FromArgb(brightnessColor, brightnessColor, brightnessColor));
				}
			}

			return editedFrame;
		}
	}
}
