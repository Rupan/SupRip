using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace SupRip
{
	class Space
	{
		private Rectangle rect;
		private bool partial;
		private SpaceType type;
		private double angle;
		private int slopeStart;

		public enum SpaceType { Straight=0, TopRight, TopLeft, BottomRight, BottomLeft };

		public Space(Rectangle rect)
			: this(rect, false)
		{
		}

		public Space(Rectangle rect, bool isPartial)
		{
			this.rect = rect;
			partial = isPartial;
		}

		public Space(int left, int top, int width, int height, bool isPartial)
		{
			rect = new Rectangle(left, top, width, height);
			partial = isPartial;
		}

		public Space(int left, int top, int width, int height, bool isPartial, SpaceType t, int slope, double a)
			: this(left, top, width, height, isPartial)
		{
			type = t;
			angle = a;
			slopeStart = slope;
		}

		public Space(int left, int top, int width, int height)
			: this(left, top, width, height, false)
		{
		}

		public void Resize(int left, int top, int right, int bottom)
		{
			rect.X -= left;
			rect.Width += left + right;
			rect.Y -= top;
			rect.Height += top + bottom;
		}

		override public string ToString()
		{
			string r = "";

			if (partial)
				r = "partial ";

			switch (type)
			{
				case SpaceType.Straight:
					return r + "straight";
				case SpaceType.TopRight:
					return r + "topright";
				case SpaceType.TopLeft:
					return r + "topleft";
				case SpaceType.BottomRight:
					return r + "bottomright";
				case SpaceType.BottomLeft:
					return r + "bottomleft";
			}

			return r;
		}

		public Rectangle Rect
		{
			get { return rect; }
			//set { rect = value; }
		}

		public SpaceType Type
		{
			get { return type; }
		}

		public double Angle
		{
			get { return angle; }
		}

		public int SlopeStart
		{
			get { return slopeStart; }
		}

		public bool Partial
		{
			get { return partial; }
			set { partial = value; }
		}

		public int Hash
		{
			get { return rect.Left * 1000 + rect.Top; }
		}

	}
}
