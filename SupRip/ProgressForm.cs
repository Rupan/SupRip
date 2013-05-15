using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace SupRip
{
	public partial class ProgressForm : Form
	{
		MainForm mainForm;

		public ProgressForm(MainForm m, int numSubtitles)
		{
			mainForm = m;
			InitializeComponent();
			progressBar.Maximum = numSubtitles;
		}

		public void SetProgressBarPosition(int p)
		{
			progressBar.Value = p;
			numLabel.Text = p.ToString() + " / " + progressBar.Maximum;
		}

		private void ProgressForm_Load(object sender, EventArgs e)
		{

		}

		private void cancelButton_Click(object sender, EventArgs e)
		{
			mainForm.CancelThread();
		}
	}
}