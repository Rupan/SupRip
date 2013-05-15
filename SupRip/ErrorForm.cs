using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace SupRip
{
	public partial class ErrorForm : Form
	{
		Exception exception;

		public ErrorForm()
		{
			InitializeComponent();
		}

		public ErrorForm(Exception e)
		{
			InitializeComponent();

			exception = e;
		}

		private void ErrorForm_Load(object sender, EventArgs e)
		{
			errorText.Text = exception.Message;
			errorText.Text += "\r\n\r\n";
			errorText.Text += exception.TargetSite;
			errorText.Text += "\r\n\r\n";
			errorText.Text += exception.StackTrace;
		}
	}
}