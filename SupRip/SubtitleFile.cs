using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Diagnostics;

namespace SupRip
{
	class SRTInfo
	{
		public int unscanned, finished, containingErrors;

		public SRTInfo()
		{
			unscanned = 0;
			finished = 0;
			containingErrors = 0;
		}
	}

	class SubtitleFile
	{
		FileStream supFileStream;
		List<SubtitleCaption> captions;

		public SubtitleFile(string fileName)
		{
            FileStream fs = new FileStream(fileName, FileMode.Open);

            char p = (char)fs.ReadByte();
            fs.Position = 0;

            if (p == 'P')
                LoadBluraySup(fs);
            else
                throw new SUPFileFormatException("HD DVD Sup");
        }

		public void RecalculateTimes()
		{
			foreach (SubtitleCaption d in captions)
				d.UpdateSRTText();
		}

		private void LoadBluraySup(FileStream fs)
		{
			bool track = false;

			captions = new List<SubtitleCaption>();

			byte[] b = new byte[4];
			int dataLength;
			bool subtitleFinished;
			SubtitleCaption caption = new SubtitleCaption(), lastData = null;

            int numBlocks = 0, frameIndex = 0;
			int[] bitmapPartIndex = new int[2];

            if (track) Debugger.Print("# New Subtitle");

            while (fs.ReadByte() == 'P' && fs.ReadByte() == 'G')
			{
				uint pts1 = BigEndianInt32(ref fs) / 90;
				uint pts2 = BigEndianInt32(ref fs) / 90;

				int controlType = fs.ReadByte();

				subtitleFinished = false;
				if (track) Debugger.Print("  => Block: type = " + String.Format("{0:X}", controlType));
                switch (controlType)
				{
					case 0x16:
						// PTS
						dataLength = BigEndianInt16(ref fs);
						//if (dataLength != 0x13)
						//	throw new Exception("strange dataLength = " + dataLength);
						caption.startTime = pts1;
						int swidth = BigEndianInt16(ref fs);
						int sheight = BigEndianInt16(ref fs);
						fs.ReadByte(); // Frame Rate

						BigEndianInt16(ref fs); // subtitleIndex
						fs.Position += 3; // Unknown

						int numInfoBlocks = fs.ReadByte();

						if (numInfoBlocks * 8 + 11 != dataLength)
							throw new Exception("Mismatched Info Blocks: Datalength = " + dataLength + ", numBlocks = " + numInfoBlocks);

						if (track) Debugger.Print("       PTS : num = " + numInfoBlocks);
						if (track) Debugger.Print("         time = " + (caption.startTime / 1000) + "s");

						if (numInfoBlocks == 1)
						{
							fs.Position += 3; // Unknown
							caption.Forced = (fs.ReadByte() & 64) != 0;

							if (track) Debugger.Print("         forced = " + (caption.Forced ? "yes" : "no"));

							int x1 = BigEndianInt16(ref fs);
							int y1 = BigEndianInt16(ref fs);
						}
						else if (numInfoBlocks == 2)
						{
							if (track) Debugger.Print("        Secondary Title");

							fs.Position += 3; // Unknown
							int forced2 = fs.ReadByte();
							caption.Forced = (forced2 & 64) != 0;

							if (track) Debugger.Print("       forced1 = " + (caption.Forced ? "yes" : "no"));

							int x1 = BigEndianInt16(ref fs);
							int y1 = BigEndianInt16(ref fs);

							fs.Position += 3; // Unknown
							int forced3 = fs.ReadByte();
							caption.Forced = (forced2 & 64) != 0;

							if (track) Debugger.Print("       forced2 = " + (caption.Forced ? "yes" : "no"));

							int x2 = BigEndianInt16(ref fs);
							int y2 = BigEndianInt16(ref fs);
						}
						break;
					case 0x17:
						// Window definition
						if (track) Debugger.Print("    Window");
						
						dataLength = BigEndianInt16(ref fs);
                        caption.numFrames = fs.ReadByte();
						if (caption.numFrames != 1 && caption.numFrames != 2)
							throw new SUPFileFormatException("Number of SUP Window descriptions = " + caption.numFrames);
						if (track) Debugger.Print("     window data length = " + caption.numFrames);

                        // We skip over the id as we don't really care about it
						int sizeId = fs.ReadByte();
                        // And read the position and width of the subtitle
                        caption.frames[0].bitmapPos.X = BigEndianInt16(ref fs);
						caption.frames[0].bitmapPos.Y = BigEndianInt16(ref fs);
						caption.frames[0].bitmapPos.Width = BigEndianInt16(ref fs);
						caption.frames[0].bitmapPos.Height = BigEndianInt16(ref fs);
						if (track) Debugger.Print("       x = " + caption.frames[0].bitmapPos.X + ", y = " + caption.frames[0].bitmapPos.Y);
						if (track) Debugger.Print("       w = " + caption.frames[0].bitmapPos.Width + ", h = " + caption.frames[0].bitmapPos.Height);

						// If there are two window definitions, we get the second one too
						if (caption.numFrames == 2)
						{
							fs.Position++;
							caption.frames[1].bitmapPos.X = BigEndianInt16(ref fs);
							caption.frames[1].bitmapPos.Y = BigEndianInt16(ref fs);
							caption.frames[1].bitmapPos.Width = BigEndianInt16(ref fs);
							caption.frames[1].bitmapPos.Height = BigEndianInt16(ref fs);
							if (track) Debugger.Print("       x2 = " + caption.frames[1].bitmapPos.X + ", y2 = " + caption.frames[1].bitmapPos.Y);
							if (track) Debugger.Print("       w2 = " + caption.frames[1].bitmapPos.Width + ", h2 = " + caption.frames[1].bitmapPos.Height);
						}
						break;
					case 0x14:
						// Palette definition
						dataLength = BigEndianInt16(ref fs);
						if (dataLength == 2)
						{
							BigEndianInt16(ref fs);
							caption.emptySubtitle = true;
							break;
						}
						if (dataLength % 5 != 2)
							throw new SUPFileFormatException("Palette length is not divisible by 3");
						caption.paletteEntries = (dataLength - 2) / 5;
						if (track) Debugger.Print("     Palette: " + caption.paletteEntries + " entries");

						int dontKnow = BigEndianInt16(ref fs);
						//if (dontKnow > 7 || dontKnow < 0)
						//	throw new Exception("palette unknown == " + dontKnow);
						//fs.Position += dataLength;
						for (int i = 0; i < caption.paletteEntries; i++)
						{
							int index = fs.ReadByte();
							int y = fs.ReadByte() - 16;
							int cr = fs.ReadByte() - 128;
							int cb = fs.ReadByte() - 128;

							//int rgb = (((int)Math.Min(Math.Max(Math.Round(1.1644F * y + 1.596F * cr), 0), 255)) << 16) +
							//			(((int)Math.Min(Math.Max(Math.Round(1.1644F * y - 0.813F * cr - 0.391F * cb), 0), 255)) << 8) +
							//			(int)Math.Min(Math.Max(Math.Round(1.1644F * y + 2.018F * cb), 0), 255);

							caption.hdColorSet[index, 0] = (byte)Math.Min(Math.Max(Math.Round(1.1644F * y + 1.596F * cr), 0), 255);
							caption.hdColorSet[index, 1] = (byte)Math.Min(Math.Max(Math.Round(1.1644F * y - 0.813F * cr - 0.391F * cb), 0), 255);
							caption.hdColorSet[index, 2] = (byte)Math.Min(Math.Max(Math.Round(1.1644F * y + 2.018F * cb), 0), 255);
							caption.hdTransparency[index] = (byte)fs.ReadByte();
						}
						caption.CalculatePaletteVariance();
						break;
					case 0x15:
						dataLength = BigEndianInt16(ref fs);
						int dummy1 = BigEndianInt16(ref fs);
						int dummy2 = fs.ReadByte(); // This is usually 0, very rarely 1
						int continuation = fs.ReadByte();

						if (track) Debugger.Print("       continuation = " + String.Format("{0:x}", continuation));
						if ((continuation & 0xc0) == 0x00)
							throw new Exception("cont = " + continuation);

						// continuation == 0x80 -> this is the first pointer
						// continuation == 0xc0 -> this is the first and simultaneously the last pointer, aka the only one
						if ((continuation & 0x80) != 0)
						{
							int totalLength = BigEndianInt24(ref fs);
							if (continuation == 0xc0 && dataLength - totalLength != 7)
								throw new Exception("single block, but unexpected difference in data block lengths = " + (dataLength - totalLength));
							int currentWidth = BigEndianInt16(ref fs);
							int currentHeight = BigEndianInt16(ref fs);
							if (track) Debugger.Print("       w = " + currentWidth + ", h = " + currentHeight);

							if (track) Debugger.Print("       Bitmap 1");
							if (track) Debugger.Print("       start = " + String.Format("{0:X}", fs.Position) + ", length = " + dataLength);

							if (caption.frames[frameIndex].bitmapStarts[bitmapPartIndex[frameIndex]] != 0)
								throw new Exception("Unexpected data on multipart subtitle, cont = " + continuation);

							// Occasionally the width stored in this field is different from the one stored in the window definition
							// I suspect this is to blank out the screen. In any case, the width stored here is what we're interested in, this is the one of the bitmap
							// actually stored in the subtitle file
							caption.frames[frameIndex].bitmapPos.Width = currentWidth;
							caption.frames[frameIndex].bitmapPos.Height = currentHeight;
							caption.frames[frameIndex].bitmapStarts[bitmapPartIndex[frameIndex]] = fs.Position;
							caption.frames[frameIndex].bitmapLengths[bitmapPartIndex[frameIndex]] = dataLength - 11;
							
							fs.Position += dataLength - 11;
						}
						// continuation == 0x40 -> this is the last pointer
						else if ((continuation & 0x40) != 0)
						{
							if (track) Debugger.Print("      Bitmap 2");
							if (track) Debugger.Print("       start = " + String.Format("{0:X}", fs.Position) + ", length = " + dataLength);

							if (caption.frames[frameIndex].bitmapStarts[1] != 0)
								throw new Exception("Three part Bluray bitmapdata, didn't think those would exist");

							if (caption.frames[frameIndex].bitmapStarts[0] == 0)
								throw new Exception("Unexpected data on second part of multipart subtitle, start = " + caption.frames[frameIndex].bitmapStarts[bitmapPartIndex[frameIndex]]);

							caption.frames[frameIndex].bitmapStarts[bitmapPartIndex[frameIndex]] = fs.Position;
							caption.frames[frameIndex].bitmapLengths[bitmapPartIndex[frameIndex]] = dataLength - 4;

							fs.Position += dataLength - 4;
						}
						else
							throw new Exception("Unexpected continuation value = " + continuation);

						if ((continuation & 0x40) == 0)
							bitmapPartIndex[frameIndex] = 1;
						else
							frameIndex++;

						// Object definition
						break;
					case 0x80:
						if (track) Debugger.Print("     End");
						if (caption.frames[0].bitmapStarts[0] == 0)
							caption.emptySubtitle = true;

						dataLength = BigEndianInt16(ref fs);
						if (dataLength != 0)
							throw new Exception("code 80, length != 0");
						subtitleFinished = true;
						break;
					default:
						throw new Exception("unknown code");
				}

				if (subtitleFinished)
				{
					if (caption.emptySubtitle)
					{
						// If the subtitle block doesn't contain any bitmap data, it's just the end mark
						if (track) Debugger.Print("  - Auto adding end time to last subtitle");
						if (lastData != null)
							lastData.endTime = caption.startTime - 1;
					}
					else
					{
						if (lastData != null && lastData.endTime == 0)
						{
							lastData.endTime = caption.startTime - 1;
							if (track) Debugger.Print("+ Last title doesnt have end time");
						}

						//data.bitmap = GetBlurayBitmap(fs, data);
						captions.Add(caption);
						lastData = caption;
						if (track) Debugger.Print("# Subtitle Finished");
					}

					caption = new SubtitleCaption();
                    if (track) Debugger.Print("# New Subtitle");
					bitmapPartIndex[0] = bitmapPartIndex[1] = 0;
					frameIndex = 0;
				}

                numBlocks++;
			}


			// If we're still at the beginning of the file, it probably wasn't a Bluray SUP
			if (fs.Position == 1)
			{
				fs.Close();
				throw new SUPFileFormatException("completely empty file");
			}

			// FIXME add some code for subtitle positioning
			/*foreach (SupData d in supDatas)
			{
				Debugger.Print(d.number + " " + d.bitmapPos.Y);
			}*/

			// FIXME This is probably buggy
			if (caption.frames[0].bitmapStarts[0] == 0 && caption.startTime != 0 && lastData != null)
				lastData.endTime = caption.startTime - 1;

            Debugger.Print("Num Blocks = " + numBlocks);

			supFileStream = fs;

			// If there's two subtitle frames in one block, we have to split them into two blocks
			FillSupDataIndices();
			List<SubtitleCaption> toAdd = new List<SubtitleCaption>();
			foreach (SubtitleCaption d in captions)
			{
				if (d.numFrames > 1 && d.frames[1].bitmapStarts[0] != 0)
				{
					SubtitleCaption d2 = new SubtitleCaption(d, 1, d.Index);
					toAdd.Add(d2);
					d.frames[1].bitmapStarts[0] = 0;
					d.numFrames = 1;
				}
			}
			int displacement = 0;
			foreach (SubtitleCaption d in toAdd)
			{
				captions.Insert(d.AddAfter + displacement + 1, d);
				displacement++;
			}
			FillSupDataIndices();

			if (AppOptions.combineSubtitles)
				CleanupDuplicateSubtitles();

			FillSupDataIndices();

			foreach (SubtitleCaption d in captions)
			{
				d.frames[0].ReadIntoMemory(supFileStream);
				d.UpdateSRTText();
				Debugger.Print("loc: " + d.frames[0].bitmapPos.Top);
			}

			fs.Close();
		}

		private void FillSupDataIndices()
		{
			int i = 0;
			foreach (SubtitleCaption d in captions)
				d.Index = i++;
		}

		/// <summary>
		/// Sometimes subtitles are split into several completely equal images, each a second long, and immediately following each other
		/// This code takes care of that nonsense and instead deletes the duplicates while extending the end time of the first instance
		/// </summary>
		private void CleanupDuplicateSubtitles()
		{
			SubtitleCaption lastData = null;

			LinkedList<SubtitleCaption> toDelete = new LinkedList<SubtitleCaption>();

			foreach (SubtitleCaption d in captions)
			{
				if (lastData != null && lastData.frames[0].bitmapLengths[0] == d.frames[0].bitmapLengths[0] && lastData.frames[0].bitmapLengths[1] == d.frames[0].bitmapLengths[1] &&
								lastData.frames[0].bitmapPos == d.frames[0].bitmapPos && Math.Abs(d.startTime - lastData.endTime) < 100 &&
								d.frames[0].CompareBitmaps(lastData.frames[0]))
				{
					lastData.endTime = d.endTime;
					toDelete.AddLast(d);
				}
				else
					lastData = d;
			}

			foreach (SubtitleCaption d in toDelete)
				captions.Remove(d);
		}

		#region Helper functions for SUP decoding
		private uint LowEndianInt32(ref FileStream fs)
		{
			byte[] b = new byte[4];
			fs.Read(b, 0, 4);

			return (uint)b[0] + ((uint)b[1] << 8) + ((uint)b[2] << 16) + ((uint)b[3] << 24);
		}

		private uint BigEndianInt32(ref FileStream fs)
		{
			byte[] b = new byte[4];
			fs.Read(b, 0, 4);

			return (uint)b[3] + ((uint)b[2] << 8) + ((uint)b[1] << 16) + ((uint)b[0] << 24);
		}

		private int BigEndianInt16(ref FileStream fs)
		{
			byte[] b = new byte[2];
			fs.Read(b, 0, 2);

			return (int)b[1] + ((int)b[0] << 8);
		}

		private int BigEndianInt24(ref FileStream fs)
		{
			byte[] b = new byte[3];
			fs.Read(b, 0, 3);

			return (int)b[2] + ((int)b[1] << 8) + ((int)b[0] << 16);
		}

		private int LowEndianInt16(ref FileStream fs)
		{
			byte[] b = new byte[2];
			fs.Read(b, 0, 2);

			return (int)b[0] + ((int)b[1] << 8);
		}
		#endregion

		/*public string GetFileName(int n)
		{
			return folder + supDatas[n].ImageFileName;
		}*/

        /*public SubtitleImage GetSubtitleImage(int n)
        {
            Bitmap b = captions[n].GetBitmap();

            Debugger.Print(captions[n].Scan());

            return new SubtitleImage(b);
        }
        */
        public SubtitleImage LoadSubtitleImage(int n)
        {
            Bitmap b = captions[n].GetBitmap();

            captions[n].image = new SubtitleImage(b, captions[n]);

            Debugger.Print(captions[n].Scan());

            return captions[n].image;
        }


		public void UpdateSubtitleText(int n, SubtitleImage si)
		{
			captions[n].Text = si.GetText();
			captions[n].UpdateSRTText();
			//lines[n].Text = s;
			//lines[n].UpdateSRTText();
		}

		public string GetSRTText()
		{
			StringBuilder sb = new StringBuilder(10000);

			string lastLine = null, lastStart = null, lastEnd = null;
			int n = 1;
			foreach (SubtitleCaption d in captions)
			{
				String s = d.SRTText;

				if (AppOptions.convertDoubleApostrophes)
					s = s.Replace("''", "\"");
				if (AppOptions.stripFormatting)
					s = s.Replace("<i>", "").Replace("</i>", "");

				if (!AppOptions.forcedOnly || d.Forced)
				{
					if (AppOptions.combineSubtitles)
					{
						if (lastLine != null)
						{
							if (s.CompareTo(lastLine) == 0)
							{
								// This line is identical to the last one, so combine them
								lastEnd = d.End;
							}
							else
							{
								// This line is different from the last one
								// So save the last line, and replace it by the current one
								sb.Append(n + "\r\n");
								sb.Append(lastStart + " --> " + lastEnd + "\r\n");
								sb.Append(lastLine);
								sb.Append("\r\n\r\n");

								n++;

								lastStart = d.Start;
								lastEnd = d.End;
								lastLine = s;
							}
						}
						else
						{
							// This is the first line of the file
							lastStart = d.Start;
							lastEnd = d.End;
							lastLine = s;
						}
					}
					else
					{
						sb.Append(n + "\r\n");
						sb.Append(d.Start + " --> " + d.End + "\r\n");
						sb.Append(s);
						sb.Append("\r\n\r\n");

						n++;
					}

				}
			}

			if (AppOptions.combineSubtitles && lastLine != null)
			{
				// This line is different from the last one
				// So save the last line, and replace it by the current one
				sb.Append(n + "\r\n");
				sb.Append(lastStart + " --> " + lastEnd + "\r\n");
				sb.Append(lastLine);
				sb.Append("\r\n\r\n");
			}

			return sb.ToString();
		}

		public SRTInfo GetSRTInfo()
		{
			SRTInfo r = new SRTInfo();

			foreach (SubtitleCaption d in captions)
			{
				if (!d.Scanned)
					r.unscanned++;
				else if (d.SRTText.Contains("¤"))
					r.containingErrors++;
				else
					r.finished++;
			}

			return r;
		}

		public int NumSubtitles
		{
			get { return captions.Count; }
		}

		
		public void WriteBitmaps(string supName, int numSubtitles, int spacing)
		{
			Bitmap[] workBitmaps = new Bitmap[numSubtitles];

			int j = 0, k = 0;
			while (j < captions.Count)
			{
				// Create the bitmaps from the subtitle file
				// We're not writing them to a collected bitmap yet because we have to find out the total height
				SubtitleImage img = null;
				int maxWidth = 0, totalHeight = 0;
				numSubtitles = Math.Min(numSubtitles, captions.Count - j);
				for (int i = j; i < j + numSubtitles; i++)
				{
					img = new SubtitleImage(captions[i].GetBitmap(), captions[i]);

					workBitmaps[i-j] = img.GetBitmap();
					if (workBitmaps[i - j].Width > maxWidth)
						maxWidth = workBitmaps[i - j].Width;
					totalHeight += workBitmaps[i - j].Height + spacing;
				}

				// Create the final bitmap and write all the earlier created ones to it.
				Bitmap finalBitmap = new Bitmap(maxWidth, totalHeight, PixelFormat.Format32bppArgb);
				Graphics g = Graphics.FromImage(finalBitmap);
				g.FillRectangle(new SolidBrush(Color.Black), 0, 0, finalBitmap.Width, finalBitmap.Height);
				int y = 0;
				for (int i = 0; i < numSubtitles; i++)
				{
					g.DrawImage(workBitmaps[i], 0, y);
					y += workBitmaps[i].Height + spacing;
				}
				int x = supName.LastIndexOf('.');
				string bitmapName = supName.Substring(0, x) + "." + k + ".png";
				finalBitmap.Save(bitmapName);
				
				// Increase the indexes to work on the next bitmap file.
				k++;
				j += numSubtitles;
			}
		}
		
		public bool IsSubtitleForced(int n)
		{
			return captions[n].Forced;
		}

		[Conditional("DEBUG")]
		public void SaveXml(string fileName)
		{
			StreamWriter sw = new StreamWriter(fileName);
			foreach (SubtitleCaption d in captions)
			{
				sw.WriteLine("\t<subpicture>");
				sw.WriteLine("\t\t<start>" + d.startTime.ToString() + "</start>");
				sw.WriteLine("\t\t<end>" + d.endTime.ToString() + "</end>");
				sw.WriteLine("\t\t<forced>" + (d.Forced ? "true" : "false") + "</forced>");
				sw.WriteLine("\t</subpicture>");
			}

			sw.Close();
		}

		public void Close()
		{
			supFileStream.Close();
		}
	}
}
