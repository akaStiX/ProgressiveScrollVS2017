using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System.Windows;

namespace ProgressiveScroll
{
	class HighlightRenderer
	{
		private ITextView _textView;
		private SimpleScrollBar _scrollBar;

		public ColorSet Colors { get; set; }

		private static readonly int markerWidth = 5;
		private static readonly int markerStartOffset = -3;
		private static readonly int markerEndOffset = 2;


		public HighlightRenderer(ITextView textView, SimpleScrollBar scrollBar)
		{
			_textView = textView;
			_scrollBar = scrollBar;
		}

		public void Render(DrawingContext drawingContext)
		{
			if (!HighlightWordTaggerProvider.Taggers.ContainsKey(_textView))
			{
				return;
			}

			NormalizedSnapshotSpanCollection spans = new NormalizedSnapshotSpanCollection(new SnapshotSpan(_textView.TextSnapshot, 0, _textView.TextSnapshot.Length));
			IEnumerable<ITagSpan<HighlightWordTag>> tags = HighlightWordTaggerProvider.Taggers[_textView].GetTags(spans);
			List<SnapshotSpan> highlightList = new List<SnapshotSpan>();
			foreach (ITagSpan<HighlightWordTag> highlight in tags)
			{
				highlightList.Add(highlight.Span);
			}

			NormalizedSnapshotSpanCollection highlights = new NormalizedSnapshotSpanCollection(highlightList);

			if (highlights.Count > 0)
			{
				double yTop = Math.Floor(_scrollBar.GetYCoordinateOfBufferPosition(highlights[0].Start)) + markerStartOffset;
				double yBottom = Math.Ceiling(_scrollBar.GetYCoordinateOfBufferPosition(highlights[0].End)) + markerEndOffset;

				for (int i = 1; i < highlights.Count; ++i)
				{
					double y = _scrollBar.GetYCoordinateOfBufferPosition(highlights[i].Start) + markerStartOffset;
					if (yBottom < y)
					{
						drawingContext.DrawRectangle(
							Colors.HighlightsBrush,
							null,
							new Rect(_scrollBar.Width - markerWidth, yTop, markerWidth, yBottom - yTop));

						yTop = y;
					}

					yBottom = Math.Ceiling(_scrollBar.GetYCoordinateOfBufferPosition(highlights[i].End)) + markerEndOffset;
				}

				drawingContext.DrawRectangle(
					Colors.HighlightsBrush,
					null,
					new Rect(_scrollBar.Width - markerWidth, yTop, markerWidth, yBottom - yTop));
			}
		}

	}
}
