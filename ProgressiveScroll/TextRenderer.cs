using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
		private ITextView _textView;
		private IOutliningManager _outliningManager;
		private SimpleScrollBar _scrollBar;

		private int _width;
		private int _height;
		private int _stride;
		private readonly PixelFormat _pf = PixelFormats.Rgb24;
		private byte[] _pixels;

		public BitmapSource Bitmap { get; private set; }
		public ColorSet Colors { get; set; }
		public int Height { get { return _height; } }

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

		public TextRenderer(ITextView textView, IOutliningManager outliningManager, SimpleScrollBar scrollBar)
		{
			_textView = textView;
			_outliningManager = outliningManager;
			_scrollBar = scrollBar;
			_width = scrollBar.Width;
			_height = 0;
			_stride = (_width * _pf.BitsPerPixel + 7) / 8;
			_pixels = null;
		}

		public void Render()
		{
			// Find the hidden regions
			IEnumerable<ICollapsed> collapsedRegions = _outliningManager.GetCollapsedRegions(new SnapshotSpan(_textView.TextBuffer.CurrentSnapshot, new Span(0, _textView.TextBuffer.CurrentSnapshot.Length)));
			IEnumerator<ICollapsed> currentCollapsedRegion = collapsedRegions.GetEnumerator();
			SnapshotSpan? currentCollapsedSnapshotSpan = null;
			if (currentCollapsedRegion.MoveNext())
			{
				currentCollapsedSnapshotSpan = currentCollapsedRegion.Current.Extent.GetSpan(_textView.TextBuffer.CurrentSnapshot);
			}

			// Get the highlights
			IEnumerable<ITagSpan<HighlightWordTag>> highlightsEnumerable = HighlightWordTaggerProvider.Taggers[_textView].GetTags(new NormalizedSnapshotSpanCollection(new SnapshotSpan(_textView.TextSnapshot, 0, _textView.TextSnapshot.Length)));
			List<SnapshotSpan> highlightList = new List<SnapshotSpan>();
			foreach (ITagSpan<HighlightWordTag> highlight in highlightsEnumerable)
			{
				highlightList.Add(highlight.Span);
			}
			NormalizedSnapshotSpanCollection highlights = new NormalizedSnapshotSpanCollection(highlightList);
			int highlightIndex = 0;

			// Create the image buffer
			_width = _scrollBar.Width;
			_stride = (_width * _pf.BitsPerPixel + 7) / 8;
			_height = _textView.VisualSnapshot.LineCount;
			_pixels = new byte[_stride * _height];

			// Clear the image buffer with the whitespace color
			Clear();

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
							SetPixel(virtualColumn, virtualLine, Colors.HighlightBrush.Color);
						}
						else if (commentType != CommentType.None)
						{
							SetPixel(virtualColumn, virtualLine, Colors.CommentBrush.Color);
						}
						else if (inString)
						{
							SetPixel(virtualColumn, virtualLine, Colors.StringBrush.Color);
						}
						else
						{
							SetPixel(virtualColumn, virtualLine, Colors.TextBrush.Color);
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
						SetPixels(virtualColumn, virtualLine, Colors.WhitespaceBrush.Color, numChars);
					}
				}

				++realColumn;
				virtualColumn += numChars;
			}

			Bitmap = BitmapSource.Create(
				_width,
				_height,
				96,
				96,
				_pf,
				null,
				_pixels,
				_stride);
		}

		private void Clear()
		{
			int numPixels = _height * _stride / (_pf.BitsPerPixel / 8);
			Color clearColor = Colors.WhitespaceBrush.Color;
			for (int i = 0; i < numPixels; ++i)
			{
				_pixels[3 * i] = clearColor.R;
				_pixels[3 * i + 1] = clearColor.G;
				_pixels[3 * i + 2] = clearColor.B;
			}
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

		private bool IsKeyword(string keyword)
		{
			return _keywords.ContainsKey(keyword);
		}
	}
}
