using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Collections;
using Microsoft.Win32;

namespace SupRip
{
	class AppOptions
	{
		public static int minimumSpaceCharacterWidth;
		public static int charSplitTolerance;
		public static int similarityTolerance;
		public static int contrast;

		public static bool convertDoubleApostrophes;
		public static bool stripFormatting;
		public static bool replaceHighCommas;
		public static bool forcedOnly;
		public static bool preservePositions;
		public static bool combineSubtitles;

		public static bool modiInstalled;

		//public static List<string> easilyConfused = null, narrow = null;
		private static Hashtable easilyConfused = null, narrow = null;

		public static int ptsOffset;

		public AppOptions()
		{
			RegistryKey rk;
			rk = Registry.CurrentUser.OpenSubKey("Software\\SupRip\\");

			try
			{
				minimumSpaceCharacterWidth = (int)rk.GetValue("minimumSpaceCharacterWidth");
			}
			catch (NullReferenceException)
			{
				minimumSpaceCharacterWidth = 12;
			}

			try
			{
				charSplitTolerance = (int)rk.GetValue("charSplitTolerance");
			}
			catch (NullReferenceException)
			{
				charSplitTolerance = 2;
			}

			try
			{
				similarityTolerance = (int)rk.GetValue("similarityTolerance");
			}
			catch (NullReferenceException)
			{
				similarityTolerance = 5;
			}

			try
			{
				contrast = (int)rk.GetValue("contrast");
			}
			catch (NullReferenceException)
			{
				contrast = 0;
			}

			try
			{
				convertDoubleApostrophes = ((string)rk.GetValue("convertDoubleApostrophes")).Equals("True") ? true : false;
			}
			catch (NullReferenceException)
			{
				convertDoubleApostrophes = true;
			}

			try
			{
				stripFormatting = ((string)rk.GetValue("stripFormatting")).Equals("True") ? true : false;
			}
			catch (NullReferenceException)
			{
				stripFormatting = true;
			}

			try
			{
				replaceHighCommas = ((string)rk.GetValue("replaceHighCommas")).Equals("True") ? true : false;
			}
			catch (NullReferenceException)
			{
				replaceHighCommas = true;
			}

			try
			{
				forcedOnly = ((string)rk.GetValue("forcedOnly")).Equals("True") ? true : false;
			}
			catch (NullReferenceException)
			{
				forcedOnly = false;
			}

			try
			{
				preservePositions = ((string)rk.GetValue("preservePositions")).Equals("True") ? true : false;
			}
			catch (NullReferenceException)
			{
				preservePositions = true;
			}

			try
			{
				combineSubtitles = ((string)rk.GetValue("combineSubtitles")).Equals("True") ? true : false;
			}
			catch (NullReferenceException)
			{
				combineSubtitles = true;
			}

			string line;
			StreamReader sr;

			try
			{
				sr = new StreamReader("easilyconfused.txt");
				easilyConfused = new Hashtable();
				while ((line = sr.ReadLine()) != null)
					easilyConfused.Add(line, true);
			}
			catch (FileNotFoundException)
			{
				easilyConfused = null;
			}

			try
			{
				sr = new StreamReader("narrow.txt");
				narrow = new Hashtable();
				while ((line = sr.ReadLine()) != null)
					narrow.Add(line, true);
			}
			catch (FileNotFoundException)
			{
				narrow = null;
			}
		}

		public void SaveOptions()
		{
			RegistryKey rk;
			rk = Registry.CurrentUser.CreateSubKey("Software\\SupRip");

			rk.SetValue("minimumSpaceCharacterWidth", minimumSpaceCharacterWidth);
			rk.SetValue("charSplitTolerance", charSplitTolerance);
			rk.SetValue("similarityTolerance", similarityTolerance);
			rk.SetValue("contrast", contrast);
			rk.SetValue("convertDoubleApostrophes", convertDoubleApostrophes);
			rk.SetValue("stripFormatting", stripFormatting);
			rk.SetValue("replaceHighCommas", replaceHighCommas);
			rk.SetValue("forcedOnly", forcedOnly);
			rk.SetValue("preservePositions", preservePositions);
			rk.SetValue("combineSubtitles", combineSubtitles);
		}

		public void ResetToDefaults()
		{
			minimumSpaceCharacterWidth = 12;
			charSplitTolerance = 2;
			similarityTolerance = 5;
			contrast = 0;
			convertDoubleApostrophes = true;
			stripFormatting = true;
			replaceHighCommas = true;
			forcedOnly = false;
			preservePositions = true;
			combineSubtitles = true;
		}

		public static bool IsEasilyConfusedLetter(string c)
		{
			if (c == null || easilyConfused == null)
				return false;

			if (easilyConfused[c] == null)
				return false;
			else
				return true;
		}

		public static bool IsNarrowCharacter(string c)
		{
			if (c == null || narrow == null)
				return false;

			if (narrow[c] == null)
				return false;
			else
				return true;
		}

	}
}
