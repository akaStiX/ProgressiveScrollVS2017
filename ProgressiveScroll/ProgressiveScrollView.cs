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
	using Microsoft.VisualStudio.Text.Document;
	using System.ComponentModel.Composition;
	using Microsoft.VisualStudio.Text.Classification;
	using Microsoft.VisualStudio.Utilities;
	using Microsoft.VisualStudio.Editor;



	class ProgressiveScrollView
	{
		private readonly IWpfTextView _textView;
		private readonly IVerticalScrollBar _scrollBar;
		private readonly ProgressiveScroll _progressiveScroll;

		private TextRenderer _textRenderer;
		private ChangeRenderer _changeRenderer;
		private HighlightRenderer _highlightRenderer;
		private MarkerRenderer _markerRenderer;

		public bool TextDirty { get; set; }

		private ColorSet _colorSet;

		public ProgressiveScrollView(
			IWpfTextView textView,
			IOutliningManager outliningManager,
			ITagAggregator<ChangeTag> changeTagAggregator,
			ITagAggregator<IVsVisibleTextMarkerTag> markerAggregator,
			IVerticalScrollBar verticalScrollBar,
			ProgressiveScroll progressiveScroll)
		{
			_textView = textView;
			_scrollBar = verticalScrollBar;
			_progressiveScroll = progressiveScroll;

			_textRenderer = new TextRenderer(textView, outliningManager);
			_changeRenderer = new ChangeRenderer(textView, changeTagAggregator, verticalScrollBar);
			_highlightRenderer = new HighlightRenderer(textView, verticalScrollBar);
			_markerRenderer = new MarkerRenderer(textView, markerAggregator, verticalScrollBar);

			TextDirty = true;
		}

		public void Dispose()
		{
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

		public void Render(DrawingContext drawingContext)
		{
			if (!this._textView.IsClosed)
			{
				// Update the color set with the one from the parent class.
				_colorSet = _progressiveScroll.Colors;

				// Update text bitmap if necessary
				if (TextDirty)
				{
					_textRenderer.Colors = _colorSet;
					_textRenderer.Render();
					TextDirty = false;
				}

				// Render the text bitmap with scaling
				Rect rect = new Rect(0.0, 0.0, _progressiveScroll.ActualWidth, Math.Min(_textRenderer.Height, _progressiveScroll.DrawHeight));
				drawingContext.DrawImage(_textRenderer.Bitmap, rect);

				// Render viewport
				int numEditorLines = Math.Max((int)(_textView.ViewportHeight / _textView.LineHeight), 5);
				int firstLine = (int)_scrollBar.GetYCoordinateOfBufferPosition(new SnapshotPoint(_textView.TextViewLines.FirstVisibleLine.Snapshot, _textView.TextViewLines.FirstVisibleLine.Start));

				drawingContext.DrawRectangle(_colorSet.VisibleBrush, null, new Rect(0.0, firstLine, _progressiveScroll.ActualWidth, numEditorLines));

				// Render various marks
				_changeRenderer.Colors = _colorSet;
				_changeRenderer.Render(drawingContext);

				_highlightRenderer.Colors = _colorSet;
				_highlightRenderer.Render(drawingContext);

				_markerRenderer.Colors = _colorSet;
				_markerRenderer.Render(drawingContext);
			}
		}
	}
}
