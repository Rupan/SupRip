using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SupRip
{
	public class SubtitleFrame
	{
		public long[] bitmapStarts;
		public long[] bitmapLengths;
		public byte[] bitmapArray;
		public Rectangle bitmapPos;
		private ulong bitmapHash;

		public SubtitleFrame()
		{
			bitmapStarts = new long[2];
			bitmapLengths = new long[2];
			bitmapPos = new Rectangle();
		}

		/// <summary>
		/// Copy constructor
		/// </summary>
		/// <param name="other">source to copy from</param>
		public SubtitleFrame(SubtitleFrame other)
		{
			bitmapStarts = new long[2];
			bitmapLengths = new long[2];
			for (int i = 0; i < 2; i++)
			{
				bitmapStarts[i] = other.bitmapStarts[i];
				bitmapLengths[i] = other.bitmapStarts[i];
			}

			bitmapPos = new Rectangle(other.bitmapPos.Location, other.bitmapPos.Size);
		}

		public ulong BitmapHash
		{
			get
			{
				return bitmapHash;
			}
		}

		public void SetBitmapHash()
		{
			bitmapHash = 0;

			for (int i = 0; i < bitmapArray.Length; i++)
				bitmapHash = bitmapArray[i] + (bitmapHash << 6) + (bitmapHash << 16) - bitmapHash;
		}

		public void ReadIntoMemory(FileStream fs)
		{
			bitmapArray = new byte[bitmapLengths[0] + bitmapLengths[1]];

			fs.Position = bitmapStarts[0];
			fs.Read(bitmapArray, 0, (int)bitmapLengths[0]);
			fs.Position = bitmapStarts[1];
			fs.Read(bitmapArray, (int)bitmapLengths[0], (int)bitmapLengths[1]);
		}

		public bool CompareBitmaps(SubtitleFrame other)
		{
			if (BitmapHash == 0)
				SetBitmapHash();

			if (other.BitmapHash == 0)
				other.SetBitmapHash();

			return BitmapHash == other.BitmapHash;
		}

	}
}
