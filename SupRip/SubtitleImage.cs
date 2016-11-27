using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SupRip
{
	class SubtitleImage
	{
		public Bitmap subtitleBitmap;
		public SubtitleCaption caption;
		private byte[,] subtitleArray, uncorrectedArray;
		private int height, width;
		private TextLine[] textLines;
		public LinkedList<SubtitleLetter> letters, alternativeLetters;
		public SortedList<int, Space> debugLocations;
		public LinkedList<Point> debugPoints;
		
		class EndOfImageReachedException : Exception
		{
		}

		class NoSubtitleTextException : Exception
		{
		}

		public SubtitleImage(Bitmap source, SubtitleCaption parent)
		{
		    subtitleBitmap = source;
			caption = parent;

			CreateSubtitleArray();
		}

		/// <summary>
		/// This function creates a standard byte[] array to be more easily processable
		/// It also applies contrast correction and scans for and corrects italic lines
		/// </summary>
		private void CreateSubtitleArray()
		{
			// Copy the bitmap to an array to perform postprocessing
			BitmapData bmpData = subtitleBitmap.LockBits(new Rectangle(0, 0, subtitleBitmap.Width, subtitleBitmap.Height), ImageLockMode.ReadOnly, subtitleBitmap.PixelFormat);
			byte[] bitmapBytes = new byte[subtitleBitmap.Size.Width * subtitleBitmap.Size.Height * 4];
			IntPtr ptr = bmpData.Scan0;
			Marshal.Copy(ptr, bitmapBytes, 0, bitmapBytes.Length);
			subtitleBitmap.UnlockBits(bmpData);

			// Then, convert it to grayscale while zeroing out the really dark parts
			width = subtitleBitmap.Size.Width;
			height = subtitleBitmap.Size.Height;
			subtitleArray = new byte[subtitleBitmap.Size.Height, subtitleBitmap.Size.Width];
			int k = 0;
			for (int j = 0; j < subtitleBitmap.Size.Height; j++)
				for (int i = 0; i < subtitleBitmap.Size.Width; i++)
				{
					byte v = (byte)((bitmapBytes[k++] + bitmapBytes[k++] + bitmapBytes[k++]) * bitmapBytes[k++] / 768);
					if (v < 30)
						subtitleArray[j, i] = 0;
					else
						subtitleArray[j, i] = v;
				}

			// Check if we need to apply any contrast correction
			if (AppOptions.contrast != 0)
			{
				double fact = 0.5 + AppOptions.contrast / 5;

				for (int j = 0; j < subtitleBitmap.Size.Height; j++)
					for (int i = 0; i < subtitleBitmap.Size.Width; i++)
					{
						subtitleArray[j, i] = Logistic(subtitleArray[j, i], fact);
					}
			}

			DateTime t = DateTime.Now;
			textLines = FindTextLines(subtitleArray);
			AdjustItalicLines();
			letters = FindLetters(subtitleArray, false);
			alternativeLetters = FindLetters(uncorrectedArray, true);
			Debugger.lettersTime += (DateTime.Now - t).TotalMilliseconds;
		}

		byte Logistic(byte x, double fact)
		{
			double arg = ((double)x - 128) * 5 / 128;
			double r = 1 / (1 + Math.Pow(Math.E, -arg * fact));

			return (byte)(r * 256);
		}

		class TextLine
		{
			private int num;
			private int start, end;
			public double angle;

			public TextLine(int n, int s, int e)
			{
				num = n;
				start = s;
				end = e;
			}

			public int Num
			{
				get { return num; }
				set { num = value; }
			}

			public int Height
			{
				get { return end - start; }
			}

			public int Start
			{
				get { return start; }
				set { start = value; }
			}

			public int End
			{
				get { return end; }
				set { end = value; }
			}
		}





		/// <summary>
		/// Creates a list of the text lines in this subtitle and their coordinates
		/// </summary>
		/// <param name="limit"></param>
		/// <returns>An array of TextLine objects</returns>
		private TextLine[] FindTextLines(byte[,] workArray)
		{
			LinkedList<TextLine> list = new LinkedList<TextLine>();

			int height = workArray.GetLength(0);
			int width = workArray.GetLength(1);

			int lineStart = 0, lineEnd = 0, y;
			int lineNumber = 0;

			for (lineNumber = 0; ; lineNumber++)
			{
				// Scan down until we find some text
				y = lineEnd;
				while (y < height && !LineContainsPixels(workArray, y))
					y++;

				if (y == height)
					break;
				
				lineStart = y;

				// Continue to scan until we stop finding text
				while (y < height && LineContainsPixels(workArray, y))
					y++;

				if (y == height)
				{
					lineEnd = height - 1;
					if (lineEnd - lineStart > 20)
						list.AddLast(new TextLine(lineNumber, lineStart, lineEnd));
					break;
				}

				lineEnd = y;

				// We found a line
				list.AddLast(new TextLine(lineNumber, lineStart, lineEnd));
			}

			TextLine[] r = new TextLine[list.Count];
			list.CopyTo(r, 0);

			int newCount = r.Length;
			TextLine lastFullLine = null;
			for (int i=0; i<r.Length; i++)
			{
				if (r[i].Height < 20)
				{
					if (r.Length == 1)
						throw new Exception("Could only find one line of text, and it's smaller than 20 pixels");

					// If it's smaller, it likely is just some accents that either belong to the line above or below
					if (i == 0)
						r[1].Start = r[0].Start;
					else if (i == r.Length - 1)
						lastFullLine.End = r[i].End;
					else
					{
						int topDistance = r[i].Start - lastFullLine.End;
						int bottomDistance = r[i + 1].Start - r[i].End;

						// Try to decide whether to assign this line of accents to the top or bottom text line
						if (topDistance < 5 || bottomDistance / topDistance > 2)
							lastFullLine.End = r[i].End;
						else if (bottomDistance < 5 || topDistance / bottomDistance > 2)
							r[i + 1].Start = r[i].Start;
						else if (topDistance < bottomDistance)
							lastFullLine.End = r[i].End;
						else
							r[i + 1].Start = r[i].Start;

					}

					r[i] = null;
					newCount--;
				}
				else
					lastFullLine = r[i];
			}

			// Remove all the nulled lines
			int j = 0;
			TextLine[] r2 = new TextLine[newCount];
			for (int i = 0; i < r.Length; i++)
			{
				if (r[i] != null)
				{
					r2[j] = r[i];
					j++;
				}
			}

			return r2;
		}

		private static LinkedList<double> angleList;
		private static int angleCount;
		private static double italicAngle;
		private double FindLineAngle(byte[,] image, TextLine line)
		{
			const int numAngles = 20;
			const double angleResolution = 30;

			int w = image.GetLength(1);
			int[] emptyColumns = new int[numAngles];

			double r;
			if (italicAngle != 0.0)
			{
				// If we've already found a good common italic angle for this subtitle, use it
				for (int i = 0; i < w; i++)
				{
					if (!ColumnContainsPixels(subtitleArray, i, line.Start, line.End))
						emptyColumns[0]++;
					if (!ColumnContainsPixels(subtitleArray, i, italicAngle, line.Start, line.End))
						emptyColumns[1]++;
				}

				if (emptyColumns[1] > emptyColumns[0])
					r = italicAngle;
				else
					r = 0;
			}
			else
			{
				if (angleList == null)
					angleList = new LinkedList<double>();

				int ecMax = 0, ecMaxIndex = -1;
				for (int j = 0; j < numAngles; j++)
				{
					// Don't scan for really slight inclinations as they're probably just misdetected normal letters
					if (j > 0 && j < 5)
						continue;

					for (int i = 0; i < w; i++)
					{
						if (!ColumnContainsPixels(subtitleArray, i, (double)j / angleResolution, line.Start, line.End))
							emptyColumns[j]++;
					}
					//Debugger.Print(j + ": " + emptyColumns[j]);

					if (emptyColumns[j] > ecMax)
					{
						ecMax = emptyColumns[j];
						ecMaxIndex = j;
					}
				}

				if (ecMaxIndex != 0 && angleCount < 20)
				{
					angleList.AddLast((double)ecMaxIndex / angleResolution);
					angleCount++;
				}

				if (angleCount == 20)
				{
					foreach (double d in angleList)
						italicAngle += d;

					italicAngle /= 20;
					//Debugger.Print("decided on " + italicAngle);
				}

				r = (double)ecMaxIndex / angleResolution;
			}

			return r;
		}

		private void FindSpaces(int yStart, int yEnd, bool partial, ref SortedList<int, Space> spaces)
		{
			// First, try to find straight breaks between letters that go through the whole height
			int[] scores = new int[subtitleBitmap.Size.Width];
			for (int i = 0; i < subtitleBitmap.Size.Width; i++)
				scores[i] = ColumnFilledPixels(subtitleArray, i, yStart, yEnd);

			int pixelSetLimit = 1;
			int xStart, xEnd;
			bool state;

			state = false;// scores[0] > pixelSetLimit;
			xStart = xEnd = 0;
			for (int x = 0; x < subtitleBitmap.Size.Width; x++)
			{
				if (state && scores[x] < pixelSetLimit)
				{
					// There was a letter to the left of us, but now it isn't anymore. Start a new rectangle
					xStart = x;
					state = false;
				}

				if (!state && scores[x] >= pixelSetLimit || !state && x == subtitleBitmap.Size.Width - 1)
				{
					// There was an empty space to the left of us, but now it is a letter. Finish and save a rectangle
					if (x - xStart >= AppOptions.charSplitTolerance || x < AppOptions.charSplitTolerance || x >= subtitleBitmap.Size.Width - AppOptions.charSplitTolerance)
					{
						xEnd = x;
						if (x == subtitleBitmap.Size.Width - 1)
							xEnd++;

						Rectangle newRect = new Rectangle(xStart, yStart, xEnd - xStart, yEnd - yStart);

						bool intersects = false;
						if (partial)
						{
							foreach (KeyValuePair<int, Space> kvp in spaces)
							{
								Space r = kvp.Value;
								if (Intersects(newRect, r.Rect))
								{
									intersects = true;
									break;
								}
							}
						}

						if (!intersects)
						{
							Space nr = new Space(newRect, partial);
							spaces.Add(nr.Hash, nr);
						}
					}

					state = true;
				}
			}
		}

		private bool Intersects(Rectangle r1, Rectangle r2)
		{
			return !(r2.Left > r1.Right || r2.Right < r1.Left || r2.Top > r2.Bottom || r2.Bottom < r2.Top);
		}

		private bool Intersects(Rectangle r1, SortedList<int, Space> rList)
		{
			foreach (KeyValuePair<int, Space> kvp in rList)
			{
				if (Intersects(r1, kvp.Value.Rect))
					return true;
			}

			return false;
		}

		private void ExtendPartialSpace(int yStart, int yEnd, Space r)
		{
			if (!r.Partial)
				return;

			int y;
			if (r.Rect.Top == yStart)
			{
				for (y = r.Rect.Bottom; y < yEnd; y++)
				{
					if (LineContainsPixels(subtitleArray, y, r.Rect.Left, r.Rect.Right))
						break;
				}
				r.Resize(0, 0, 0, y - r.Rect.Bottom);
			}
			if (r.Rect.Bottom == yEnd)
			{
				for (y = r.Rect.Top; y >= yStart; y--)
				{
					if (LineContainsPixels(subtitleArray, y, r.Rect.Left, r.Rect.Right))
						break;
				}
				r.Resize(0, r.Rect.Top - y, 0, 0);
			}

			if (r.Rect.Top <= yStart && r.Rect.Bottom >= yEnd - 1)
				r.Partial = false;
		}

		/// <summary>
		/// Takes a list of rectangles that reach to half height and attempts to extend them either up or down until they reach a filled pixel
		/// </summary>
		/// <param name="yStart">Start of the line of text that we're allowed to work in</param>
		/// <param name="yEnd">End of the line of text</param>
		/// <param name="rectangles">The list of rectangles that we should attempt to extend</param>
		private void ExtendPartialSpaces(int yStart, int yEnd, ref SortedList<int, Space> spaces)
		{
			foreach (KeyValuePair<int, Space> kvp in spaces)
			{
				Space r = kvp.Value;
				ExtendPartialSpace(yStart, yEnd, kvp.Value);
			}
		}

		/// <summary>
		/// Cleans up a list of rectangle by deleting all the ones that are still marked as partial. They come from unsuccessful attempts to find a space
		/// and only clutter up the data structures
		/// </summary>
		/// <param name="rectangles">List that we should clean up</param>
		private void CleanupSpaces(ref SortedList<int, Space> spaces)
		{
			int lastFullSpace = -10000;

			LinkedList<int> deleteList = new LinkedList<int>();
			foreach (KeyValuePair<int, Space> kvp in spaces)
			{
				// Delete all the partial rectangles
				if (kvp.Value.Partial)
					deleteList.AddLast(kvp.Key);
				else
				{
					// Also delete spaces that are less than 5 pixels apart
					if (kvp.Value.Rect.Left - lastFullSpace < 4)
						deleteList.AddLast(kvp.Key);

					lastFullSpace = kvp.Value.Rect.Right;
				}
			}
			foreach (int toDelete in deleteList)
				spaces.Remove(toDelete);
		}

		private void FindDiagonalBreaks(int yStart, int yEnd, ref SortedList<int, Space> rectangles)
		{
			SortedList<int, Space> newRects = new SortedList<int, Space>();

			int splitLimit = AppOptions.charSplitTolerance;
			const int verticalDisplacement = 2;
			const double maxAngle = 0.5;

			foreach (KeyValuePair<int, Space> kvp in rectangles)
			{
				Space r = kvp.Value;
				if (!r.Partial)
					continue;

				//if (r.Rect.X != 288) continue;

				bool found = false;

				// Check the bottom half rectangles
				if (r.Rect.Bottom == yEnd)
				{
					// Try to the right
					for (double k = 0.1; k < maxAngle; k += 0.1)
					{
						int x;
						for (x = r.Rect.Right; x > r.Rect.Right - splitLimit; x--)
							if (ColumnContainsPixels(subtitleArray, x, k, yStart, r.Rect.Top + verticalDisplacement, Anchor.Bottom))
								break;

						if (x <= r.Rect.Right - splitLimit)
						{
							Space bgr = new Space(r.Rect.Right, yStart, 0, yEnd - yStart - 1, false, Space.SpaceType.TopRight, r.Rect.Top - yStart + verticalDisplacement, k);
							newRects.Add(bgr.Hash, bgr);
							found = true;
							break;
						}
					}

					if (found)
						continue;

					// Try to the left
					for (double k = -0.1; k > -maxAngle; k -= 0.1)
					{
						int x;
						for (x = r.Rect.Left - 1; x < r.Rect.Left + splitLimit - 1; x++)
							if (ColumnContainsPixels(subtitleArray, x, k, yStart, r.Rect.Top + verticalDisplacement, Anchor.Bottom))
								break;

						if (x >= r.Rect.Left + splitLimit - 1)
						{
							Space bgr = new Space(r.Rect.Left, yStart, 0, yEnd - yStart - 1, false, Space.SpaceType.TopLeft, r.Rect.Top - yStart + verticalDisplacement, k);
							newRects.Add(bgr.Hash, bgr);
							break;
						}
					}
				}
				else if (r.Rect.Top == yStart)
				{
					// Try to the right
					for (double k = -0.1; k > -maxAngle; k -= 0.1)
					{
						int x;
						for (x = r.Rect.Right; x > r.Rect.Right - splitLimit; x--)
							if (ColumnContainsPixels(subtitleArray, x, k, r.Rect.Bottom - verticalDisplacement, yEnd, Anchor.Top))
								break;

						if (x <= r.Rect.Right - splitLimit)
						{
							Space bgr = new Space(r.Rect.Right, yStart, 0, yEnd - yStart - 1, false, Space.SpaceType.BottomRight, r.Rect.Bottom - yStart - verticalDisplacement, k);
							newRects.Add(bgr.Hash, bgr);
							break;
						}
					}

					if (found)
						continue;

					// Try to the left
					for (double k = 0.1; k < maxAngle; k += 0.1)
					{
						int x;
						for (x = r.Rect.Left - 1; x < r.Rect.Left + splitLimit - 1; x++)
							if (ColumnContainsPixels(subtitleArray, x, k, r.Rect.Bottom - verticalDisplacement, yEnd, Anchor.Top))
								break;

						if (x >= r.Rect.Left + splitLimit - 1)
						{
							Space bgr = new Space(r.Rect.Left, yStart, 0, yEnd - yStart - 1, false, Space.SpaceType.BottomLeft, r.Rect.Bottom - yStart - verticalDisplacement, k);
							newRects.Add(bgr.Hash, bgr);
							break;
						}
					}
				}
			}

			// Add the completed letter split rectangles to our main list
			foreach (KeyValuePair<int, Space> kvp in newRects)
			{
				//rectangles.Remove(kvp.Key);
				rectangles.Add(kvp.Value.Hash+1, kvp.Value);
			}
		}

		/// <summary>
		/// Applies some shear to the subtitlearray so that italic characters are standing vertical
		/// </summary>
		/// <param name="yStart">Start of the area to be transformed</param>
		/// <param name="yEnd">End of the area</param>
		/// <param name="angle">Angle of the letters</param>
		private void CorrectItalics(int yStart, int yEnd, double angle)
		{
			double dx, p;
			int dxi;
			byte[,] newArray = new byte[height, width];

			for (int j = yStart; j < yEnd; j++)
			{
				dx = angle * ((yStart + yEnd) / 2 - j);
				p = dx - Math.Floor(dx);
				dxi = (int)Math.Floor(dx);

				for (int i = 0; i < width; i++)
				{
					if (i + dxi >= 0 && i + dxi + 1 < width)
						newArray[j, i] = (byte)((1 - p) * subtitleArray[j, i + dxi] + p * subtitleArray[j, i + dxi + 1]);
				}
			}
			for (int j = 0; j < yStart; j++)
				for (int i = 0; i < width; i++)
					newArray[j, i] = subtitleArray[j, i];
			for (int j = yEnd; j < height; j++)
				for (int i = 0; i < width; i++)
					newArray[j, i] = subtitleArray[j, i];

			subtitleArray = newArray;
		}

		private void AdjustItalicLines()
		{
			uncorrectedArray = (byte[,])subtitleArray.Clone();

			foreach (TextLine line in textLines)
			{
				DateTime t = DateTime.Now;
				line.angle = FindLineAngle(subtitleArray, line);
				//Debugger.Print("a = " + angle);
				if (line.angle != 0.0)
					CorrectItalics(line.Start, line.End, line.angle);
				Debugger.angleTime += (DateTime.Now - t).TotalMilliseconds;
			}
		}

		public LinkedList<SubtitleLetter> FindLetters(byte[,] workArray, bool reverseItalics)
		{
			//letterLocations = new SubtitleLetter[0];
			//return;
			Rectangle rect;
			SubtitleLetter l;
			int xStart, xEnd, yStart, yEnd;
			LinkedList<SubtitleLetter> r = new LinkedList<SubtitleLetter>();

			DateTime t = DateTime.Now;
			Debugger.linesTime += (DateTime.Now - t).TotalMilliseconds;

			debugLocations = new SortedList<int, Space>();
			debugPoints = new LinkedList<Point>();

			SortedList<int, Space> spaceRectangles;

			int lineNum = 0;
			foreach (TextLine line in textLines)
			{
				lineNum++;
				// If this isn't the first line, insert a line feed.
				if (line.Num != 0)
				{
					rect = new Rectangle(1, line.Start - 20, 10, 25);
					r.AddLast(new SubtitleLetter(rect, "\r\n"));
				}

				//Debugger.Draw2DArray(subtitleArray);

				t = DateTime.Now;
				spaceRectangles = new SortedList<int, Space>();
				FindSpaces(line.Start, line.End, false, ref spaceRectangles);
				// Check the top and bottom two-thirds
				FindSpaces(line.Start, line.Start + line.Height * 2 / 3, true, ref spaceRectangles);
				FindSpaces(line.Start + (line.End - line.Start) / 3, line.End, true, ref spaceRectangles);
				// Check the top and bottom half
				FindSpaces(line.Start, (line.Start + line.End) / 2, true, ref spaceRectangles);
				FindSpaces((line.Start + line.End) / 2, line.End, true, ref spaceRectangles);

				MergeSpaces(line.Start, line.End, ref spaceRectangles);

				ExtendPartialSpaces(line.Start, line.End, ref spaceRectangles);
				//foreach (KeyValuePair<int, Space> kvp in spaceRectangles) debugLocations.Add(kvp.Key, kvp.Value);
				FindDiagonalBreaks(line.Start, line.End, ref spaceRectangles);


				CleanupSpaces(ref spaceRectangles);
				Debugger.spacesTime += (DateTime.Now - t).TotalMilliseconds;


				Space lastSpace = null;
				foreach (KeyValuePair<int, Space> kvp in spaceRectangles)
				{
					if (lastSpace == null)
					{
						lastSpace = kvp.Value;
						continue;
					}

					xStart = lastSpace.Rect.Right;
					xEnd = kvp.Value.Rect.Left;

					// If there was a lot of empty space, it was probably a space character and should be marked even if empty
					if (lastSpace.Rect.X != 0 && lastSpace.Rect.Width > AppOptions.minimumSpaceCharacterWidth)
					{
						rect = new Rectangle(lastSpace.Rect.Left + 3, line.Start + 4, lastSpace.Rect.Width - 6, line.Height - 10);
						r.AddLast(new SubtitleLetter(rect, line.angle, " "));
					}

					yStart = line.Start;
					yEnd = line.End;

					rect = new Rectangle(xStart, yStart, xEnd - xStart, yEnd - yStart);
					t = DateTime.Now;
					l = ExtractLetter(rect, line.angle, lastSpace, kvp.Value);
					Debugger.extractTime += (DateTime.Now - t).TotalMilliseconds;
					if (l != null)
					{
						l.Height = (l.Coords.Y + l.Coords.Bottom) / 2 - (line.Start + line.End) / 2;
						r.AddLast(l);
					}

					lastSpace = kvp.Value;
				}
			}

			return r;
		}

		private void MergeSpaces(int yStart, int yEnd, ref SortedList<int, Space> rectangles)
		{
			Space lastSpace = null;
			int lastKey = -1;

			LinkedList<int> deleteList = new LinkedList<int>();
			LinkedList<Space> addList = new LinkedList<Space>();

			foreach (KeyValuePair<int, Space> kvp in rectangles)
			{
				if (lastSpace != null)
				{
					if (lastSpace.Partial == true && lastSpace.Partial == kvp.Value.Partial && kvp.Value.Rect.Left - lastSpace.Rect.Right < 10)
					{
						if (lastSpace.Rect.Top == kvp.Value.Rect.Top || lastSpace.Rect.Bottom == kvp.Value.Rect.Bottom)
						{
							Space t = new Space(lastSpace.Rect.X, lastSpace.Rect.Bottom - 1, kvp.Value.Rect.Right - lastSpace.Rect.X, 1, true);
							ExtendPartialSpace(yStart, yEnd, t);
							if (lastSpace.Rect.Height - t.Rect.Height < 5 && kvp.Value.Rect.Height - t.Rect.Height < 5)
							{
								deleteList.AddLast(kvp.Key);
								deleteList.AddLast(lastKey);
								addList.AddLast(t);
							}
						}
					}
				}

				lastSpace = kvp.Value;
				lastKey = kvp.Key;
			}

			foreach (int toDelete in deleteList)
				rectangles.Remove(toDelete);
			foreach (Space toAdd in addList)
				rectangles.Add(toAdd.Hash, toAdd);
		}

		/*
		private void FindBorders()
		{
			// Do a few sanity checks on the current state of the application
			if (subtitleBitmap == null)
				throw new Exception("Trying to use FindBorders on a null image");

			if (subtitleBitmap.PixelFormat != PixelFormat.Format32bppArgb)
				throw new Exception("Pixel format isn't Format32bppArgb");

			// Read the bitmap into managed memory for easier handling
			BitmapData bmpData = subtitleBitmap.LockBits(new Rectangle(0, 0, subtitleBitmap.Width, subtitleBitmap.Height), ImageLockMode.ReadOnly, subtitleBitmap.PixelFormat);
			byte[] bitmapBytes = new byte[subtitleBitmap.Size.Width * subtitleBitmap.Size.Height * 4];
			IntPtr ptr = bmpData.Scan0;
			Marshal.Copy(ptr, bitmapBytes, 0, bitmapBytes.Length);
			subtitleBitmap.UnlockBits(bmpData);

			// Find the borders of the subtitle by scanning the lines for non-transparent pixels
			int firstLine = -1, lastLine = -1;
			int firstColumn = -1, lastColumn = -1;
			int step;

			step = 20;
			for (int i = 0; i < subtitleBitmap.Size.Height; i += step)
			{
				if (LineContainsPixels(bitmapBytes, subtitleBitmap.Size.Width, i, 200))
				{
					if (step == 1)
					{
						firstLine = i;
						break;
					}
					else if (step == 4)
					{
						i -= step;
						step = 1;
					}
					else
					{
						i -= step * 2;
						step = 4;
					}
				}
			}

			step = 4;
			for (int i = subtitleBitmap.Size.Height - 1; i > firstLine; i -= step)
			{
				if (LineContainsPixels(bitmapBytes, subtitleBitmap.Size.Width, i, 200))
				{
					if (step == 1)
					{
						lastLine = i;
						break;
					}
					else
					{
						i += step;
						step = 1;
					}
				}
			}

			step = 4;
			for (int i = 0; i < subtitleBitmap.Size.Width; i += step)
			{
				if (ColumnContainsPixels(bitmapBytes, subtitleBitmap.Size.Width, i, 200))
				{
					if (step == 1)
					{
						firstColumn = i;
						break;
					}
					else
					{
						i -= step;
						step = 1;
					}
				}
			}

			step = 4;
			for (int i = subtitleBitmap.Size.Width - 1; i > firstColumn; i -= step)
			{
				if (ColumnContainsPixels(bitmapBytes, subtitleBitmap.Size.Width, i, 200))
				{
					if (step == 1)
					{
						lastColumn = i;
						break;
					}
					else
					{
						i += step;
						step = 1;
					}
				}
			}

			// Check whether we found any content in this image that might contain subtitle text
			if (firstLine == -1 || lastLine == -1 || firstColumn == -1 || lastColumn == -1)
				throw new NoSubtitleTextException();

			// Store the found borders in our array
			subtitleBorders = new Rectangle(firstColumn, firstLine, lastColumn - firstColumn, lastLine - firstLine);
		}
		*/
		#region Column and Line Searching functions

		private bool LineContainsPixels(byte[] bytes, int w, int line, int limit)
		{
			for (int i = line * w * 4; i < (line + 1) * w * 4; i += 4)
			{
				if ((bytes[i] > limit || bytes[i + 1] > limit || bytes[i + 2] > limit) && bytes[i + 3] > 0)
					return true;
			}
			return false;
		}

		private bool LineContainsPixels(byte[] bytes, int w, int line, int limit, int x1, int x2)
		{
			for (int i = (line * w + x1) * 4; i < (line * w + x2) * 4; i += 4)
			{
				if ((bytes[i] > limit || bytes[i + 1] > limit || bytes[i + 2] > limit) && bytes[i + 3] > 0)
					return true;
			}
			return false;
		}

		private bool ColumnContainsPixels(byte[] bytes, int w, int column, int limit)
		{
			for (int i = column * 4; i < bytes.Length; i += w * 4)
			{
				if ((bytes[i] > limit || bytes[i + 1] > limit || bytes[i + 2] > limit) && bytes[i + 3] > 0)
					return true;
			}
			return false;
		}

		#endregion

		public const int pixelLimitAsSet = 60;
		private bool LineContainsPixels(byte[,] image, int line)
		{
			return LineContainsPixels(image, line, 0, image.GetLength(1));
		}

		/// <summary>
		/// Checks whether a certain line has any set pixels between x1 and x2
		/// </summary>
		/// <param name="image">The subtitle image to check</param>
		/// <param name="line">Which line to check</param>
		/// <param name="x1">The left border</param>
		/// <param name="x2">The right border</param>
		/// <returns>True or false</returns>
		private bool LineContainsPixels(byte[,] image, int line, int x1, int x2)
		{
			int i;
			for (i = x1; i < x2 && image[line, i] < pixelLimitAsSet; i++)
				;

			return i < x2;
		}

		private bool ColumnContainsPixels(byte[,] image, int column)
		{
			return ColumnContainsPixels(image, column, 0, image.GetLength(0));
		}

		private bool ColumnContainsPixels(byte[,] image, int column, int y1, int y2)
		{
			int i;
			for (i = y1; i < y2 && image[i, column] < pixelLimitAsSet; i++)
				;

			return i < y2;
		}

		private int ColumnFilledPixels(byte[,] image, int column, int y1, int y2)
		{
			int r = 0;
			for (int i = y1; i < y2; i++)
			{
				if (image[i, column] >= pixelLimitAsSet)
					r++;
			}

			return r;
		}

		private bool ColumnContainsPixels(byte[,] image, int column, double angle, int y1, int y2)
		{
			if (angle == 0.0)
				return ColumnContainsPixels(image, column, y1, y2);

			int width = image.GetLength(1);

			int x1 = column + (int)((y2 - y1) * angle) / 2;

			int ymax, ymin;
			if (angle > 0.0)
			{
				ymin = Math.Max(y1 + (int)((x1 - width + 1) / angle) + 1, y1);
				ymax = Math.Min(y1 + (int)(x1 / angle), y2);
			}
			else
			{
				ymin = Math.Max(y1 + (int)(x1 / angle) + 1, y1);
				ymax = Math.Min(y1 + (int)((x1 - width + 1) / angle), y2);
			}

			int i;
			for (i = ymin; i < ymax && image[i, x1 - (int)((i - y1) * angle)] < pixelLimitAsSet; i++)
				//debugPoints.AddLast(new Point(x1 - (int)((i - y1) * angle), i));
				;

			return i < ymax;
		}

		enum Anchor { Center, Top, Bottom };
		private bool ColumnContainsPixels(byte[,] image, int column, double angle, int y1, int y2, Anchor anchor)
		{
			if (anchor == Anchor.Center)
				return ColumnContainsPixels(image, column, angle, y1, y2);
			else if (anchor == Anchor.Bottom)
			{
				int c = column + (int)((y2 - y1) * angle) / 2;
				return ColumnContainsPixels(image, c, angle, y1, y2);
			}
			else // anchor == Anchor.Top
			{
				int c = column - (int)((y2 - y1) * angle) / 2;
				return ColumnContainsPixels(image, c, angle, y1, y2);
			}
		}

		public string GetText()
		{
			if (letters == null)
				return "";

			StringBuilder sb = new StringBuilder(100);

			bool italic = false;
			foreach (SubtitleLetter l in letters)
			{
				if (l.Angle != 0.0 && !italic)
				{
					sb.Append("<i>");
					italic = true;
				}
				if (l.Angle == 0.0 && italic)
				{
					sb.Append("</i>");
					italic = false;
				}

				if (l.Text != null)
					sb.Append(l.Text);
				else
					sb.Append("¤");
			}

			if (italic)
				sb.Append("</i>");

			return sb.ToString();
		}


		enum Side { Left, Right }
		byte[,] TrimExtension(byte[,] old, Side side)
		{
			int height = old.GetLength(0);
			int width = old.GetLength(1);
			int newWidth;
			byte[,] r = null;

			if (side == Side.Right)
			{
				for (newWidth = width - 1; newWidth >= 0; newWidth--)
					if (ColumnContainsPixels(old, newWidth))
						break;

				// If this array doesn't contain any set pixels at all, just discard it
				if (newWidth == -1)
					return null;

				newWidth++;
				r = new byte[height, newWidth];

				for (int j = 0; j < height; j++)
				{
					for (int i = 0; i < newWidth; i++)
					{
						r[j, i] = old[j, i];
					}
				}
			}
			else if (side == Side.Left)
			{
				for (newWidth = 0; newWidth < width; newWidth++)
					if (ColumnContainsPixels(old, newWidth))
						break;

				// If this array doesn't contain any set pixels at all, just discard it
				if (newWidth == width)
					return null;

				newWidth = width - newWidth;
				r = new byte[height, newWidth];

				for (int j = 0; j < height; j++)
				{
					for (int i = 0; i < newWidth; i++)
					{
						r[j, i] = old[j, i + width - newWidth];
					}
				}
			}
			else
				throw new Exception("invalid argument for TrimExtension " + side);

			return r;
		}

		byte[,] CombineArrays(byte[,] a, byte[,] b)
		{
			if (a.GetLength(0) != b.GetLength(0))
				throw new Exception("Trying to combine two arrays that don't have the same height");

			byte[,] r = new byte[a.GetLength(0), a.GetLength(1) + b.GetLength(1)];

			for (int j = 0; j < a.GetLength(0); j++)
			{
				for (int i = 0; i < a.GetLength(1); i++)
					r[j, i] = a[j, i];
				for (int i = 0; i < b.GetLength(1); i++)
					r[j, i + a.GetLength(1)] = b[j, i];
			}

			return r;
		}

		public SubtitleLetter ExtractLetter(Rectangle rect, double angle, Space lastSpace, Space nextSpace)
		{
			/*if (angle != 0.0)
				throw new Exception("angle");
			*/

			byte[,] subArray = new byte[rect.Height, rect.Width];

			for (int j = 0; j < rect.Height; j++)
				for (int i = 0; i < rect.Width; i++)
					subArray[j, i] = subtitleArray[rect.Top + j, rect.Left + i];

			// Cut off some parts
			// fixme check these formulas
			// topright = avp, title 680
			// bottomright = avp, title 713
			// bottomleft = avp, title 642
			if (lastSpace.Type == Space.SpaceType.TopRight)
				for (int j = 0; j < lastSpace.SlopeStart; j++)
				{
					//Debugger.Print();
					//Debugger.Print("l = " + j);
					//Debugger.Print("a = " + ((int)((lastSpace.SlopeStart - j) * lastSpace.Angle)));
					//Debugger.Print("b = " + ((int)((lastSpace.SlopeStart - j) * lastSpace.Angle) + 1));
					//Debugger.Print("c = " + ((lastSpace.SlopeStart - j) * lastSpace.Angle));
					//Debugger.Print("d = " + (int)Math.Round((lastSpace.SlopeStart - j) * lastSpace.Angle));
					for (int i = 0; i < (int)((lastSpace.SlopeStart - j) * lastSpace.Angle) + 1 && i < rect.Width; i++)
						subArray[j, i] = 0;
				}
			if (lastSpace.Type == Space.SpaceType.BottomRight)
				for (int j = lastSpace.SlopeStart; j < rect.Height; j++)
					for (int i = 0; i < (int)((j - lastSpace.SlopeStart) * -lastSpace.Angle) + 1 && i < rect.Width; i++)
						subArray[j, i] = 0;
			if (nextSpace.Type == Space.SpaceType.TopLeft)
				for (int j = 0; j < nextSpace.SlopeStart; j++)
					for (int i = 0; i < (int)((nextSpace.SlopeStart - j) * -nextSpace.Angle) + 1 && i < rect.Width; i++)
						subArray[j, rect.Width - i - 1] = 0;
			if (nextSpace.Type == Space.SpaceType.BottomLeft)
				for (int j = nextSpace.SlopeStart; j < rect.Height; j++)
					for (int i = 0; i < (int)((j - nextSpace.SlopeStart) * nextSpace.Angle) + 1 && i < rect.Width; i++)
						subArray[j, rect.Width - i - 1] = 0;

			// The following functions all extend the letter by a small triangle if one of their adjactant
			// spaces are sloped
			if (nextSpace.Type == Space.SpaceType.TopRight)
			{
				int newWidth = Math.Min(width - rect.Right, (int)(nextSpace.SlopeStart * nextSpace.Angle));

				byte[,] extension = new byte[rect.Height, newWidth];
				for (int j = 0; j < nextSpace.SlopeStart; j++)
					for (int i = 0; i < (int)Math.Round((nextSpace.SlopeStart - j) * nextSpace.Angle) && i < newWidth; i++)
						extension[j, i] = subtitleArray[rect.Top + j, rect.Right + i];

				extension = TrimExtension(extension, Side.Right);
				if (extension != null)
				{
					subArray = CombineArrays(subArray, extension);
					rect.Width += extension.GetLength(1);
				}
			}
			if (nextSpace.Type == Space.SpaceType.BottomRight)
			{
				int newWidth = Math.Min(width - rect.Right, (int)((rect.Height - nextSpace.SlopeStart) * -nextSpace.Angle) + 1);

				byte[,] extension = new byte[rect.Height, newWidth];
				for (int j = nextSpace.SlopeStart; j < rect.Height; j++)
					for (int i = 0; i < (int)Math.Round((nextSpace.SlopeStart - j) * nextSpace.Angle) && i < newWidth; i++) // fixme maybe add a + 1 after the nextSpace.Angle)
						extension[j, i] = subtitleArray[rect.Top + j, rect.Right + i];

				extension = TrimExtension(extension, Side.Right);
				if (extension != null)
				{
					subArray = CombineArrays(subArray, extension);
					rect.Width += extension.GetLength(1);
				}
			}
			if (lastSpace.Type == Space.SpaceType.TopLeft)
			{
				int newWidth = Math.Min(rect.Left, (int)(lastSpace.SlopeStart * -lastSpace.Angle) + 1);

				byte[,] extension = new byte[rect.Height, newWidth];
				for (int j = 0; j < nextSpace.SlopeStart; j++)
					for (int i = 0; i < (int)Math.Round(newWidth + (lastSpace.SlopeStart - j) * lastSpace.Angle) && i < newWidth; i++)
						extension[j, i] = subtitleArray[rect.Top + j, rect.Left - newWidth + i];

				extension = TrimExtension(extension, Side.Left);
				if (extension != null)
				{
					subArray = CombineArrays(extension, subArray);
					rect.Width += extension.GetLength(1);
					rect.X -= extension.GetLength(1);
				}
			}
			if (lastSpace.Type == Space.SpaceType.BottomLeft)
			{
				int newWidth = Math.Min(rect.Left, (int)((rect.Height - lastSpace.SlopeStart) * lastSpace.Angle) + 1);

				byte[,] extension = new byte[rect.Height, newWidth];
				for (int j = lastSpace.SlopeStart; j < rect.Height; j++)
					for (int i = 1; i <= (int)Math.Round((j - lastSpace.SlopeStart) * lastSpace.Angle) && i < newWidth; i++)
						extension[j, newWidth - i] = subtitleArray[rect.Top + j, rect.Left - i];

				extension = TrimExtension(extension, Side.Left);
				if (extension != null)
				{
					subArray = CombineArrays(extension, subArray);
					rect.Width += extension.GetLength(1);
					rect.X -= extension.GetLength(1);
				}
			}


			// Find the top and bottom border of this letter
			int y = 0;
			while (y < rect.Height && !LineContainsPixels(subArray, y, 0, rect.Width))
				y++;

			if (y == rect.Height) // In this case, there are only empty pixels in this space, so return an empty letter
				return null;

			int yStart = y;
			y = rect.Height - 1;
			while (!LineContainsPixels(subArray, y, 0, rect.Width))
				y--;
			int yEnd = y + 1;

			byte[,] r = new byte[yEnd - yStart, subArray.GetLength(1)];
			for (int j = 0; j < yEnd - yStart; j++)
				for (int i = 0; i < subArray.GetLength(1); i++)
					r[j, i] = subArray[j + yStart, i];

			//Debugger.Draw2DArray(r);

			rect.Y += yStart;
			rect.Height -= yStart - yEnd + rect.Height;
			
			return new SubtitleLetter(r, rect, angle);
		}

		public Bitmap GetSubtitlePart(Rectangle rect)
		{
			Bitmap r = new Bitmap(rect.Width + 6, rect.Height + 6);
			Graphics g = Graphics.FromImage(r);
			g.FillRectangle(new SolidBrush(Color.Black), 0, 0, r.Width, r.Height);
			g.DrawImage(subtitleBitmap, 3, 3, rect, GraphicsUnit.Pixel);
			g.Dispose();

			return r;
		}

		public Bitmap GetBitmap()
		{
			int h = subtitleBitmap.Height;
			int w = subtitleBitmap.Width;

			Bitmap r = new Bitmap(w, h, PixelFormat.Format32bppArgb);
			BitmapData bmpData = r.LockBits(new Rectangle(0, 0, r.Width, r.Height), ImageLockMode.WriteOnly, r.PixelFormat);
			IntPtr ptr = bmpData.Scan0;

			byte[] bitmapBytes = new byte[subtitleBitmap.Width * subtitleBitmap.Height * 4];
			int k = 0;
			for (int j = 0; j < h; j++)
				for (int i = 0; i < w; i++)
				{
					bitmapBytes[k++] = subtitleArray[j, i];
					bitmapBytes[k++] = subtitleArray[j, i];
					bitmapBytes[k++] = subtitleArray[j, i];
					bitmapBytes[k++] = 255;
				}

			Marshal.Copy(bitmapBytes, 0, ptr, bitmapBytes.Length);
			r.UnlockBits(bmpData);

			return r;
		}

		public void FixSpaces()
		{
			bool lastLetterNarrow = false;
			LinkedList<SubtitleLetter> toDelete = new LinkedList<SubtitleLetter>();

			SubtitleLetter lastLetter = null;
			foreach (SubtitleLetter l in letters)
			{
				// If this is a space that is immediately following a 1, then apply more stringent rules for deciding whether this is actually a space
				if (l.Text == " ")
				{
					if (lastLetterNarrow && l.Coords.Width + 6 < AppOptions.minimumSpaceCharacterWidth * 3 / 2 + 3)
						toDelete.AddLast(l);
				}

				if (AppOptions.IsNarrowCharacter(l.Text))
				{
					// Check if we are immediately following a qualifying space
					if (lastLetter != null && lastLetter.Text == " " && lastLetter.Coords.Width + 6 < AppOptions.minimumSpaceCharacterWidth * 3 / 2 + 3)
						toDelete.AddLast(lastLetter);

					lastLetterNarrow = true;
				}
				else
					lastLetterNarrow = false;

				lastLetter = l;
			}

			foreach (SubtitleLetter l in toDelete)
				letters.Remove(l);
		}

		public void ImageOCR(SubtitleFonts fonts, bool reportUnknownCharacter)
		{
			if (letters == null)
			{
				// We haven't scanned for letters yet, this probably means I'm debugging something as this shouldn't normally happen
				return;
			}

			fonts.debugStrings.Clear();
			// First try to scan all the letters we can
			foreach (SubtitleLetter l in letters)
			{
				if (l.Text == null)
				{
					// This letter is unknown, try to scan it
					DateTime t = DateTime.Now;
					SubtitleLetter l2 = fonts.FindMatch(l, AppOptions.similarityTolerance * 100);
					Debugger.scanTime += (DateTime.Now - t).TotalMilliseconds;

					if (l2 != null)
					{
						//Debugger.Print("    found letter " + l2.Text);
						if (AppOptions.replaceHighCommas && l.Height < -10 && l2.Text.Equals(","))
							l.Text = "'";
						else if (AppOptions.replaceHighCommas && l.Height > 10 && l2.Text.Equals("'"))
							l.Text = ",";
						else
							l.Text = l2.Text;
					}
					// If we should report unknown characters, then throw an exception, otherwise just skip it
					else if (reportUnknownCharacter)
						throw new UnknownCharacterException();
				}
			}
		}
	}
}
