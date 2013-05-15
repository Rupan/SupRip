using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace SupRip
{
	class Debugger
	{
		private static StreamWriter sw;

		public static double lettersTime, scanTime, diffTime, angleTime, linesTime, widenTime, absDiffTime, spacesTime, translationTime, extractTime;
		public static int translationFound, translationNotFound;

		[Conditional("DEBUG")]
		public static void ResetTimes()
		{
		}

		[Conditional("DEBUG")]
		public static void PrintTimes()
		{
			Print("lettersTime = " + lettersTime.ToString());
			Print("> linesTime = " + linesTime.ToString());
			Print("> angleTime = " + angleTime.ToString());
			Print("> spacesTime = " + spacesTime.ToString());
			Print("> extractTime = " + extractTime.ToString());
			Print("scanTime = " + scanTime.ToString());
			Print("> diffTime = " + diffTime.ToString());
			Print("> > widenTime = " + widenTime.ToString());
			Print("> absDiffTime = " + absDiffTime.ToString());
			Print("> translationTime = " + translationTime.ToString());
			Print("");
			Print("translationFound = " + translationFound.ToString());
			Print("translationNotFound = " + translationNotFound.ToString());
		}

		[Conditional("DEBUG")]
		public static void Print()
		{
			Print("");
		}

		[Conditional("DEBUG")]
		public static void Print(int i)
		{
			Print(i.ToString());
		}

		[Conditional("DEBUG")]
		public static void OpenFile()
		{
			sw = new StreamWriter(new FileStream(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\debug.txt", FileMode.Create));
		}

		[Conditional("DEBUG")]
		public static void Print(string s)
		{
			if (sw == null)
				OpenFile();

			sw.WriteLine(s);
			sw.Flush();
		}

		[Conditional("DEBUG")]
		public static void PrintTimestamped(string s)
		{
			if (sw == null)
				OpenFile();

			DateTime dt = DateTime.Now;
			sw.WriteLine("{1:00}:{2:00}:{3:00},{4:000} {0}", s, dt.Hour, dt.Minute, dt.Second, dt.Millisecond);
			sw.Flush();
		}

		[Conditional("DEBUG")]
		public static void Draw2DArrayo(byte[,] b)
		{
			if (sw == null)
				OpenFile();

			for (int j = 0; j < b.GetLength(0); j++)
			{
				for (int i = 0; i < b.GetLength(1); i++)
				{
					if (b[j, i] > 200)
						sw.Write("@");
					else if (b[j, i] > 10)
						sw.Write("#");
					else if (b[j, i] > 40)
						sw.Write("*");
					else if (b[j, i] > 1)
						sw.Write(".");
					else
						sw.Write(" ");
				}
				sw.WriteLine();
			}

			sw.Flush();
		}

		[Conditional("DEBUG")]
		public static void Draw2DArray(byte[,] b)
		{
			if (sw == null)
				OpenFile();

			for (int j = 0; j < b.GetLength(0); j++)
			{
				for (int i = 0; i < b.GetLength(1); i++)
				{
					sw.Write(String.Format("{0,2:x}", b[j, i]));
				}
				sw.WriteLine();
			}

			sw.Flush();
		}

		public static void CleanUp()
		{
			if (sw != null)
			{
				sw.Close();
				sw.Dispose();
			}
		}

		[Conditional("DEBUG")]
		public static void SaveBitmap(System.Drawing.Bitmap bitmap)
		{
			bitmap.Save("c:\\temp\\temp.png");
		}
	}
}
