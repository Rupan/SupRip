using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SupRip
{
	class SubtitleCaption
	{
        public class OcrWord
        {
            public int rep;
            public string text;

            public OcrWord(int r, string t)
            {
                rep = r;
                text = t;
            }

            public override string ToString()
            {
				return rep + " " + text;
            }
        }

        List<OcrWord> ocrWords;

		public long startTime, endTime;

		private bool forced;
		private bool scanned;
		private int index, addAfter;
		private string startString, endString;
		private string srtText;
		private string text;
		public int numFrames, paletteEntries;

        public SubtitleImage image;

		public SubtitleFrame[] frames;

		public bool emptySubtitle;

		public byte[] hdTransparency;
		public byte[,] hdColorSet;

		public SubtitleCaption()
		{
			frames = new SubtitleFrame[2];

			hdColorSet = new byte[256, 3];
			hdTransparency = new byte[256];

			frames[0] = new SubtitleFrame();
			frames[1] = new SubtitleFrame();
		}

		public SubtitleCaption(SubtitleCaption other, int n, int parentIndex) : this()
		{
			frames[0] = new SubtitleFrame(other.frames[1]);

			startTime = other.startTime;
			endTime = other.endTime;
			forced = other.forced;
			scanned = other.scanned;
			paletteEntries = other.paletteEntries;
			addAfter = parentIndex;

			numFrames = 1;
			emptySubtitle = other.emptySubtitle;
			Array.Copy(other.hdTransparency, hdTransparency, other.hdTransparency.Length);
			for (int j = 0; j < 256; j++)
				for (int i = 0; i < 3; i++)
					hdColorSet[j, i] = other.hdColorSet[j, i];
		}

		public string Start
		{
			get { return startString; }
		}

		public bool Forced
		{
			get { return forced; }
			set { forced = value; }
		}

		public string End
		{
			get { return endString; }
		}

		public string Text
		{
			get { return text; }
			set { text = value; }
		}

		public int Index
		{
			get { return index; }
			set { index = value; }
		}

		public int AddAfter
		{
			get { return addAfter; }
		}

		public string SRTText
		{
			get { return srtText; }
		}

		public bool Scanned
		{
			get { return scanned; }
		}

		private void UpdateTimeStrings()
		{
			long st = startTime - AppOptions.ptsOffset;
			long en = endTime - AppOptions.ptsOffset;
			long ms, s, m, h;
			ms = st % 1000;
			s = (st / 1000) % 60;
			m = (st / 60000) % 60;
			h = (st / 3600000) % 60;
			startString = String.Format("{0:00}:{1:00}:{2:00},{3:000}", h, m, s, ms);
			ms = en % 1000;
			s = (en / 1000) % 60;
			m = (en / 60000) % 60;
			h = (en / 3600000) % 60;
			endString = String.Format("{0:00}:{1:00}:{2:00},{3:000}", h, m, s, ms);
		}

		private int val;
		private byte pos = 4;
		private int Read2Bits(Stream fs)
		{
			int shift;

			if (pos >= 3)
			{
				pos = 0;
				val = fs.ReadByte();
			}
			else
				pos++;

			shift = 2 * (3 - pos);
			return (val & 0x3 << shift) >> shift;
		}

		private void Read2Bits(bool flush)
		{
			if (flush)
				pos = 4;
		}
		
		public Bitmap GetBitmap()
		{
			int w = frames[0].bitmapPos.Width;
			int h = frames[0].bitmapPos.Height;
			byte[] bitmapBytes = new byte[w * h * 4];

			//long eoData = data.frames[n].bitmapStarts[0, 0] + data.frames[n].bitmapLengths[0, 0];
			bool eol = false;
			int x = 0, y = 0;
			int color;
			int stride = w * 4;

			MemoryStream ms = new MemoryStream(frames[0].bitmapArray, false);

			int runLength = 0;
			int rlCode;
			int switches;

			while (y < h)
			{
				byte b1 = (byte)ms.ReadByte();
				if (b1 != 0)
				{
					color = b1;
					runLength = 1;
				}
				else
				{
					switches = Read2Bits(ms);
					if ((switches & 0x02) == 0)
					{
						// If the first bit is a zero we have a lot of zero pixels ahead of us
						color = 0;
						if ((switches & 0x01) == 0)
						{
							rlCode = 0;
							rlCode = (rlCode << 2) + Read2Bits(ms);
							rlCode = (rlCode << 2) + Read2Bits(ms);
							rlCode = (rlCode << 2) + Read2Bits(ms);

							if (rlCode == 0)
								eol = true;
							else
								runLength = rlCode;
						}
						else
						{
							rlCode = 0;
							rlCode = (rlCode << 2) + Read2Bits(ms);
							rlCode = (rlCode << 2) + Read2Bits(ms);
							rlCode = (rlCode << 2) + Read2Bits(ms);
							rlCode = (rlCode << 8) + ms.ReadByte();
							runLength = rlCode;
						}
					}
					else
					{
						if ((switches & 0x01) == 0)
						{
							rlCode = 0;
							rlCode = (rlCode << 2) + Read2Bits(ms);
							rlCode = (rlCode << 2) + Read2Bits(ms);
							rlCode = (rlCode << 2) + Read2Bits(ms);
							runLength = rlCode;
						}
						else
						{
							rlCode = 0;
							rlCode = (rlCode << 2) + Read2Bits(ms);
							rlCode = (rlCode << 2) + Read2Bits(ms);
							rlCode = (rlCode << 2) + Read2Bits(ms);
							rlCode = (rlCode << 8) + ms.ReadByte();
							runLength = rlCode;
						}
						color = ms.ReadByte();
					}
				}

				if (!eol)
				{
					int xe = x + runLength;

					if (xe > w)
						throw new SUPFileFormatException("Line longer than the width of the subtitle");
					for (; x < xe; x++)
					{
						bitmapBytes[y * stride + x * 4] = hdColorSet[color, 0];
						bitmapBytes[y * stride + x * 4 + 1] = hdColorSet[color, 1];
						bitmapBytes[y * stride + x * 4 + 2] = hdColorSet[color, 2];
						bitmapBytes[y * stride + x * 4 + 3] = (byte)hdTransparency[color];
					}
				}
				else
				{
					if (x < w)
					{
						//throw new Exception("unfinished line");
						for (; x < w; x++)
						{
							bitmapBytes[y * stride + x * 4] = hdColorSet[0, 0];
							bitmapBytes[y * stride + x * 4 + 1] = hdColorSet[0, 1];
							bitmapBytes[y * stride + x * 4 + 2] = hdColorSet[0, 2];
							bitmapBytes[y * stride + x * 4 + 3] = (byte)hdTransparency[0];
						}
					}

					eol = false;
					x = 0;
					y++;
				}

			}

			/*for (x=0; x < 140; x++)
			{
				bitmapBytes[131 * stride + x * 4] = 255;
				bitmapBytes[131 * stride + x * 4 + 1] = 255;
				bitmapBytes[131 * stride + x * 4 + 2] = 0;
				bitmapBytes[131 * stride + x * 4 + 3] = 255;
			}*/
			/*
			StringBuilder sb = new StringBuilder(2000);
			for (int j = 0; j < h; j++)
			{
				sb.Append(j.ToString());
				for (int i = 0; i < w; i++)
				{
					int v = bitmapBytes[j * stride + i * 4] + bitmapBytes[j * stride + i * 4 + 1] + bitmapBytes[j * stride + i * 4 + 2];
					v = v * bitmapBytes[j * stride + i * 4 + 3];
					v = v / 256;
					if (v > 200)
						sb.Append('#');
					else
						sb.Append(' ');
				}
				sb.Append("\n");
			}
			Debugger.Print(sb.ToString());
			*/

			Bitmap bitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb);
			BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, bitmap.PixelFormat);
			IntPtr ptr = bmpData.Scan0;
			Marshal.Copy(bitmapBytes, 0, ptr, bitmapBytes.Length);
			bitmap.UnlockBits(bmpData);

            // Draw the transparent subtitle on a black background
            Bitmap subtitleBitmap = new Bitmap(bitmap.Width + 20, bitmap.Height, PixelFormat.Format32bppArgb);
            Graphics g = Graphics.FromImage(subtitleBitmap);
            g.FillRectangle(new SolidBrush(Color.Black), 0, 0, bitmap.Width + 20, bitmap.Height);
            g.DrawImage(bitmap, new Point(10, 0));
            g.Dispose();

            return subtitleBitmap;
		}

		public void CalculatePaletteVariance()
		{
			/*
			int[] sum = new int[3];
			sum[0] = sum[1] = sum[2] = 0;

			for (int i = 0; i < paletteEntries; i++)
			{
				sum[0] += hdColorSet[i, 0];
				sum[1] += hdColorSet[i, 1];
				sum[2] += hdColorSet[i, 2];
			}

			sum[0] /= paletteEntries;
			sum[1] /= paletteEntries;
			sum[2] /= paletteEntries;

			Debugger.Print(sum[0] + " " + sum[1] + " " + sum[2]);
			 * */
			int sum = 0;

			for (int i = 0; i < paletteEntries; i++)
			{
				sum += hdTransparency[i];
			}

			sum /= paletteEntries;

			Debugger.Print(sum);
		}

		public void UpdateSRTText()
		{
			UpdateTimeStrings();

			if (text != null)
			{
				if (AppOptions.preservePositions && frames[0].bitmapPos.Top < 300)
					srtText = "{\an8}" + text;
				else
					srtText = text;

				scanned = true;
			}
			else
				srtText = "Line " + index;
		}

		/*
        public SubtitleImage GetImage()
        {
			Bitmap b = GetBitmap();
			Debugger.Print(Scan());
			return new SubtitleImage(GetBitmap());
        }*/

        public string Scan()
		{
            string bitmapName = Path.GetTempPath() + "suprip_temp.png";
			MODI.Document md = new MODI.Document();
			GetBitmap().Save(bitmapName);

			md.Create(bitmapName);

			md.OCR(MODI.MiLANGUAGES.miLANG_ENGLISH, true, true);

			MODI.Image image = (MODI.Image)md.Images[0];
			MODI.Layout layout = image.Layout;

			string scanned = "";

            ocrWords = new List<OcrWord>();
			for (int j = 0; j < layout.Words.Count; j++)
			{
				// Get this word and deal with it.
				MODI.Word word = (MODI.Word)layout.Words[j];

                OcrWord w = new OcrWord(word.RecognitionConfidence, word.Text);
                ocrWords.Add(w);

				string text = word.Text;
				scanned += text + " ";
				int rc = word.RecognitionConfidence;
			}
			md.Close(false);

			return scanned;
		}
	}
}
