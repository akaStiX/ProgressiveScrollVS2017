using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Text.Document;
using System.Windows;

namespace ProgressiveScroll
{
	class ChangeRenderer
	{
		private ITextView _textView;
		private ITagAggregator<ChangeTag> _changeTagAggregator;
		private SimpleScrollBar _scrollBar;

		public ColorSet Colors { get; set; }

		public ChangeRenderer(ITextView textView, ITagAggregator<ChangeTag> changeTagAggregator, SimpleScrollBar scrollBar)
		{
			_textView = textView;
			_changeTagAggregator = changeTagAggregator;
			_scrollBar = scrollBar;
		}

		public void Render(DrawingContext drawingContext)
		{
			NormalizedSnapshotSpanCollection[] allChanges = GetUnifiedChanges(
				_textView.TextSnapshot,
				_changeTagAggregator.GetTags(new SnapshotSpan(_textView.TextSnapshot, 0, _textView.TextSnapshot.Length)));

			DrawChanges(drawingContext, allChanges[(int)ChangeTypes.ChangedSinceOpened], Colors.ChangedBrush);
			DrawChanges(drawingContext, allChanges[(int)(ChangeTypes.ChangedSinceOpened | ChangeTypes.ChangedSinceSaved)], Colors.UnsavedChangedBrush);
		}

		internal static NormalizedSnapshotSpanCollection[] GetUnifiedChanges(ITextSnapshot snapshot, IEnumerable<IMappingTagSpan<ChangeTag>> tags)
		{
			List<SnapshotSpan>[] unnormalizedChanges = new List<SnapshotSpan>[4]
			{
				null,
				new List<SnapshotSpan>(),
				new List<SnapshotSpan>(),
				new List<SnapshotSpan>()
			};

			foreach (IMappingTagSpan<ChangeTag> change in tags)
			{
				unnormalizedChanges[(int)change.Tag.ChangeTypes].AddRange(change.Span.GetSpans(snapshot));
			}

			NormalizedSnapshotSpanCollection[] changes = new NormalizedSnapshotSpanCollection[4];
			for (int i = 1; (i <= 3); ++i)
			{
				changes[i] = new NormalizedSnapshotSpanCollection(unnormalizedChanges[i]);
			}

			return changes;
		}

		private void DrawChanges(DrawingContext drawingContext, NormalizedSnapshotSpanCollection changes, Brush brush)
		{
			if (changes.Count > 0)
			{
				double yTop = Math.Floor(_scrollBar.GetYCoordinateOfBufferPosition(changes[0].Start)) - 2;
				double yBottom = Math.Ceiling(_scrollBar.GetYCoordinateOfBufferPosition(changes[0].End)) + 2;

				for (int i = 1; i < changes.Count; ++i)
				{
					double y = _scrollBar.GetYCoordinateOfBufferPosition(changes[i].Start) - 2;
					if (yBottom < y)
					{
						drawingContext.DrawRectangle(
							brush,
							null,
							new Rect(0, yTop, 3, yBottom - yTop));

						yTop = y;
					}

					yBottom = Math.Ceiling(_scrollBar.GetYCoordinateOfBufferPosition(changes[i].End)) + 2;
				}

				drawingContext.DrawRectangle(
					brush,
					null,
					new Rect(0, yTop, 3, yBottom - yTop));
			}
		}
	}
}
