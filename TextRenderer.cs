using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using System.Diagnostics;
using Microsoft.VisualStudio.Text.Outlining;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace ProgressiveScroll
{
	class TextRenderer
	{
		private readonly ProgressiveScroll _progressiveScroll;
		private readonly ITextView _textView;
		private readonly IOutliningManager _outliningManager;

		public DrawingVisual TextVisual { get; private set; }

		private Thread _thread;
		private bool _invalidateAgain;
		private bool _finishedDrawing;

		// We render the text to a bitmap with a maximum height.
		private const int MaxBitmapHeight = 2000;

		private readonly PixelFormat _pixelFormat = PixelFormats.Bgra32;
		private int _bitmapWidth;
		private int _bitmapHeight;
		private int _bitmapStride;
		private byte[] _bitmapPixels;

		private int _width;
		private int _height;
		private int _stride;
		private byte[] _pixels;

		private double _lineRatio;

		private Object l = new Object();
		private BitmapSource _bitmap;


		public int Height { get { return _bitmapHeight; } }
		public int NumLines { get { return _textView.VisualSnapshot.LineCount; } }

		static private Dictionary<string, byte> _keywords = new Dictionary<string, byte>
		{
			{ "__alignof", 0 }, { "__asm", 0 },
			{ "bool", 0 }, { "break", 0 },
			{ "case", 0 }, { "catch", 0 }, { "char", 0 }, { "class", 0 }, { "const", 0 }, { "const_cast", 0 }, { "continue", 0 },
			{ "default", 0 }, { "delete", 0 }, { "do", 0 }, { "double", 0 }, { "dynamic_cast", 0 },
			{ "else", 0 }, { "enum", 0 }, { "explicit", 0 }, { "extern", 0 },
			{ "false", 0 }, { "float", 0 }, { "for", 0 }, { "friend", 0 },
			{ "goto", 0 },
			{ "if", 0 }, { "inline", 0 }, { "int", 0 },
			{ "long", 0 },
			{ "mutable", 0 },
			{ "namespace", 0 }, { "new", 0 },
			{ "operator", 0 },
			{ "private", 0 }, { "protected", 0 }, { "public", 0 },
			{ "register", 0 }, { "reinterpret_cast", 0 }, { "return", 0 },
			{ "short", 0 }, { "signed", 0 }, { "sizeof", 0 }, { "static", 0 }, { "static_cast", 0 }, { "struct", 0 }, { "switch", 0 },
			{ "template", 0 }, { "this", 0 }, { "throw", 0 }, { "true", 0 }, { "try", 0 }, { "typedef", 0 }, { "typename", 0 },
			{ "union", 0 }, { "unsigned", 0 }, { "using", 0 },
			{ "void", 0 }, { "volatile", 0 }, { "virtual", 0 },
			{ "wchar_t", 0 }, { "while", 0 },
		};

		enum CommentType
		{
			None,
			SingleLine,
			MultiLine
		};

		public TextRenderer(ProgressiveScroll progressiveScroll, ITextView textView, IOutliningManager outliningManager)
		{
			_progressiveScroll = progressiveScroll;
			TextVisual = new DrawingVisual();

			_textView = textView;
			_outliningManager = outliningManager;
			_bitmapWidth = Options.ScrollBarWidth;
			_bitmapHeight = 0;
			_bitmapStride = (_bitmapWidth * _pixelFormat.BitsPerPixel + 7) / 8;
			_bitmapPixels = null;

			_width = _bitmapWidth;
			_height = _bitmapHeight;
			_stride = _bitmapStride;
			_pixels = null;

			_lineRatio = 1.0;
		}

		public void Invalidate(Parts parts)
		{
			if (parts.HasFlag(Parts.TextContent))
			{
				if (_thread == null)
				{
					_invalidateAgain = false;
					_finishedDrawing = false;
					_thread = new Thread(RenderAsync);
					_thread.Name = "Progressive Scroll Text Render";
					_thread.Priority = ThreadPriority.Lowest;
					_thread.Start();
				}
				else
				{
					// The thread is still busy rendering from the last update
					_invalidateAgain = true;
				}
			}
			else
			if (parts.HasFlag(Parts.Text))
			{
				// If there's a background thread and it's still drawing, Render will be called anyway.
				if (_thread == null || _finishedDrawing)
				{
					TextVisual.Dispatcher.BeginInvoke(new Action<bool>(Render), DispatcherPriority.Render, false);
				}
			}
		}

		private void Render(bool bitmapDirty)
		{
			if (bitmapDirty)
			{
				lock (l)
				{
					_bitmap = BitmapSource.Create(
						_bitmapWidth,
						_bitmapHeight,
						96,
						96,
						_pixelFormat,
						null,
						_bitmapPixels,
						_bitmapStride);
				}
			}

			// Render the text bitmap with scaling
			DrawingGroup drawingGroup = new DrawingGroup();
			RenderOptions.SetBitmapScalingMode(drawingGroup, BitmapScalingMode.HighQuality);
			ImageDrawing image = new ImageDrawing();
			double textHeight = Math.Min(Height, _progressiveScroll.DrawHeight);
			image.Rect = new Rect(0.0, 0.0, _progressiveScroll.ActualWidth, textHeight);
			image.ImageSource = _bitmap;
			drawingGroup.Children.Add(image);

			using (DrawingContext drawingContext = TextVisual.RenderOpen())
			{
				drawingContext.DrawDrawing((Drawing)drawingGroup);
			}

			if (_invalidateAgain)
			{
				Invalidate(Parts.TextContent);
			}
		}

		public void RenderAsync()
		{
			try
			{
				DrawLines();

				lock (l)
				{
					_bitmapWidth = _width;
					_bitmapHeight = _height;
					_bitmapStride = _stride;
					_bitmapPixels = _pixels;
				}

				_finishedDrawing = true;

				// We call Render synchronously so we don't update pixels before it's done updating the bitmap
				TextVisual.Dispatcher.Invoke(new Action<bool>(Render), DispatcherPriority.Render, true);
			}
			catch (Exception)
			{
				// Something went wrong, possibly the textview was disposed of.
			}

			_thread = null;
		}

		public void DrawLines()
		{
			// Find the hidden regions
			IEnumerable<ICollapsed> collapsedRegions = new List<ICollapsed>();
			if (_outliningManager != null)
			{
				collapsedRegions = _outliningManager.GetCollapsedRegions(new SnapshotSpan(_textView.TextBuffer.CurrentSnapshot, new Span(0, _textView.TextBuffer.CurrentSnapshot.Length)));
			}
			IEnumerator<ICollapsed> currentCollapsedRegion = collapsedRegions.GetEnumerator();
			SnapshotSpan? currentCollapsedSnapshotSpan = null;
			if (currentCollapsedRegion.MoveNext())
			{
				currentCollapsedSnapshotSpan = currentCollapsedRegion.Current.Extent.GetSpan(_textView.TextBuffer.CurrentSnapshot);
			}

			// Get the highlights
			List<SnapshotSpan> highlightList = new List<SnapshotSpan>();

			if (HighlightWordTaggerProvider.Taggers.ContainsKey(_textView))
			{
				IEnumerable<ITagSpan<HighlightWordTag>> highlightsEnumerable = HighlightWordTaggerProvider.Taggers[_textView].GetTags(new NormalizedSnapshotSpanCollection(new SnapshotSpan(_textView.TextSnapshot, 0, _textView.TextSnapshot.Length)));

				foreach (ITagSpan<HighlightWordTag> highlight in highlightsEnumerable)
				{
					highlightList.Add(highlight.Span);
				}
			}

			NormalizedSnapshotSpanCollection highlights = new NormalizedSnapshotSpanCollection(highlightList);
			int highlightIndex = 0;

			// Create the image buffer
			_width = Options.ScrollBarWidth;
			_height = Math.Min(NumLines, MaxBitmapHeight);
			_stride = (_width * _pixelFormat.BitsPerPixel + 7) / 8;
			_pixels = new byte[_stride * _height];
			_lineRatio = (double)(_height) / (double)(NumLines);

			string text = _textView.TextBuffer.CurrentSnapshot.GetText();

			int wrapAfter = int.MaxValue;
			int tabSize = 4;

			CommentType commentType = CommentType.None;
			int multiLineCommentStart = -1;

			bool inKeyword = false;
			bool inString = false;

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
						if ((text[i] == '\r') && (i + 1 < text.Length) && (text[i + 1] == '\n'))
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
								multiLineCommentStart = i;
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
							if (text.Substring(i - 1, 2) == "*/" &&
								i > multiLineCommentStart + 2) // Make sure we don't detect "/*/" as opening and closing a comment.
							{
								commentType = CommentType.None;
							}
						}
						break;
					}

					while (highlightIndex < highlights.Count && i >= highlights[highlightIndex].End.Position)
					{
						++highlightIndex;
					}

					if (isLineVisible)
					{
						if (highlightIndex < highlights.Count && i >= highlights[highlightIndex].Start.Position)
						{
							SetPixel(virtualColumn, virtualLine, _progressiveScroll.Colors.HighlightsBrush.Color);
						}
						else if (commentType != CommentType.None)
						{
							SetPixel(virtualColumn, virtualLine, _progressiveScroll.Colors.CommentsBrush.Color);
						}
						else if (inString)
						{
							SetPixel(virtualColumn, virtualLine, _progressiveScroll.Colors.StringsBrush.Color);
						}
						else
						{
							SetPixel(virtualColumn, virtualLine, _progressiveScroll.Colors.TextBrush.Color);
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
						SetPixels(virtualColumn, virtualLine, _progressiveScroll.Colors.WhitespaceBrush.Color, numChars);
					}
				}

				++realColumn;
				virtualColumn += numChars;
			}
		}

		private void SetPixel(int x, int y, Color c)
		{
			// Not entirely accurate, some pixels should be split between lines, but close enough
			y = (int)(y * _lineRatio);
			if (x < _width && y < _height)
			{
				int pixelOffset = y * _stride + x * 4;
				_pixels[pixelOffset] += (byte)(_lineRatio * c.B);
				_pixels[pixelOffset + 1] += (byte)(_lineRatio * c.G);
				_pixels[pixelOffset + 2] += (byte)(_lineRatio * c.R);
				_pixels[pixelOffset + 3] += (byte)(_lineRatio * 255);
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

		private bool IsKeyword(string keyword)
		{
			return _keywords.ContainsKey(keyword);
		}
	}
}
