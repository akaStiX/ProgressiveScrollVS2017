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
using System.IO;

namespace ProgressiveScroll
{
	class MarkerRenderer
	{
		private ITextView _textView;
		private ITagAggregator<IVsVisibleTextMarkerTag> _markerTagAggregator;
		private EnvDTE.Debugger _debugger;
		private SimpleScrollBar _scrollBar;
		private string _filename;

		public ColorSet Colors { get; set; }

		private static readonly int markerWidth = 5;
		private static readonly int markerStartOffset = -3;
		private static readonly int markerEndOffset = 2;

		private static int _bookmarkType = 3;

		public MarkerRenderer(ITextView textView, ITagAggregator<IVsVisibleTextMarkerTag> markerTagAggregator, EnvDTE.Debugger debugger, SimpleScrollBar scrollBar)
		{
			_textView = textView;
			_markerTagAggregator = markerTagAggregator;
			_debugger = debugger;
			_scrollBar = scrollBar;

			// ... Pretty convoluted way to get the filename:
			ITextDocument doc;
			bool success = textView.TextBuffer.Properties.TryGetProperty<ITextDocument>(typeof(ITextDocument), out doc);
			if (success)
			{
				_filename = doc.FilePath;
			}
			else
			{
				_filename = "";
			}
		}

		public void Render(DrawingContext drawingContext)
		{
			NormalizedSnapshotSpanCollection bookmarks = GetBookmarks(
				_textView.TextSnapshot,
				_markerTagAggregator.GetTags(new SnapshotSpan(_textView.TextSnapshot, 0, _textView.TextSnapshot.Length)));

			DrawMarkers(drawingContext, bookmarks, Colors.BookmarkBrush);

			NormalizedSnapshotSpanCollection breakpoints = GetBreakpoints();
			DrawMarkers(drawingContext, breakpoints, Colors.BreakpointBrush);
		}

		internal NormalizedSnapshotSpanCollection GetBreakpoints()
		{
			List<SnapshotSpan> unnormalizedBreakpoints = new List<SnapshotSpan>();

			foreach (EnvDTE.Breakpoint bp in _debugger.Breakpoints)
			{
				if (bp.LocationType == EnvDTE.dbgBreakpointLocationType.dbgBreakpointLocationTypeFile &&
					bp.File == _filename)
				{
					unnormalizedBreakpoints.Add(_textView.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(bp.FileLine).Extent);
				}
			}

			return new NormalizedSnapshotSpanCollection(unnormalizedBreakpoints);
		}

		internal static NormalizedSnapshotSpanCollection GetBookmarks(ITextSnapshot snapshot, IEnumerable<IMappingTagSpan<IVsVisibleTextMarkerTag>> tags)
		{
			List<SnapshotSpan> unnormalizedBookmarks = new List<SnapshotSpan>();

			foreach (IMappingTagSpan<IVsVisibleTextMarkerTag> tag in tags)
			{
				if (tag.Tag.Type == _bookmarkType)
				{
					unnormalizedBookmarks.AddRange(tag.Span.GetSpans(snapshot));
				}
			}

			return new NormalizedSnapshotSpanCollection(unnormalizedBookmarks);
		}

		private void DrawMarkers(DrawingContext drawingContext, NormalizedSnapshotSpanCollection markers, Brush brush)
		{
			if (markers.Count > 0)
			{
				double yTop = Math.Floor(_scrollBar.GetYCoordinateOfBufferPosition(markers[0].Start)) + markerStartOffset;
				double yBottom = Math.Ceiling(_scrollBar.GetYCoordinateOfBufferPosition(markers[0].End)) + markerEndOffset;

				for (int i = 1; i < markers.Count; ++i)
				{
					double y = _scrollBar.GetYCoordinateOfBufferPosition(markers[i].Start) + markerStartOffset;
					if (yBottom < y)
					{
						drawingContext.DrawRectangle(
							brush,
							null,
							new Rect(_scrollBar.Width - markerWidth, yTop, markerWidth, yBottom - yTop));

						yTop = y;
					}

					yBottom = Math.Ceiling(_scrollBar.GetYCoordinateOfBufferPosition(markers[i].End)) + markerEndOffset;
				}

				drawingContext.DrawRectangle(
					brush,
					null,
					new Rect(_scrollBar.Width - markerWidth, yTop, markerWidth, yBottom - yTop));
			}
		}
	}
}
