using System;
using System.Collections.Generic;
using System.Text;

namespace SupRip
{
	class SSTFileFormatException : Exception
	{
		public SSTFileFormatException(string reason) : base(reason)
		{
		}
	}
}
