using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace SupRip
{
	class PositionedString
	{
		private Rectangle position;
		private string str;

		public PositionedString(Rectangle p, string s)
		{
			position = p;
			str = s;
		}

		public Rectangle Position
		{
			get { return position; }
		}

		public string Str
		{
			get { return str; }
		}
	}
}
