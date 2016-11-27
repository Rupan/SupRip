using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace SupRip
{
	class PrecalculateThread
	{
		ManualResetEvent stopEvent, finishedEvent;
		MainForm parentForm;
		int nSubtitles;

		public PrecalculateThread(MainForm f, ManualResetEvent se, ManualResetEvent fe, int n)
		{
			parentForm = f;
			stopEvent = se;
			finishedEvent = fe;
			nSubtitles = n;
		}

		public void Run()
		{
			for (int i = 0; i < nSubtitles; i++)
			{
				if (stopEvent.WaitOne(0, true))
					break;

				parentForm.ImageOCR(i, false);
				parentForm.Invoke(parentForm.updateProgressDelegate, new Object[] { i * 100 / nSubtitles });
			}

			finishedEvent.Set();
		}

	}
}
