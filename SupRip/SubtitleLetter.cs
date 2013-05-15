using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SupRip
{
	class SubtitleLetter
	{
		private Rectangle coords;
		private double angle;
		private int height;
		private string text;
		private double[] borders;
		private byte[,] image;

		public int ImageWidth
		{
			get { return image.GetLength(1); }
		}

		public int ImageHeight
		{
			get { return image.GetLength(0); }
		}

		public Rectangle Coords
		{
			get { return coords; }
			set { coords = value; }
		}

		public double Angle
		{
			get { return angle; }
			set { angle = value; }
		}

		public string Text
		{
			get { return text; }
			set { text = value; }
		}

		/// <summary>
		/// Sets the height of the letter above the baseline.
		/// Used for distinguishing apostrophes from commas.
		/// </summary>
		public int Height
		{
			get { return height; }
			set { height = value; }
		}

		public SubtitleLetter(Rectangle r)
		{
			coords = r;
			text = null;
		}

		public SubtitleLetter(Rectangle r, string s)
			: this(r)
		{
			text = s;
		}

		public SubtitleLetter(Rectangle r, double a, string s)
			: this(r, s)
		{
			angle = a;
		}

		public SubtitleLetter(byte[,] i)
		{
			image = i;
			ReduceImage();
		}

		public SubtitleLetter(byte[,] i, string s)
			: this(i)
		{
			text = s;
		}

		public SubtitleLetter(byte[,] i, double a)
			: this(i)
		{
			angle = a;
		}

		public SubtitleLetter(byte[,] i, Rectangle r, double a)
			: this(i, a)
		{
			coords = r;
		}

		private void ReduceImage()
		{
			borders = new double[4];
			for (int i = 0; i < 4; i++)
				borders[i] = AveragedBorderWidth(image, i);
		}

		private string DrawDiff(int[,] da)
		{
			int h = da.GetLength(0);
			int w = da.GetLength(1);

			StringBuilder sb = new StringBuilder(1000);
			for (int j = 0; j < h; j++)
			{
				for (int i = 0; i < w; i++)
				{
					if (da[j, i] > 100)
						sb.Append('#');
					else if (da[j, i] > 50)
						sb.Append('+');
					else if (da[j, i] > 20)
						sb.Append(':');
					else if (da[j, i] > 10)
						sb.Append('.');
					else
						sb.Append(' ');
				}
				sb.Append("\n");
			}
			return sb.ToString();
		}

		double BorderWidth(byte[,] array, int side)
		{
			return BorderWidth(array, side, -1);
		}

		/// <summary>
		/// Takes the border width as defined by below function, and averages it over three adjacent pixels in order to remove randomness that can happen if a vertical
		/// drop is right at the point where we do a measurement
		/// </summary>
		/// <param name="array"></param>
		/// <param name="side"></param>
		/// <returns></returns>
		double AveragedBorderWidth(byte[,] array, int side)
		{
			int h = array.GetLength(0), w = array.GetLength(1);
			int midHeight = h / 2, midWidth = w / 2;

			int position;
			if (side == 0 || side == 2)
				position = midWidth;
			else
				position = midHeight;

			double x1 = BorderWidth(array, side, position - 1);
			double x2 = BorderWidth(array, side, position);
			double x3 = BorderWidth(array, side, position + 1);
			double avgx = (x1 + x2 + x3) / 3;
			if (Math.Abs(x1 - avgx) > 3 || Math.Abs(x2 - avgx) > 3 || Math.Abs(x3 - avgx) > 3)
				return -1;
			else
				return avgx;
		}


		const int SIDE_TOP = 0, SIDE_RIGHT = 1, SIDE_BOTTOM = 2, SIDE_LEFT = 3;
		/// <summary>
		/// In order to allow quicker first guesses whether two letters are identical, this function measures the general shape of a letter
		/// by counting how many pixels are between its square bounding box and the first filled out pixels
		/// </summary>
		/// <param name="array">The bitmap of the letter to be measured</param>
		/// <param name="side">The side to be measured</param>
		/// <param name="position">The x (when doing top or bottom) or y (for left and right) coordinate of the line or column to be measured</param>
		/// <returns>The distance between the edge of the square bounding box and the letter in pixels</returns>
		double BorderWidth(byte[,] array, int side, int position)
		{
			try
			{
				double r = 0;
				int i;
				int h = array.GetLength(0), w = array.GetLength(1);
				int midHeight = h / 2, midWidth = w / 2;

				if (position != -1)
				{
					midHeight = position;
					midWidth = position;
				}

				switch (side)
				{
					case SIDE_TOP:
						for (i = 0; i < h; i++)
							if (array[i, midWidth] > 200)
							{
								if (i == 0)
									r = i;
								else
									// In order to make more exact measurements, we use this averaging formula to get a floating point result
									r = i - (double)array[i - 1, midWidth] / (double)array[i, midWidth];
								break;
							}
						break;
					case SIDE_RIGHT:
						for (i = w - 1; i >= 0; i--)
							if (array[midHeight, i] > 200)
							{
								if (i == w - 1)
									r = i;
								else
									r = i + (double)array[midHeight, i + 1] / (double)array[midHeight, i];
								break;
							}
						break;
					case SIDE_BOTTOM:
						for (i = h - 1; i >= 0; i--)
							if (array[i, midWidth] > 200)
							{
								if (i == h - 1)
									r = i;
								else
									r = i + (double)array[i + 1, midWidth] / (double)array[i, midWidth];
								break;
							}
						break;
					case SIDE_LEFT:
						for (i = 0; i < w; i++)
							if (array[midHeight, i] > 200)
							{
								if (i == 0)
									r = i;
								else
									r = i - (double)array[midHeight, i - 1] / (double)array[midHeight, i];
								break;
							}
						break;
				}

				return r;
			}
			catch (IndexOutOfRangeException)
			{
			}
			catch (Exception)
			{
			}

			return 0;
		}

		private double FindTranslation(byte[,] a, byte[,] b)
		{
			int w = a.GetLength(1), h = a.GetLength(0);
			//if (b.GetLength(1) != w || b.GetLength(0) != h) throw new Exception("the two arrays in FindTranslation need to be of equal size");

			int step, progress;
			// Check the left side
			double[,] distances = new double[4, 3];
			step = h / 5;
			progress = step;
			for (int i = 0; i < 3; i++)
			{
				distances[1, i] = BorderWidth(a, SIDE_RIGHT, progress);
				distances[3, i] = BorderWidth(a, SIDE_LEFT, progress);
				progress += step;
			}

			// other array
			double[,] distances2 = new double[4, 3];
			step = h / 5;
			progress = step;
			for (int i = 0; i < 3; i++)
			{
				distances2[1, i] = BorderWidth(b, SIDE_RIGHT, progress);
				distances2[3, i] = BorderWidth(b, SIDE_LEFT, progress);
				progress += step;
			}

			double x=0;
			for (int i = 0; i < 3; i++)
				x += distances2[1, i] - distances[1, i];
			for (int i = 0; i < 3; i++)
				x += distances2[3, i] - distances[3, i];
			x /= 6;

			double sdev = 0;
			for (int i = 0; i < 3; i++)
				sdev += Math.Pow((distances2[1, i] - distances[1, i]) - x, 2);
			for (int i = 0; i < 3; i++)
				sdev += Math.Pow((distances2[3, i] - distances[3, i]) - x, 2);

/*
			int[,] diffs = new int[4, 3];
			for (int j = 0; j < 4; j++)
				for (int i = 0; i < 3; i++)
					diffs[j, i] = distances2[j, i] - distances[j, i];

			if (diffs[1, 0] == diffs[1, 1] && diffs[1, 0] == diffs[1, 2])
			{
				int sds = 13;
			}

			if (diffs[0, 0] == diffs[0, 1] && diffs[0, 0] == diffs[0, 2])
			{
				int sds = 13;
			}*/


	
			/*if (min < 1000)
				Debugger.translationFound++;
			else
				Debugger.translationNotFound++;

			if (min < 1000)
				return new Point(mink, minm);
			else*/
			return x;
		}

		private Point OldFindTranslation(byte[,] a, byte[,] b)
		{
			int w = a.GetLength(1), h = a.GetLength(0);
			if (b.GetLength(1) != w || b.GetLength(0) != h)
				throw new Exception("the two arrays in FindTranslation need to be of equal size");


			const int maxDiff = 2;

			// Get all the possible translations and find the difference between the two images under those translations
			int[,] sums = new int[5, 5];
			for (int m = -maxDiff; m <= maxDiff; m++)
			{
				for (int k = -maxDiff; k <= maxDiff; k++)
				{
					for (int j = maxDiff; j < h - maxDiff; j++)
					{
						for (int i = maxDiff; i < w - maxDiff; i++)
						{
							//line[k + 1, j, i] = Math.Abs(a[y, i] - b[y, i+k]);
							int v = Math.Abs(a[j, i] - b[j + m, i + k]);
							sums[m + 2, k + 2] += v;
						}
					}
				}
			}

			// Find the translation with the least difference between a and b
			int min = 99999, minm = 0, mink = 0;
			for (int m = -maxDiff; m <= maxDiff; m++)
			{
				for (int k = -maxDiff; k <= maxDiff; k++)
				{
					if (sums[m + 2, k + 2] < min)
					{
						min = sums[m + 2, k + 2];
						minm = m;
						mink = k;
					}
				}
			}

			if (min < 1000)
				Debugger.translationFound++;
			else
				Debugger.translationNotFound++;

			if (min < 1000)
				return new Point(mink, minm);
			else
				return new Point(0, 0);
		}

		private int ComputeAbsDiff(byte[,] a, byte[,] b)
		{
			int w = Math.Min(a.GetLength(1), b.GetLength(1));
			int h = Math.Min(a.GetLength(0), b.GetLength(0));
			long r = 0;
			for (int j = 0; j < h; j++)
				for (int i = 0; i < w; i++)
					r += (a[j, i] - b[j, i]) * (a[j, i] - b[j, i]);
			//r += Math.Abs(a[j, i] - b[j, i]);

			return (int)(r / h / w);
		}

		public bool BordersMatch(SubtitleLetter other)
		{
			for (int i = 0; i < 4; i++)
			{
				if (borders[i] != -1 && other.borders[i] != -1 && Math.Abs(borders[i] - other.borders[i]) > 4)
					return false;
			}

			return true;
		}

		private int ComputeAbsDiff(byte[,] a, byte[,] b, Point translation)
		{
			int w = a.GetLength(1), h = a.GetLength(0);
			int sx = Math.Abs(translation.X), sy = Math.Abs(translation.Y);

			long r = 0;
			for (int j = sy; j < h - sy; j++)
				for (int i = sx; i < w - sx; i++)
					r += (a[j, i] - b[j + translation.Y, i + translation.X]) * (a[j, i] - b[j + translation.Y, i + translation.X]);
			//r += Math.Abs(a[j, i] - b[j + translation.Y, i + translation.X]);

			//Debugger.Print("tr = " + r);
            return (int)(r / h / w);
        }

		/*private int ComputeDiff(byte[,] a, byte[,] b)
		{
			int w = a.GetLength(1), h = a.GetLength(0);
			int r = 0;
			for (int j = 0; j < h; j++)
				for (int i = 0; i < w; i++)
					r += a[j, i] - b[j, i];

			return r * 100 / h / w;
		}*/

		public int OldMatches(SubtitleLetter o)
		{
			// If the width or height differs by more than 2 pixels, it's probably another character
			if (Math.Abs(o.image.GetLength(1) - image.GetLength(1)) > 1 || Math.Abs(o.image.GetLength(0) - image.GetLength(0)) > 1)
				return 999999;

			//double translation = FindTranslation(i1, i2);
			double translation = FindTranslation(image, o.image);
			byte[,] moved = o.MoveLetter(-translation);

			// Build an array out of their differences
			DateTime t = DateTime.Now;
			int ad = ComputeAbsDiff(image, moved);
			Debugger.Print("diff (" + Text + ") = " + ad);
			Debugger.absDiffTime += (DateTime.Now - t).TotalMilliseconds;

			return ad;
		}

		private byte[,] Widen(byte[,] img)
		{
			int w = img.GetLength(1);
			int h = img.GetLength(0);

			byte x;
			byte[,] r = new byte[h, w];
			for (int j = 0; j < h; j++)
				for (int i = 0; i < w; i++)
				{
					x = img[j, i];
					if (j > 0 && img[j - 1, i] > x)
						x = img[j - 1, i];
					if (j < h - 1 && img[j + 1, i] > x)
						x = img[j + 1, i];
					if (i > 0 && img[j, i - 1] > x)
						x = img[j, i - 1];
					if (i < w-1 && img[j, i + 1] > x)
						x = img[j, i + 1];

					r[j, i] = (byte)Math.Min(255, x * 2);
				}

			return r;
		}

		private int Difference(byte[,] a, byte[,] b)
		{
			DateTime t = DateTime.Now;
			byte[,] bWidened = Widen(b);
			Debugger.widenTime += (DateTime.Now - t).TotalMilliseconds;

			int w = Math.Min(a.GetLength(1), bWidened.GetLength(1));
			int h = Math.Min(a.GetLength(0), bWidened.GetLength(0));
			int r = 0;
			byte[,] difference = new byte[h, w];
			for (int j = 0; j < h; j++)
				for (int i = 0; i < w; i++)
					r += (byte)Math.Max(0, a[j, i] - bWidened[j, i]);

			return r;
/*			byte[,] bWidened = Widen(b);

			int w = Math.Min(a.GetLength(1), bWidened.GetLength(1));
			int h = Math.Min(a.GetLength(0), bWidened.GetLength(0));
			byte[,] difference = new byte[h, w];
			for (int j = 0; j < h; j++)
				for (int i = 0; i < w; i++)
					difference[j, i] = (byte)Math.Max(0, a[j, i] - bWidened[j, i]);

			int r = 0;
			for (int j = 0; j < h; j++)
				for (int i = 0; i < w; i++)
					r += difference[j, i];

			return r;*/
		}

		public int Matches(SubtitleLetter o)
		{
			// If the width or height differs by more than a pixel, it's probably another character
			if (Math.Abs(o.image.GetLength(1) - image.GetLength(1)) > 1 || Math.Abs(o.image.GetLength(0) - image.GetLength(0)) > 1)
				return 999999;

			//double translation = FindTranslation(i1, i2);
			DateTime t = DateTime.Now;
			double translation = FindTranslation(image, o.image);
			byte[,] moved = o.MoveLetter(-translation);
			Debugger.translationTime += (DateTime.Now - t).TotalMilliseconds;

			//Debugger.Print("########################################################################");
			//Debugger.Draw2DArray(image);
			//Debugger.Print("------------------------------------------------------------------------");
			//Debugger.Draw2DArray(moved);

			t = DateTime.Now;
			int diff1 = Difference(image, moved) + Difference(moved, image);
			Debugger.diffTime += (DateTime.Now - t).TotalMilliseconds;
            if (diff1 > 0)
			    return diff1 + 1000;

			t = DateTime.Now;
			int ad = ComputeAbsDiff(image, moved);
			Debugger.absDiffTime += (DateTime.Now - t).TotalMilliseconds;

            return Math.Min(1000, ad / 10);
		}

		private byte[,] MoveLetter(double p)
		{
			if (p == 0.0)
				return image;

			byte[,] r = new byte[ImageHeight, ImageWidth];

			int direction = Math.Sign(p) * (int)(Math.Abs(p) + 1);
			int first = (direction > 0) ? direction - 1 : direction + 1;
			double p2 = Math.Abs(Math.IEEERemainder(p, 1.0));
			int b1, b2;

			double ddf = Math.Floor(-1.4);

			for (int j = 0; j < ImageHeight; j++)
			{
				for (int i = 0; i < ImageWidth; i++)
				{
					if (i - first >= 0 && i - first < ImageWidth)
						b1 = image[j, i - first];
					else
						b1 = 0;
					if (i - direction >= 0 && i - direction < ImageWidth)
						b2 = image[j, i - first];
					else
						b2 = 0;
					
					r[j, i] = (byte)((1 - p2) * b1 + p2 * b2);
				}
			}

			return r;
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder(50);
			if (text != null)
				sb.Append("\"" + text + "\" ");

			if (coords != null)
				sb.AppendFormat("({0}/{1}), ({2}/{3})", coords.Left, coords.Top, coords.Right, coords.Bottom);

			return sb.ToString();
		}

		public string StringDrawing()
		{
			int h = image.GetLength(0);
			int w = image.GetLength(1);

			StringBuilder sb = new StringBuilder(1000);
			for (int j = 0; j < h; j++)
			{
				for (int i = 0; i < w; i++)
				{
					if (image[j, i] > 200)
						sb.Append('#');
					else if (image[j, i] > 100)
						sb.Append('+');
					else if (image[j, i] > 50)
						sb.Append('.');
					else
						sb.Append(' ');
				}
				sb.Append("\n");
			}
			return sb.ToString();
		}

		public string DumpLetter()
		{
			int h = image.GetLength(0);
			int w = image.GetLength(1);

			StringBuilder sb = new StringBuilder(1000);
			for (int j = 0; j < h; j++)
			{
				for (int i = 0; i < w; i++)
				{
					sb.AppendFormat("{0:000} ", image[j, i]);
				}
				sb.Append("\n");
			}
			return sb.ToString();
		}

		public Bitmap GetBitmap()
		{
			if (image == null)
				return null;

			int w = image.GetLength(1);
			int h = image.GetLength(0);

			Bitmap r = new Bitmap(w + 4, h + 4, PixelFormat.Format32bppArgb);
			BitmapData bmpData = r.LockBits(new Rectangle(0, 0, r.Width, r.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

			byte[] by = new byte[r.Width * r.Height * 4];
			for (int i = 0; i < by.Length; i++)
			{
				int x = (i / 4) % r.Width;
				int y = (i / 4) / r.Width;

				if (x < 2 || x >= r.Width - 2 || y < 2 || y >= r.Height - 2)
				{
					if (i % 4 == 3)
						by[i] = 255;
					else
						by[i] = 0;
				}
				else
				{
					if (i % 4 == 3)
						by[i] = 255;
					else
						by[i] = image[y - 2, x - 2];
				}
			}

			Marshal.Copy(by, 0, bmpData.Scan0, by.Length);
			r.UnlockBits(bmpData);
			
			return r;
		}
	}
}
