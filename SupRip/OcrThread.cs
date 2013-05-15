using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace SupRip
{
	class OcrThread
	{

		ManualResetEvent stopEvent, finishedEvent;
		MainForm parentForm;
		int nSubtitles, startingSubtitle;
		bool reportUnknownChar;
		int foundNum;

		public int FoundNum
		{
			get { return foundNum; }
		}

		public OcrThread(MainForm f, ManualResetEvent se, ManualResetEvent fe, int n)
		{
			parentForm = f;
			stopEvent = se;
			finishedEvent = fe;
			nSubtitles = n;

			reportUnknownChar = false;
		}

		public OcrThread(MainForm f, ManualResetEvent se, ManualResetEvent fe, int start, int n)
		{
			parentForm = f;
			stopEvent = se;
			finishedEvent = fe;
			nSubtitles = n;

			startingSubtitle = start;
			reportUnknownChar = true;
			foundNum = -1;
		}

		public void Run()
		{
			if (reportUnknownChar)
			{
				// If we find a character that we can't scan, we should report this to the UI thread so it can ask the user for an identification
				for (int i = startingSubtitle; i < nSubtitles; i++)
				{
					if (AppOptions.forcedOnly && !parentForm.IsSubtitleForced(i))
						continue;

					if (stopEvent.WaitOne(0, true))
						break;

					try
					{
						parentForm.ImageOCR(i, true);
						parentForm.Invoke(parentForm.updateProgressDelegate, new Object[] { i });
					}
					catch (UnknownCharacterException)
					{
						foundNum = i;
						break;
					}
				}
			}
			else
			{
				// Just scan the whole movie, leaving unidentifiable characters alone
				for (int i = 0; i < nSubtitles; i++)
				{
					if (AppOptions.forcedOnly && !parentForm.IsSubtitleForced(i))
						continue;

					if (stopEvent.WaitOne(0, true))
						break;

					parentForm.ImageOCR(i, false);
					parentForm.Invoke(parentForm.updateProgressDelegate, new Object[] { i });
				}
			}

			finishedEvent.Set();
		}

	}
}
