using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Reflection;
using System.Threading;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SupRip
{
	public delegate void DelegateUpdateProgress(int x);

	public partial class MainForm : Form
	{
		private Size bitmapSize;
		private Bitmap bitmap;
		private SubtitleImage currentSubtitle;
		private int currentNum, oldNum;
		private SubtitleLetter activeLetter;
		private SubtitleFonts fonts;
		private Rectangle subtitleImageRectangle;
		private SubtitleFile subfile;
        private bool initialized;

		private AppOptions options;

		private double bitmapScale;

		Pen redPen, bluePen, yellowPen, greenPen, whitePen;
		ProgressForm pf;
		ManualResetEvent stopEvent, finishedEvent;
		public DelegateUpdateProgress updateProgressDelegate, updatePrecalculateDelegate;

		public MainForm()
		{
			InitializeComponent();

			Debugger.ResetTimes();

#if !DEBUG
			debugButton.Hide();
#endif

			bitmapSize = new Size(400, 400);
			bitmap = new Bitmap(bitmapSize.Width, bitmapSize.Height, PixelFormat.Format24bppRgb);

			fonts = new SubtitleFonts();

			redPen = new Pen(new SolidBrush(Color.Red));
			yellowPen = new Pen(new SolidBrush(Color.Yellow));
			bluePen = new Pen(new SolidBrush(Color.Blue));
			greenPen = new Pen(new SolidBrush(Color.Green));
			whitePen = new Pen(new SolidBrush(Color.White));

			//SetStyle(ControlStyles.UserPaint, true);
			//SetStyle(ControlStyles.AllPaintingInWmPaint, true);
			//SetStyle(ControlStyles.DoubleBuffer, true);

			subtitlePictureBox.SizeMode = PictureBoxSizeMode.Zoom;
			letterPictureBox.SizeMode = PictureBoxSizeMode.Zoom;

			ToolTip tt = new ToolTip();
			tt.AutoPopDelay = 20000;
			tt.SetToolTip(nextButton, "Moves to the next subtitle image\n\nCtrl+N");
			tt.SetToolTip(previousButton, "Moves to the previous subtitle image\n\nCtrl+P");
			tt.SetToolTip(ocrButton, "Tries to scan the current image, and will prompt the user to identify any unknown character.\n\nCtrl+O");
			tt.SetToolTip(minimumSpaceCharacterWidthTextBox, "Configures how big an empty space between two letters has to be to be counted as a space character.\nIf spaces are inserted where there shouldn't be any, increase this number.\nIf too many spaces are not detected, lower this number.");
			tt.SetToolTip(charSplitTolerance, "Configures how eagerly the OCR function splits characters.\nIf too many characters (especially 'k') get split in the middle, increase this number.\nIf too many double characters get erroneously detected as a single one, lower this number.");
			tt.SetToolTip(similarityTolerance, "Configures how similar two letters have to be so they are seen as equal.\nIf you have to manually enter too many letters, increase this number.\nIf there are some accidentially misidentified letters, lower this number.");
			tt.SetToolTip(contrast, "Sets a contrast correction on the image. Helpful for some subtitles that have large gray zones, but slows down OCR if it is set to any other value than zero.");
			tt.SetToolTip(autoProgress, "Automatically continues with the next subtitle if all characters in this one can be scanned. OCR will stop as soon as an unknown character is encountered.");
			tt.SetToolTip(autoOCRButton, "Automatically scans all subtitles. Unknown characters will simply be skipped.");
			tt.SetToolTip(loadButton, "Load a new subtitle file.");
			tt.SetToolTip(saveButton, "Save the scanned SRT file as you can see it on the left to a file.");
			tt.SetToolTip(convertDoubleApostrophes, "Automatically replaces double-apostrophes with a single quote sign.");
			tt.SetToolTip(replaceHighCommas, "Automatically replaces comma signs that are pretty high up in their line with apostrophes.");
			tt.SetToolTip(forcedOnly, "Only output forced subtitles.");
			tt.SetToolTip(combineSubtitles, "Combines two subsequent subtitles with completely identical text so they only use one line in the SRT.");
			tt.SetToolTip(ptsOffset, "The delay that should be applied to timestamps. For most subtitles it will be zero.");
			
			nextButton.Enabled = false;
			previousButton.Enabled = false;
			autoOCRButton.Enabled = false;
			ocrButton.Enabled = false;
			letterOKButton.Enabled = false;
			saveButton.Enabled = false;

			// Load the settings from the registry
            initialized = false;
			options = new AppOptions();
			minimumSpaceCharacterWidthTextBox.Text = AppOptions.minimumSpaceCharacterWidth.ToString();
			charSplitTolerance.Text = AppOptions.charSplitTolerance.ToString();
			similarityTolerance.Text = AppOptions.similarityTolerance.ToString();
			contrast.Text = AppOptions.contrast.ToString();
			convertDoubleApostrophes.Checked = AppOptions.convertDoubleApostrophes;
			stripFormatting.Checked = AppOptions.stripFormatting;
			replaceHighCommas.Checked = AppOptions.replaceHighCommas;
			forcedOnly.Checked = AppOptions.forcedOnly;
			preservePositions.Checked = AppOptions.preservePositions;
			combineSubtitles.Checked = AppOptions.combineSubtitles;
            initialized = true;

            try
            {
                Assembly x = Assembly.Load("Interop.MODI");
				AppOptions.modiInstalled = true;
            }
            catch (FileNotFoundException)
            {
				AppOptions.modiInstalled = false;
            }


			System.Windows.Forms.Timer timer1 = new System.Windows.Forms.Timer();
			timer1.Enabled = true;
			timer1.Interval = 100;
			timer1.Tick += new System.EventHandler(TimerEvent);

			Text = "SupRip " + version;
		}

		private void SaveSettings()
		{
			try
			{
				Debugger.PrintTimes();
				Debugger.CleanUp();
				fonts.Save();
				options.SaveOptions();
			}
			catch (Exception)
			{
			}
		}

		private void LoadSubtitleFile(string fileName)
		{
			SubtitleFile newSubFile;

			try
			{
				newSubFile = new SubtitleFile(fileName);
				//subfile.SaveXml("c:\\temp\\test.xml");
			}
			catch (SUPFileFormatException)
			{
				MessageBox.Show("Couldn't open the file\n" + fileName + ".\nMaybe it's not a BluRay .sup file?\nStandard resolution DVD and HD DVD subtitles aren't supported.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				newSubFile = null;
			}
			catch (IOException e)
			{
				MessageBox.Show("Couldn't open the file\n" + fileName + "\nbecause of \n" + e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				newSubFile = null;
			}
			catch (Exception e)
			{
				ErrorForm form = new ErrorForm(e);
				form.ShowDialog();
				newSubFile = null;
			}

			if (newSubFile != null)
			{
				//If the new file has been loaded successfully, dispose of the old one
				if (subfile != null)
					subfile.Close();
				subfile = newSubFile;

				nextButton.Enabled = true;
				previousButton.Enabled = true;
				autoOCRButton.Enabled = true;
				ocrButton.Enabled = true;
				letterOKButton.Enabled = true;
				saveButton.Enabled = true;

                subtitleType.Text = "Bluray";
                /*else if (subfile.Type == SubtitleFile.SubtitleType.Scenarist)
                    subtitleType.Text = "Scenarist";*/

                currentNum = 0;
#if DEBUG
				currentNum = 17;
#endif
				currentSubtitle = LoadSubtitleImage(currentNum);
				UpdateTextBox();
				totalPages.Text = "/ " + subfile.NumSubtitles;
				UpdateBitmaps();

				// Update the window title
				Text = "SupRip " + version + " - " + fileName.Substring(fileName.LastIndexOf('\\') + 1);
			}
		}

		public string version = "1.17";


		private void MainForm_Load(object sender, EventArgs e)
		{
#if DEBUG
			//LoadSubtitleFile("d:\\hdrip\\6.sup");
			//LoadSubtitleFile(@"d:\downloads\deadsnow.english.sup");
			LoadSubtitleFile(@"d:\hdrip\firefly2en.sup");
#endif
		}

		private SubtitleImage LoadSubtitleImage(int number)
		{
#if !DEBUG
			try
			{
#endif
			pageNum.Text = (currentNum + 1).ToString();
			//Debugger.Print("## Image " + number);
			SubtitleImage r = subfile.LoadSubtitleImage(number); //fixme AdjustFormScrollbars space length after narrow characters
			activeLetter = null;
			ImageOCR(r);
			return r;

#if !DEBUG
			}
			catch (Exception e)
			{
				fonts.Save();
				throw new Exception(e.Message + "\n\n" + e.StackTrace);
			}
#endif
		}

		private void MoveToImage(int num)
		{
			fontName.Text = fonts.DefaultFontName;

			if (currentSubtitle != null && num != currentNum)
			{
				subfile.UpdateSubtitleText(currentNum, currentSubtitle);

				currentNum = num;
				if (currentNum < 0)
					currentNum = 0;
				if (currentNum >= subfile.NumSubtitles)
					currentNum = subfile.NumSubtitles - 1;

				currentSubtitle = LoadSubtitleImage(currentNum);
				UpdateTextBox();
				UpdateBitmaps();
			}
		}

		private void pageNum_TextChanged(object sender, EventArgs e)
		{
			try
			{
				MoveToImage(Int32.Parse(pageNum.Text) - 1);
			}
			catch (FormatException)
			{
				pageNum.Text = currentNum.ToString();
			}
		}

		private void ActivateNextUnknownLetter()
		{
			// Search for the first letter that hasn't yet been converted to text
			activeLetter = null;
			foreach (SubtitleLetter l in currentSubtitle.letters)
			{
				if (l.Text == null)
				{
					// If we found an unconverted letter, first try to find a match in the current font
					SubtitleLetter l2 = fonts.FindMatch(l, AppOptions.similarityTolerance * 100);
					if (l2 != null)
					{
						l.Text = l2.Text;
						continue;
					}
					else
					{
						activeLetter = l;
						break;
					}
				}
			}

			if (activeLetter != null)
			{
				// We found an unknown character, so mark it for editing
				letterInputBox.Focus();
				AcceptButton = letterOKButton;
				UpdateBitmaps();
			}
			else
			{
				// All letters can be OCR'd in this text, so just redraw everything in green
				UpdateBitmaps();
			}
		}

		private void AssignLetterText(SubtitleLetter l, string text)
		{
			// Set the text on the letter that's part of the displayed subtitle
			activeLetter.Text = text;

			// Next, check whether this letter was already known before. If yes, we have to delete that one
			SubtitleLetter l2 = fonts.FindMatch(l, AppOptions.similarityTolerance * 100);
			if (l2 != null)
				fonts.DeleteLetter(l2);

			// Add the newly identified letter to the active font
			//SubtitleLetter letter = currentSubtitle.ExtractLetter(activeLetter, activeLetter.Angle);
			if (letterInputBox.Text != "")
			{
				activeLetter.Text = text;
				fonts.AddLetter(activeLetter);
			}
		}

		private void ImageOCR(SubtitleImage si)
		{
			si.ImageOCR(fonts, false);
            si.FixSpaces();
		}

		/*
		private void ImageOCR(SubtitleImage si, bool reportUnknownCharacter)
		{
			if (si.letters == null)
			{
				// We haven't scanned for letters yet, this probably means I'm debugging something as this shouldn't normally happen
				return;
			}

			fonts.debugStrings.Clear();
			// First try to scan all the letters we can
			foreach (SubtitleLetter l in si.letters)
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
		 * */

		
		public void ImageOCR(int n, bool report)
		{
			SubtitleImage si = subfile.LoadSubtitleImage(n);
			si.ImageOCR(fonts, report);
		}
		/*
		
		public void ImageOCR(int n, bool reportUnknownCharacter)
		{
			SubtitleImage si = subfile.LoadSubtitleImage(n);
			ImageOCR(si, reportUnknownCharacter);
			subfile.UpdateSubtitleText(n, si);
		}
		 * */

		private void EnterHTMLText(RichTextBox richTextBox, string t)
		{
			int x = 0, oldx = 0;
			string p;
			Font regularFont = new Font(richTextBox.Font, FontStyle.Regular), italicFont = new Font(richTextBox.Font, FontStyle.Italic);

			while (true)
			{
				x = t.IndexOf("<i>", oldx);
				if (x == -1)
				{
					// No more italic text, so just print the remainder in regular, then quit
					p = t.Substring(oldx, t.Length - oldx);
					richTextBox.SelectionFont = regularFont;
					richTextBox.AppendText(p);
					break;
				}
				p = t.Substring(oldx, x - oldx);
				richTextBox.SelectionFont = regularFont;
				richTextBox.AppendText(p);
				oldx = x + 3;
				x = t.IndexOf("</i>", oldx);
				p = t.Substring(oldx, x - oldx);
				richTextBox.SelectionFont = italicFont;
				richTextBox.AppendText(p);
				oldx = x + 4;
			}
		}

		private void UpdateTextBox()
		{
			// Update the subtitle text box with what we already know
			if (currentSubtitle != null)
			{
				string t = currentSubtitle.GetText();
				//SuspendLayout();
				subtitleTextBox2.Clear();
				EnterHTMLText(subtitleTextBox2, t);
				//ResumeLayout();
			}
			else
			{
				subtitleTextBox2.Text = "";
			}
		}

		#region BUTTONCLICKEVENTS

		private void previousButton_Click(object sender, EventArgs e)
		{
			MoveToImage(currentNum - 1);
		}

		private void nextButton_Click(object sender, EventArgs e)
		{
			MoveToImage(currentNum + 1);
		}

		private void TimerEvent(object sender, EventArgs e)
		{
			// Check the thread events whether one of the worker threads has finished
			// If that happened, dispose the modal progress dialog so that the main thread can continue
			if (finishedEvent != null && finishedEvent.WaitOne(0, true))
			{
				pf.Dispose();
				finishedEvent.Reset();
			}
		}

		private void StartImageOCR()
		{
			ImageOCR(currentSubtitle);

			UpdateTextBox();
			subfile.UpdateSubtitleText(currentNum, currentSubtitle);

			ActivateNextUnknownLetter();

			// If the checkbox to continue with the next subtitle is checked, try to search the whole movie for an unknown character
			// We do that in another thread so we don't block the UI
			if (activeLetter == null && autoProgress.Checked)
			{
				oldNum = currentNum;

				// Set up the thread for scanning the images
				stopEvent = new ManualResetEvent(false);
				finishedEvent = new ManualResetEvent(false);
				stopEvent.Reset();
				finishedEvent.Reset();

				updateProgressDelegate = new DelegateUpdateProgress(this.UpdateProgress);
				OcrThread worker = new OcrThread(this, stopEvent, finishedEvent, currentNum, subfile.NumSubtitles);
				Thread thread = new Thread(new ThreadStart(worker.Run));

				// Start the thread
				thread.Start();

				using (pf = new ProgressForm(this, subfile.NumSubtitles))
					pf.ShowDialog();

				if (!finishedEvent.WaitOne(0, true))
				{
					// FIXME. Add some code so that when the user clicks cancel, the image stays on the last processed subtitle
				}

				if (worker.FoundNum != -1)
					currentNum = worker.FoundNum;
				else
					currentNum = subfile.NumSubtitles - 1;

				currentSubtitle = LoadSubtitleImage(currentNum);
				ImageOCR(currentSubtitle);
				UpdateTextBox();
				ActivateNextUnknownLetter();
			}
		}

		private void StartPrecalculating()
		{
			// Set up the thread for scanning the images
			stopEvent = new ManualResetEvent(false);
			finishedEvent = new ManualResetEvent(false);
			stopEvent.Reset();
			finishedEvent.Reset();

			updateProgressDelegate = new DelegateUpdateProgress(this.UpdateProgress);
			OcrThread worker = new OcrThread(this, stopEvent, finishedEvent, subfile.NumSubtitles);
			Thread thread = new Thread(new ThreadStart(worker.Run));

			// Start the thread
			thread.Start();

			// If we ordinarily finished, then the thread has already run out.
			// But if the user pressed the cancel button, we have to stop the thread
			if (!finishedEvent.WaitOne(0, true))
				CancelThread();

			currentNum = oldNum;
			currentSubtitle = LoadSubtitleImage(currentNum);
			UpdateSRTPage();
			mainTabControl.SelectedIndex = 1;
		}

		private void ocrButton_Click(object sender, EventArgs e)
		{
			StartImageOCR();
		}

		private void letterOKButton_Click(object sender, EventArgs e)
		{
			if (letterInputBox.Text == "")
				AssignLetterText(activeLetter, null);
			else
				AssignLetterText(activeLetter, letterInputBox.Text);

			letterInputBox.Text = "";
			ImageOCR(currentSubtitle);
			UpdateTextBox();
			subfile.UpdateSubtitleText(currentNum, currentSubtitle);
			ActivateNextUnknownLetter();
			if (activeLetter == null)
				ocrButton.Focus();
		}

		#endregion

		protected override bool ProcessCmdKey(ref Message msg, Keys keydata)
		{
			switch (keydata)
			{
				case Keys.Control | Keys.P:
					MoveToImage(currentNum - 1);
					break;
				case Keys.Control | Keys.N:
					MoveToImage(currentNum + 1);
					break;
				case Keys.Control | Keys.O:
					StartImageOCR();
					break;
				case Keys.Escape:
					Application.Exit();
					break;
				default:
					return base.ProcessCmdKey(ref msg, keydata);
			}

			return true;
		}

		private void UpdateBitmaps()
		{
			// If we haven't loaded some subtitle file yet, we can't do anything so return
			if (currentSubtitle == null)
				return;

			if (currentSubtitle.subtitleBitmap == null)
			{
				// The current frame is empty
				try
				{
					subtitlePictureBox.Image = new Bitmap("empty.png");
				}
				catch (ArgumentException)
				{
					// Didn't find the bitmap file for some reason
					subtitlePictureBox.Image = null;
				}
				return;
			}

			// Create a graphics object so we can draw pretty rectangles on letters
			Bitmap workBitmap = (Bitmap)currentSubtitle.subtitleBitmap.Clone();
			Graphics g = Graphics.FromImage(workBitmap);

			if (currentSubtitle.letters != null)
			{
				Pen pen;
				foreach (SubtitleLetter l in currentSubtitle.letters)
				{
					if (activeLetter != null && l == activeLetter)
						pen = yellowPen;
					else if (l.Text != null)
						pen = greenPen;
					else
						pen = redPen;

					Rectangle r = l.Coords;
					if (l.Angle != 0.0)
					{
						int dx = (int)(l.Angle * r.Height / 2);
						int ddx = (int)(l.Height * l.Angle);
						Point[] polygon = new Point[4];
						polygon[0] = new Point(r.Left + dx - ddx, r.Top);
						polygon[1] = new Point(r.Right + dx - ddx, r.Top);
						polygon[2] = new Point(r.Right - dx - ddx, r.Bottom);
						polygon[3] = new Point(r.Left - dx - ddx, r.Bottom);
						g.DrawPolygon(pen, polygon);
					}
					else
						g.DrawRectangle(pen, r);
				}
			}

			// Draw some debug squares
			if (currentSubtitle.debugLocations != null)
			{
				foreach (KeyValuePair<int, Space> kvp in currentSubtitle.debugLocations)
				{
					//g.FillRectangle(Brushes.Yellow, kvp.Value.Rect);
					if (kvp.Value.Rect.Width == 0)
					{
						g.DrawLine(bluePen, kvp.Value.Rect.X, kvp.Value.Rect.Y, kvp.Value.Rect.Right, kvp.Value.Rect.Bottom);
					}
					else
					{
						if (kvp.Value.Partial)
							g.DrawRectangle(yellowPen, kvp.Value.Rect);
						else
							g.DrawRectangle(bluePen, kvp.Value.Rect);
					}
				}
			}

			if (currentSubtitle.debugPoints != null)
			{
				foreach (Point p in currentSubtitle.debugPoints)
				{
					Point p2 = new Point(p.X + 1, p.Y);
					g.DrawLine(redPen, p, p2);
				}
			}

			/*
			if (fonts.debugStrings != null)
			{
				foreach (PositionedString s in fonts.debugStrings)
				{
					g.DrawString(s.Str, new Font("Arial", 10.0f), new SolidBrush(Color.Yellow), s.Position.X, s.Position.Bottom+1);
					Point p2 = new Point(s.Position.X + 1, s.Position.Y);
					g.DrawLine(redPen, p2, p2);
				}
			}
			*/

			// Calculate the ratio and displacement that PictureBoxSizeMode.Zoom is going to use later because we need it for the mouse clicking event handling
			double widthRatio = (double)subtitlePictureBox.Width / workBitmap.Width;
			double heightRatio = (double)subtitlePictureBox.Height / workBitmap.Height;
			bitmapScale = Math.Min(widthRatio, heightRatio);
			subtitleImageRectangle = new Rectangle(subtitlePictureBox.Left, subtitlePictureBox.Top, subtitlePictureBox.Width, subtitlePictureBox.Height);
			if (widthRatio > heightRatio)
			{
				double w = workBitmap.Width * heightRatio;
				subtitleImageRectangle = new Rectangle((subtitlePictureBox.Width - (int)w) / 2, 0, subtitlePictureBox.Width - (subtitlePictureBox.Width - (int)w), subtitlePictureBox.Height);
			}
			else // if (widthRatio < heightRatio)
			{
				double h = workBitmap.Height * widthRatio;
				subtitleImageRectangle = new Rectangle(0, (subtitlePictureBox.Height - (int)h) / 2, subtitlePictureBox.Width, subtitlePictureBox.Height - (subtitlePictureBox.Height - (int)h));
			}

			// Assign the bitmap with the subtitles and the rectangles to the picture box
			subtitlePictureBox.Image = workBitmap;

			// Display the letter we're about to edit beside the editing text box
			if (activeLetter != null)
			{
				//letterPictureBox.Image = currentSubtitle.GetSubtitlePart(activeLetter.Coords);
				letterPictureBox.Image = activeLetter.GetBitmap();
				letterOKButton.Enabled = letterPictureBox.Image != null;
			}
			else
			{
				letterPictureBox.Image = null;
				letterOKButton.Enabled = false;
			}
		}

		private void imagePage_Paint(object sender, PaintEventArgs e)
		{
			UpdateBitmaps();
		}

        private void ApplyOptions()
        {
            if (!initialized)
                return;

            try
            {
                if (Int32.Parse(minimumSpaceCharacterWidthTextBox.Text) < 1 || Int32.Parse(minimumSpaceCharacterWidthTextBox.Text) > 20)
                    throw new FormatException();
                AppOptions.minimumSpaceCharacterWidth = Int32.Parse(minimumSpaceCharacterWidthTextBox.Text);
            }
            catch (FormatException)
            {
                minimumSpaceCharacterWidthTextBox.Text = AppOptions.minimumSpaceCharacterWidth.ToString();
            }

            try
            {
                if (Int32.Parse(charSplitTolerance.Text) < 1 || Int32.Parse(charSplitTolerance.Text) > 20)
                    throw new FormatException();
                AppOptions.charSplitTolerance = Int32.Parse(charSplitTolerance.Text);
            }
            catch (FormatException)
            {
                charSplitTolerance.Text = AppOptions.charSplitTolerance.ToString();
            }

			try
			{
				if (Int32.Parse(similarityTolerance.Text) < 1 || Int32.Parse(similarityTolerance.Text) > 200)
					throw new FormatException();
				AppOptions.similarityTolerance = Int32.Parse(similarityTolerance.Text);
			}
			catch (FormatException)
			{
				similarityTolerance.Text = AppOptions.similarityTolerance.ToString();
			}

			try
			{
				if (Int32.Parse(contrast.Text) < 0 || Int32.Parse(contrast.Text) > 10)
					throw new FormatException();
				AppOptions.contrast = Int32.Parse(contrast.Text);
			}
			catch (FormatException)
			{
				contrast.Text = AppOptions.contrast.ToString();
			}

			if (subfile != null)
            {
                currentSubtitle = LoadSubtitleImage(currentNum);
                UpdateBitmaps();
                UpdateTextBox();
            }
        }

		private void optionsApplyButton_Click(object sender, EventArgs e)
		{
            ApplyOptions();
		}

		private void mainTabControl_Click(object sender, EventArgs e)
		{
			UpdateSRTPage();
		}

		private bool ignoreItalicChanges;
		private void subtitlePictureBox_MouseClick(object sender, MouseEventArgs e)
		{
			// Check whether the click happened within the subtitle image
			if (subtitleImageRectangle.Contains(e.Location))
			{
				Point pt = e.Location;
				pt.Offset(-subtitleImageRectangle.Left, -subtitleImageRectangle.Top);
				pt = new Point((int)(pt.X / bitmapScale), (int)(pt.Y / bitmapScale));
				SubtitleLetter hitLetter = null;
				foreach (SubtitleLetter l in currentSubtitle.letters)
				{
					if (l.Coords.Contains(pt))
					{
						hitLetter = l;
						break;
					}
				}

				if (hitLetter != null)
				{
					// The click went inside a letter, so activate it
					activeLetter = hitLetter;

					letterInputBox.Text = hitLetter.Text;
					letterInputBox.SelectAll();
					letterInputBox.Focus();
					AcceptButton = letterOKButton;

					ignoreItalicChanges = true;
					if (activeLetter.Angle != 0.0)
						italicLetter.Checked = true;
					else
						italicLetter.Checked = false;
					ignoreItalicChanges = false;

					UpdateBitmaps();
				}
				else
				{
					// No letter was hit, so unselect everything
					activeLetter = null;
					letterInputBox.Text = "";
					letterOKButton.Enabled = false;

					UpdateBitmaps();
				}
			}

		}

		private void UpdateProgress(int x)
		{
			if (pf != null)
				pf.SetProgressBarPosition(x);
		}

		public void CancelThread()
		{
			stopEvent.Set();
		}

		private void autoOCRButton_Click(object sender, EventArgs e)
		{
			oldNum = currentNum;

			// Set up the thread for scanning the images
			stopEvent = new ManualResetEvent(false);
			finishedEvent = new ManualResetEvent(false);
			stopEvent.Reset();
			finishedEvent.Reset();

			updateProgressDelegate = new DelegateUpdateProgress(this.UpdateProgress);
			OcrThread worker = new OcrThread(this, stopEvent, finishedEvent, subfile.NumSubtitles);
			Thread thread = new Thread(new ThreadStart(worker.Run));

			// Start the thread
			thread.Start();

			using (pf = new ProgressForm(this, subfile.NumSubtitles))
				pf.ShowDialog();

			// If we ordinarily finished, then the thread has already run out.
			// But if the user pressed the cancel button, we have to stop the thread
			if (!finishedEvent.WaitOne(0, true))
				CancelThread();

			currentNum = oldNum;
			currentSubtitle = LoadSubtitleImage(currentNum);
			UpdateSRTPage();
			mainTabControl.SelectedIndex = 1;
		}

		private void UpdateSRTPage()
		{
			if (subfile != null)
			{
				subfile.RecalculateTimes();
				srtTextBox.Text = subfile.GetSRTText();
				SRTInfo srtInfo = subfile.GetSRTInfo();
				unscannedLabel.Text = srtInfo.unscanned.ToString();
				containingErrorsLabel.Text = srtInfo.containingErrors.ToString();
				finishedLabel.Text = srtInfo.finished.ToString();
			}
		}

		private void saveButton_Click(object sender, EventArgs e)
		{
			SaveFileDialog sfd = new SaveFileDialog();
			//sfd.CheckFileExists = true;
			sfd.AddExtension = true;
			sfd.DefaultExt = "srt";
			sfd.Filter = "SRT subtitles (*.srt)|*.srt|All files (*.*)|*.*";
			sfd.FilterIndex = 0;
			//sfd.InitialDirectory = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
			if (sfd.ShowDialog() == DialogResult.OK)
			{
				StreamWriter sw = new StreamWriter(sfd.OpenFile(), Encoding.Unicode);
				sw.Write(subfile.GetSRTText());
				sw.Close();
			}
		}

		private void loadButton_Click(object sender, EventArgs e)
		{
			OpenFileDialog ofd = new OpenFileDialog();
			ofd.CheckFileExists = true;
			//ofd.AddExtension = true;
			//ofd.DefaultExt = "srt";
			ofd.Filter = "Subpicture files (*.sup)|*.sup|Scenarist Subtitles (*.scn-sst)|*.scn-sst|All files (*.*)|*.*";
			ofd.FilterIndex = 0;
			//ofd.InitialDirectory = AppOptions.openFileDirectory;
			if (ofd.ShowDialog() == DialogResult.OK)
			{
				try
				{
					LoadSubtitleFile(ofd.FileName);
					mainTabControl.SelectedIndex = 0;
					//AppOptions.openFileDirectory = ofd.
				}
				catch (SSTFileFormatException ex)
				{
					MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
			}
		}

		private void convertDoubleApostrophes_CheckedChanged(object sender, EventArgs e)
		{
			AppOptions.convertDoubleApostrophes = convertDoubleApostrophes.Checked;

			UpdateSRTPage();
		}

		private void stripFormatting_CheckedChanged(object sender, EventArgs e)
		{
			AppOptions.stripFormatting = stripFormatting.Checked;

			UpdateSRTPage();
		}

		private void replaceHighCommas_CheckedChanged(object sender, EventArgs e)
		{
			AppOptions.replaceHighCommas = replaceHighCommas.Checked;
			if (subfile != null)
			{
				currentSubtitle = LoadSubtitleImage(currentNum);
				UpdateBitmaps();
			}
		}

		private void forcedOnly_CheckedChanged(object sender, EventArgs e)
		{
			AppOptions.forcedOnly = forcedOnly.Checked;

			UpdateSRTPage();
		}

		private void startCharacterMap_Click(object sender, EventArgs e)
		{
			Process charMap = new Process();
			int p = (int)Environment.OSVersion.Platform;
			if ((p == 4) || (p == 128))
				charMap.StartInfo.FileName = "/usr/bin/gucharmap";
			else
				charMap.StartInfo.FileName = System.Environment.GetFolderPath(Environment.SpecialFolder.System) + "\\charmap.exe";
			charMap.Start();
		}

		private void resetDefaults_Click(object sender, EventArgs e)
		{
			options.ResetToDefaults();
			minimumSpaceCharacterWidthTextBox.Text = AppOptions.minimumSpaceCharacterWidth.ToString();
			charSplitTolerance.Text = AppOptions.charSplitTolerance.ToString();
			similarityTolerance.Text = AppOptions.similarityTolerance.ToString();
			contrast.Text = AppOptions.contrast.ToString();
			convertDoubleApostrophes.Checked = AppOptions.convertDoubleApostrophes;
			stripFormatting.Checked = AppOptions.stripFormatting;
			replaceHighCommas.Checked = AppOptions.replaceHighCommas;
			forcedOnly.Checked = AppOptions.forcedOnly;
			combineSubtitles.Checked = AppOptions.combineSubtitles;
		}

		private void ptsOffset_TextChanged(object sender, EventArgs e)
		{
			try
			{
				//AppOptions.ptsOffset = Int32.Parse(ptsOffset.Text, System.Globalization.NumberStyles.AllowHexSpecifier);
				AppOptions.ptsOffset = Int32.Parse(ptsOffset.Text);
			}
			catch (FormatException)
			{
				if (AppOptions.ptsOffset.ToString().Length == 1)
					ptsOffset.Text = "";
				else
					ptsOffset.Text = String.Format("{0}", AppOptions.ptsOffset);
			}

			UpdateSRTPage();
		}

		private void combineSubtitles_CheckedChanged(object sender, EventArgs e)
		{
			AppOptions.combineSubtitles = combineSubtitles.Checked;

			UpdateSRTPage();
		}

		private void debugButton_Click(object sender, EventArgs e)
		{
			string[] fontNames = fonts.FontList();

			// Create the debug menu and populate it with all the currently loaded fonts
			EventHandler eh = new EventHandler(debugMenu_Click);
			MenuItem[] mi = new MenuItem[fontNames.Length + 1];
			int i=0;
			foreach (string fn in fontNames)
			{
				mi[i] = new MenuItem(fn);
				mi[i].Click += eh;
				i++;
			}

			mi[i] = new MenuItem("Duplicates");
			mi[i].Click += eh;

			// Display the menu
			ContextMenu x = new ContextMenu(mi);
			x.Show(debugButton, new Point(0, 0));
		}

		private void debugMenu_Click(object sender, EventArgs e)
		{
			MenuItem menuItem = (MenuItem)sender;
			if (menuItem.Index < fonts.Count)
				fonts.MergeUserFont(((MenuItem)sender).Text);
			else
			{
				Debugger.Print("Duplicates:");
				Debugger.Print(fonts.ListDuplicates());
			}
		}

		public bool IsSubtitleForced(int n)
		{
			return subfile.IsSubtitleForced(n);
		}

        private void options_TextChanged(object sender, EventArgs e)
        {
            ApplyOptions();
        }

		private void italicLetter_CheckedChanged(object sender, EventArgs e)
		{
			if (!ignoreItalicChanges)
			{
				activeLetter.Angle = 0.0;
			}
		}
	}
}
