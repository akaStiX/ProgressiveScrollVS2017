using System.Collections.Generic;

namespace ProgressiveScroll
{
	using System;
	using System.Windows;
	using System.Windows.Media;
	using Microsoft.VisualStudio.Text.Editor;
	using Microsoft.VisualStudio.Text.Tagging;
	using System.Windows.Media.Imaging;
	using Microsoft.VisualStudio.Text;
	using Microsoft.VisualStudio.Text.Outlining;
using Microsoft.VisualStudio.Text.Formatting;

	class ProgressiveScrollElement : FrameworkElement
	{
		private readonly IWpfTextView _textView;
		private readonly IOutliningManager _outliningManager;
		private readonly IVerticalScrollBar _scrollBar;

		private Dictionary<string, int> _keywords;

		private readonly int _scrollBarWidth = 17;
		private readonly int _minViewportHeight = 5;
		private int _width;
		private int _height;


		private byte[] _pixels;
		private readonly PixelFormat pf = PixelFormats.Rgb24;
		private int _stride;
		private BitmapSource _bitmap;

		private Color _whitespaceColor;
		private Color _normalColor;
		private Color _commentColor;
		private Color _stringColor;
		private Color _visibleColor;

		private Brush _backgroundBrush;
		private Brush _visibleBrush;

		/// <summary>
		/// Constructor for the ProgressiveScrollElement.
		/// </summary>
		/// <param name="textView">ITextView to which this ProgressiveScrollElement will be attacheded.</param>
		/// <param name="verticalScrollbar">Vertical scrollbar of the ITextViewHost that contains <paramref name="textView"/>.</param>
		/// <param name="tagFactory">MEF tag factory.</param>
		public ProgressiveScrollElement(IWpfTextView textView, IOutliningManager outliningManager, IVerticalScrollBar verticalScrollbar, ProgressiveScrollFactory factory)
		{
			_textView = textView;
			_outliningManager = outliningManager;
			_scrollBar = verticalScrollbar;

			SnapsToDevicePixels = true;

			_whitespaceColor = Color.FromRgb(0, 0, 0);
			_normalColor = Color.FromRgb(255, 255, 255);
			_commentColor = Color.FromRgb(255, 128, 255);
			_stringColor = Color.FromRgb(163, 21, 21);
			_visibleColor = Color.FromArgb(64, 255, 255, 255);

			_backgroundBrush = new SolidColorBrush(_whitespaceColor);
			_visibleBrush = new SolidColorBrush(_visibleColor);

			_width = 128;
			_stride = (_width * pf.BitsPerPixel + 7) / 8;
			_height = 0;
			_pixels = null;

			Width = _width - _scrollBarWidth;

			AddKeywords();

			outliningManager.RegionsChanged += OnRegionsChanged;
			outliningManager.RegionsCollapsed += OnRegionsCollapsed;
			outliningManager.RegionsExpanded += OnRegionsExpanded;

			this.IsVisibleChanged += OnIsVisibleChanged;
		}

		public void Dispose()
		{
		}

		private void OnRegionsChanged(object sender, RegionsChangedEventArgs args)
		{
			this.InvalidateVisual();
		}

		private void OnRegionsCollapsed(object sender, RegionsCollapsedEventArgs args)
		{
			this.InvalidateVisual();
		}

		private void OnRegionsExpanded(object sender, RegionsExpandedEventArgs args)
		{
			this.InvalidateVisual();
		}

		private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			if ((bool)e.NewValue)
			{
				_scrollBar.TrackSpanChanged += OnTrackSpanChanged;
				this.InvalidateVisual();
			}
			else
			{
				_scrollBar.TrackSpanChanged -= OnTrackSpanChanged;
			}
		}

		private void OnTrackSpanChanged(object sender, EventArgs e)
		{
			this.InvalidateVisual();
		}

		private double GetYCoordinateOfLineBottom(ITextViewLine line)
		{
			ITextSnapshot snapshot = _textView.TextSnapshot;
			if (line.EndIncludingLineBreak.Position < snapshot.Length)
			{
				// line is not the last line; get the Y coordinate of the next line.
				return _scrollBar.GetYCoordinateOfBufferPosition(new SnapshotPoint(snapshot, line.EndIncludingLineBreak.Position + 1));
			}
			else
			{
				// last line.
				double empty = 1 - ((_textView.TextViewLines.LastVisibleLine.Bottom - _textView.TextViewLines.FirstVisibleLine.Bottom) / _textView.ViewportHeight);
				return _scrollBar.GetYCoordinateOfScrollMapPosition(_scrollBar.Map.End + _scrollBar.Map.ThumbSize * empty);
			}
		}

		/// <summary>
		/// Override for the FrameworkElement's OnRender. When called, redraw
		/// all of the markers
		/// </summary>
		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);

			if (!this._textView.IsClosed)
			{
				Brush backgroundBrush = new SolidColorBrush(_whitespaceColor);
				drawingContext.DrawRectangle(
					backgroundBrush,
					null,
					new Rect(-_scrollBarWidth, 0, _width, ActualHeight));

				RenderText();

				this.VisualBitmapScalingMode = System.Windows.Media.BitmapScalingMode.Fant;
				Rect rect = new Rect(-_scrollBarWidth, 0, _width, Math.Min(_height, ActualHeight));
				drawingContext.DrawImage(_bitmap, rect);

				/*SnapshotPoint start = new SnapshotPoint(_textView.TextViewLines.FirstVisibleLine .Snapshot, _textView.TextViewLines.FirstVisibleLine.Start);

				double viewportTop =  Math.Floor(_scrollBar.GetYCoordinateOfBufferPosition(start));
				double viewportBottom = Math.Ceiling(Math.Max(GetYCoordinateOfLineBottom(_textView.TextViewLines.LastVisibleLine), viewportTop + _minViewportHeight));

				drawingContext.DrawRectangle(_visibleBrush, null, new Rect(-_scrollBarWidth, viewportTop, _width, viewportBottom));*/
			}
		}

		enum CommentType
		{
			None,
			SingleLine,
			MultiLine
		};

		private void RenderText()
		{
			// Find the hidden regions
			IEnumerable<ICollapsed> collapsedRegions = _outliningManager.GetCollapsedRegions(new SnapshotSpan(_textView.TextBuffer.CurrentSnapshot, new Span(0, _textView.TextBuffer.CurrentSnapshot.Length)));
			IEnumerator<ICollapsed> currentCollapsedRegion = collapsedRegions.GetEnumerator();
			SnapshotSpan? currentCollapsedSnapshotSpan = null;
			if (currentCollapsedRegion.MoveNext())
			{
				currentCollapsedSnapshotSpan = currentCollapsedRegion.Current.Extent.GetSpan(_textView.TextBuffer.CurrentSnapshot);
			}

			_height = _textView.VisualSnapshot.LineCount;
			_pixels = new byte[_stride * _height];

			string text = _textView.TextBuffer.CurrentSnapshot.GetText();

			int wrapAfter = int.MaxValue;
			int tabSize = 4;

			CommentType commentType = CommentType.None;

			bool inKeyword = false;
			bool inString = false;

			// Here "virtual" refers to rendered coordinates, which can differ from real text coordinates due to word wrapping,
			// hidden text regions and tabs.
			int virtualLine = 0;
			int virtualColumn = 0;
			int realLine = 0;
			int realColumn = 0;
			bool isLineVisible = true;

			for (int i = 0; i < text.Length; ++i)
			{
				// Check for a real newline, a virtual newline (due to word wrapping) or the end of the text.
				bool isRealNewline = (text[i] == '\r') || (text[i] == '\n');
				bool isVirtualNewline = (virtualColumn >= wrapAfter);
				bool isTextEnd = (i == text.Length - 1);

				if (isRealNewline || isVirtualNewline || isTextEnd)
				{
					virtualColumn = 0;

					if (isLineVisible)
					{
						// Advance the virtual line.
						++virtualLine;
					}

					if (isTextEnd)
					{
						break;
					}

					if (isRealNewline)
					{
						++realLine;
						realColumn = 0;
						inKeyword = false;
						inString = false;

						// In case of CRLF, eat the next character too.
						if( (text[i] == '\r') && (i + 1 < text.Length) && (text[i + 1] == '\n'))
						{
							++i;
						}

						if (i + 1 < text.Length)
						{
							if (currentCollapsedSnapshotSpan.HasValue && (i + 1 > currentCollapsedSnapshotSpan.Value.End.Position))
							{
								if (currentCollapsedRegion.MoveNext())
								{
									currentCollapsedSnapshotSpan = currentCollapsedRegion.Current.Extent.GetSpan(_textView.TextBuffer.CurrentSnapshot);
								}
								else
								{
									currentCollapsedSnapshotSpan = null;
								}
							}

							isLineVisible = !(currentCollapsedSnapshotSpan.HasValue && (i + 1 > currentCollapsedSnapshotSpan.Value.Start.Position));
						}

						if (commentType == CommentType.SingleLine)
						{
							commentType = CommentType.None;
						}

						continue;
					}
				}

				int numChars = 1;
				if (!System.Char.IsWhiteSpace(text[i]))
				{
					switch (commentType)
					{
						case CommentType.None:
						{
							if (!inString && (text.Substring(i, 2) == "//"))
							{
								commentType = CommentType.SingleLine;
								inKeyword = false;
							}
							else
							if (!inString && (text.Substring(i, 2) == "/*"))
							{
								commentType = CommentType.MultiLine;
								inKeyword = false;
							}
							else
							if (text[i] == '"')
							{
								inKeyword = false;
								if (inString)
								{
									int backslashStart = i - 1;
									while (i >= 0 && text[i] == '\\')
									{
										--backslashStart;
									}

									int numBackslashes = i - backslashStart - 1;

									if (numBackslashes % 2 == 0)
									{
										inString = false;
									}
								}
								else
								{
									inString = true;
								}
							}
							else
							if (!inKeyword && !inString && IsCppIdStart(text[i]) && ((i == 0) || IsCppIdSeparator(text[i - 1])))
							{
								int keywordEnd = i + 1;
								while (keywordEnd < text.Length && !IsCppIdSeparator(text[keywordEnd]))
								{
									++keywordEnd;
								}
								int len = keywordEnd - i;
								inKeyword = IsKeyword(text.Substring(i, len));
							}
							else
							if (inKeyword && IsCppIdSeparator(text[i]))
							{
								inKeyword = false;
							}
						}
						break;

						case CommentType.MultiLine:
						{
							if ((text[i - 1] == '*') && (text[0] == '/'))
							{
								commentType = CommentType.None;
							}
						}
						break;
					}

					// Advance the highlight interval, if needed.
					//while(crHighlight && (realColumn >= (int)crHighlight->end))
					//	crHighlight = crHighlight->next;

					// Override the color with the match color if inside a marker.
					//if(crHighlight && (realColumn >= (int)crHighlight->start))
					//	textFlags |= TextFlag_Highlight;

					if (isLineVisible)
					{
						if (commentType != CommentType.None)
						{
							SetPixel(virtualColumn, virtualLine, _commentColor);
						}
						else if (inString)
						{
							SetPixel(virtualColumn, virtualLine, _stringColor);
						}
						else
						{
							SetPixel(virtualColumn, virtualLine, _normalColor);
						}
					}
				}
				else
				{
					inKeyword = false;

					if (text[i] == '\t')
					{
						numChars = tabSize - (virtualColumn % tabSize);
					}

					if (isLineVisible)
					{
						SetPixels(virtualColumn, virtualLine, _whitespaceColor, numChars);
					}
				}

				++realColumn;
				virtualColumn += numChars;
			}

			_bitmap = BitmapSource.Create(
				_width,
				_height,
				96,
				96,
				pf,
				null,
				_pixels,
				_stride);
		}

		private void SetPixel(int x, int y, Color c)
		{
			if (x < _width && y < _height)
			{
				int pixelOffset = y * _stride + x * 3;
				_pixels[pixelOffset] = c.R;
				_pixels[pixelOffset + 1] = c.G;
				_pixels[pixelOffset + 2] = c.B;
			}
		}

		private void SetPixels(int x, int y, Color c, int num)
		{
			for (int i = 0; i < num; ++i)
			{
				SetPixel(x + i, y, c);
			}
		}

		private bool IsCppIdSeparator(char c)
		{
			return !Char.IsLetterOrDigit(c) && c != '_';
		}

		private bool IsCppIdStart(char c)
		{
			return System.Char.IsLetter(c) || (c == '_');
		}

		private void AddKeywords()
		{
			_keywords = new Dictionary<string, int>();
			_keywords.Add("if", 0);
			_keywords.Add("inline", 0);
			_keywords.Add("do", 0);
			_keywords.Add("union", 0);
			_keywords.Add("void", 0);
			_keywords.Add("delete", 0);
			_keywords.Add("for", 0);
			_keywords.Add("__asm", 0);
			_keywords.Add("unsigned", 0);
			_keywords.Add("__alignof", 0);
			_keywords.Add("public", 0);
			_keywords.Add("this", 0);
			_keywords.Add("while", 0);
			_keywords.Add("struct", 0);
			_keywords.Add("throw", 0);
			_keywords.Add("static", 0);
			_keywords.Add("wchar_t", 0);
			_keywords.Add("template", 0);
			_keywords.Add("char", 0);
			_keywords.Add("break", 0);
			_keywords.Add("static_cast", 0);
			_keywords.Add("else", 0);
			_keywords.Add("volatile", 0);
			_keywords.Add("enum", 0);
			_keywords.Add("friend", 0);
			_keywords.Add("default", 0);
			_keywords.Add("int", 0);
			_keywords.Add("bool", 0);
			_keywords.Add("double", 0);
			_keywords.Add("goto", 0);
			_keywords.Add("class", 0);
			_keywords.Add("switch", 0);
			_keywords.Add("new", 0);
			_keywords.Add("false", 0);
			_keywords.Add("sizeof", 0);
			_keywords.Add("private", 0);
			_keywords.Add("try", 0);
			_keywords.Add("case", 0);
			_keywords.Add("short", 0);
			_keywords.Add("return", 0);
			_keywords.Add("register", 0);
			_keywords.Add("reinterpret_cast", 0);
			_keywords.Add("mutable", 0);
			_keywords.Add("long", 0);
			_keywords.Add("const", 0);
			_keywords.Add("signed", 0);
			_keywords.Add("operator", 0);
			_keywords.Add("extern", 0);
			_keywords.Add("continue", 0);
			_keywords.Add("true", 0);
			_keywords.Add("float", 0);
			_keywords.Add("typedef", 0);
			_keywords.Add("virtual", 0);
			_keywords.Add("typename", 0);
			_keywords.Add("using", 0);
			_keywords.Add("const_cast", 0);
			_keywords.Add("protected", 0);
			_keywords.Add("explicit", 0);
			_keywords.Add("catch", 0);
			_keywords.Add("namespace", 0);
			_keywords.Add("dynamic_cast", 0);
		}

		private bool IsKeyword(string keyword)
		{
			return _keywords.ContainsKey(keyword);
		}

	}
}
