using System;
using System.Collections.Generic;
using System.Text;

namespace SupRip
{
	class FontfileFormatException : Exception
	{
		public FontfileFormatException(string reason) : base(reason)
		{
		}
	}
}
