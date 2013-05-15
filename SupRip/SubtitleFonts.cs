using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Collections;

namespace SupRip
{
	class SubtitleFonts
	{
		LinkedList<SubtitleFont> fonts;
		SubtitleFont defaultFont;
		SubtitleFont userFont;
		Hashtable fontStats;
		public LinkedList<PositionedString> debugStrings;


		public SubtitleFonts()
		{
			fonts = new LinkedList<SubtitleFont>();

			defaultFont = null;

			fontStats = new Hashtable();

			debugStrings = new LinkedList<PositionedString>();

			DirectoryInfo di = new DirectoryInfo(".");
			FileInfo[] rgFiles = di.GetFiles("*.font.txt");
			foreach(FileInfo fi in rgFiles)
			{
				string fontName = fi.Name;
				fontName = fontName.Substring(0, fontName.LastIndexOf('.'));
				fontName = fontName.Substring(0, fontName.LastIndexOf('.'));
				fonts.AddLast(new SubtitleFont(SubtitleFont.FontType.ProgramFont, fontName));
			}

			try
			{
				userFont = new SubtitleFont(SubtitleFont.FontType.UserFont, "temp");
			}
			catch (FontfileFormatException)
			{
				SubtitleFont.DeleteUserFont("temp");
				userFont = new SubtitleFont(SubtitleFont.FontType.UserFont, "temp");
			}
		}

		/// <summary>
		/// Returns a list of the names of program fonts that are currently loaded
		/// </summary>
		/// <returns>An array of strings containing the names</returns>
		public string[] FontList()
		{
			string[] r = new string[fonts.Count];

			int i = 0;
			foreach (SubtitleFont font in fonts)
				r[i++] = font.Name;

			return r;
		}

		/// <summary>
		/// Saves all the fonts to disk. It skips over the ones that haven't been modified
		/// </summary>
		public void Save()
		{
			foreach (SubtitleFont font in fonts)
			{
				if (font.Changed)
					font.Save();
			}

			if (userFont.Changed)
				userFont.Save();
		}

		public SubtitleLetter FindMatch(SubtitleLetter l, int tolerance)
		{
			SortedList<int, SubtitleLetter> defaultFontResults, combinedResults, nextResults, userResults;

			//Debugger.Print("   L = " + l.Coords.Left + " / " + l.Coords.Top);

			if (defaultFont == null)
				foreach (DictionaryEntry de in fontStats)
					if ((int)de.Value > 10)
						SetDefaultFont((string)de.Key);

			// First, check whether the user font provides a very good match
			userResults = userFont.FindMatch(l, tolerance / 10);
			if (userResults.Count > 0)
				return userResults.Values[0];

			// Then, check whether the default font can scan this letter
			if (defaultFont != null)
			{
				defaultFontResults = defaultFont.FindMatch(l, tolerance);
				if (defaultFontResults.Count > 0)
				{
					debugStrings.AddLast(new PositionedString(l.Coords, defaultFontResults.Keys[0].ToString()));
					return defaultFontResults.Values[0];
				}
			}

			// Next, check the other fonts
			combinedResults = new SortedList<int, SubtitleLetter>();
			foreach (SubtitleFont font in fonts)
			{
				if (font == defaultFont)
					continue;

				nextResults = font.FindMatch(l, tolerance);

				foreach (KeyValuePair<int, SubtitleLetter> kvp in nextResults)
					if (!combinedResults.ContainsKey(kvp.Key))
						combinedResults.Add(kvp.Key, kvp.Value);

				if (nextResults.Count > 0)
				{
					if (fontStats[font.Name] == null)
						fontStats[font.Name] = 1;
					else
						fontStats[font.Name] = (int)fontStats[font.Name] + 1;
				}
			}

			if (combinedResults.Count > 0)
			{
				debugStrings.AddLast(new PositionedString(l.Coords, combinedResults.Keys[0].ToString()));
				return combinedResults.Values[0];
			}

			// We didn't find the letter in the built-in fonts, so let's check the user provided letters
			if (userResults.Count > 0)
				return userResults.Values[0];
			else
				return null;
		}

		/// <summary>
		/// Remembers one of the built in fonts as default font so it gets searched first for subsequent letters
		/// </summary>
		/// <param name="name">The name of the font to set as default</param>
		private void SetDefaultFont(string name)
		{
			foreach (SubtitleFont font in fonts)
			{
				if (font.Name == name)
				{
					defaultFont = font;
					return;
				}
			}

			throw new Exception("Trying to set an unknown font as default");
		}

		public string ListDuplicates()
		{
			return userFont.ListDuplicates();
		}

		public string DefaultFontName
		{
			get { return defaultFont != null ? defaultFont.Name : "-"; }
		}

		public void AddLetter(SubtitleLetter l)
		{
			userFont.AddLetter(l);
		}
		
		public void DeleteLetter(SubtitleLetter l2)
		{
			SubtitleLetter l = FindMatch(l2, AppOptions.similarityTolerance * 100);
			userFont.DeleteLetter(l);
		}

		public void MergeUserFont(string targetFontName)
		{
			SubtitleFont targetFont = null;
			foreach (SubtitleFont font in fonts)
			{
				if (font.Name == targetFontName)
				{
					targetFont = font;
					break;
				}
			}

			if (targetFont == null)
				throw new Exception("invalid font name for merge fonts");

			userFont.MoveLetters(targetFont);
		}

		public int Count
		{
			get { return fonts.Count; }
		}
	}
}
