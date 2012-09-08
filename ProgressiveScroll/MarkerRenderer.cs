using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text;
using System.Windows;

namespace ProgressiveScroll
{
	class MarkerRenderer
	{
		private ITextView _textView;
		private ITagAggregator<IVsVisibleTextMarkerTag> _markerTagAggregator;
		private SimpleScrollBar _scrollBar;

		public ColorSet Colors { get; set; }

		private static readonly int _breakpointType = 73;
		private static readonly int _bookmarkType = 3;

		public MarkerRenderer(ITextView textView, ITagAggregator<IVsVisibleTextMarkerTag> markerTagAggregator, SimpleScrollBar scrollBar)
		{
			_textView = textView;
			_markerTagAggregator = markerTagAggregator;
			_scrollBar = scrollBar;
		}

		public void Render(DrawingContext drawingContext)
		{
			NormalizedSnapshotSpanCollection[] markers = GetMarkers(
				_textView.TextSnapshot,
				_markerTagAggregator.GetTags(new SnapshotSpan(_textView.TextSnapshot, 0, _textView.TextSnapshot.Length)));

			DrawMarkers(drawingContext, markers[0], Colors.BookmarkBrush);
			DrawMarkers(drawingContext, markers[1], Colors.BreakpointBrush);
		}

		internal static NormalizedSnapshotSpanCollection[] GetMarkers(ITextSnapshot snapshot, IEnumerable<IMappingTagSpan<IVsVisibleTextMarkerTag>> tags)
		{
			List<SnapshotSpan>[] unnormalizedmarkers = new List<SnapshotSpan>[2]
			{
				new List<SnapshotSpan>(),
				new List<SnapshotSpan>()
			};

			foreach (IMappingTagSpan<IVsVisibleTextMarkerTag> tag in tags)
			{
				if (tag.Tag.Type == _bookmarkType)
				{
					unnormalizedmarkers[0].AddRange(tag.Span.GetSpans(snapshot));
				}
				else
				if (tag.Tag.Type == _breakpointType)
				{
					unnormalizedmarkers[1].AddRange(tag.Span.GetSpans(snapshot));
				}
			}

			NormalizedSnapshotSpanCollection[] markers = new NormalizedSnapshotSpanCollection[2];
			for (int i = 0; i < 2; ++i)
			{
				markers[i] = new NormalizedSnapshotSpanCollection(unnormalizedmarkers[i]);
			}

			return markers;
		}

		private void DrawMarkers(DrawingContext drawingContext, NormalizedSnapshotSpanCollection markers, Brush brush)
		{
			if (markers.Count > 0)
			{
				double yTop = Math.Floor(_scrollBar.GetYCoordinateOfBufferPosition(markers[0].Start)) - 3;
				double yBottom = Math.Ceiling(_scrollBar.GetYCoordinateOfBufferPosition(markers[0].End)) + 2;

				for (int i = 1; i < markers.Count; ++i)
				{
					double y = _scrollBar.GetYCoordinateOfBufferPosition(markers[i].Start) - 3;
					if (yBottom < y)
					{
						drawingContext.DrawRectangle(
							brush,
							null,
							new Rect(_scrollBar.Width - 3, yTop, 3, yBottom - yTop));

						yTop = y;
					}

					yBottom = Math.Ceiling(_scrollBar.GetYCoordinateOfBufferPosition(markers[i].End)) + 2;
				}

				drawingContext.DrawRectangle(
					brush,
					null,
					new Rect(_scrollBar.Width - 5, yTop, 5, yBottom - yTop));
			}
		}
	}
}
