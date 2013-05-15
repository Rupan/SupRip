using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace SupRip
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main(string[] args)
		{
			//Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

			if (args.Length >= 1)
			{
				//SubtitleFile subfile = new SubtitleFile("F:\\hdrip\\hb.sup");
				//subfile.WriteBitmap("c:\\temp\\test.png", 20);
				SubtitleFile subfile = new SubtitleFile(args[0]);
				int nb = 20;
				if (args.Length >= 2)
					nb = Int32.Parse(args[1]);

				int spacing = 100;
				if (args.Length >= 3)
					spacing = Int32.Parse(args[2]);

				try
				{
					subfile.WriteBitmaps(args[0], nb, spacing);
				}
				catch (Exception e)
				{
					Console.WriteLine("exception " + e.Message);
				}
			}
			else
			{

#if DEBUG
				Application.Run(new MainForm());
#else
				try
				{
					Application.Run(new MainForm());
				}
				catch (Exception e)
				{
					ErrorForm form = new ErrorForm(e);
					form.ShowDialog();
				}
#endif
			}
		}
	}
}