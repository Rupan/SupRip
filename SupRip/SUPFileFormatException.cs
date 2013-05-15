using System;
using System.Collections.Generic;
using System.Text;

namespace SupRip
{
	class SUPFileFormatException : Exception
	{
		public SUPFileFormatException(string reason)
			: base(reason)
		{
		}

		public SUPFileFormatException()
			: base()
		{
		}
	}
}
