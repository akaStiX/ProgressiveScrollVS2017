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
		private readonly SimpleScrollBar _scrollBar;
		private readonly ProgressiveScroll _progressiveScroll;

		private TextRenderer _textRenderer;
		private ChangeRenderer _changeRenderer;
		private HighlightRenderer _highlightRenderer;
		private MarkerRenderer _markerRenderer;

		public bool TextDirty { get; set; }
		public bool CursorBorderEnabled { get; set; }
		public bool RenderTextEnabled { get; set; }
		public MarkerRenderer MarkerRenderer { get { return _markerRenderer; } }

		private ColorSet _colorSet;

		public ProgressiveScrollView(
			IWpfTextView textView,
			IOutliningManager outliningManager,
			ITagAggregator<ChangeTag> changeTagAggregator,
			ITagAggregator<IVsVisibleTextMarkerTag> markerAggregator,
			ITagAggregator<IErrorTag> errorTagAggregator,
			EnvDTE.Debugger debugger,
			SimpleScrollBar scrollBar,
			ProgressiveScroll progressiveScroll)
		{
			_textView = textView;
			_scrollBar = scrollBar;
			_progressiveScroll = progressiveScroll;

			_textRenderer = new TextRenderer(textView, outliningManager, scrollBar);
			_changeRenderer = new ChangeRenderer(textView, changeTagAggregator, scrollBar);
			_highlightRenderer = new HighlightRenderer(textView, scrollBar);
			_markerRenderer = new MarkerRenderer(textView, markerAggregator, errorTagAggregator, debugger, scrollBar);

			TextDirty = true;
		}

		public void Dispose()
		{
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
				double textHeight = Math.Min(_textRenderer.Height, _progressiveScroll.DrawHeight);
				if (RenderTextEnabled)
				{
					Rect rect = new Rect(0.0, 0.0, _progressiveScroll.ActualWidth, textHeight);
					drawingContext.DrawImage(_textRenderer.Bitmap, rect);
				}

				// Render viewport
				int viewportHeight = Math.Max((int)((_textView.ViewportHeight / _textView.LineHeight) * (textHeight / _textRenderer.Height)), 5);
				int firstLine = (int)_scrollBar.GetYCoordinateOfBufferPosition(new SnapshotPoint(_textView.TextViewLines.FirstVisibleLine.Snapshot, _textView.TextViewLines.FirstVisibleLine.Start));

				drawingContext.DrawRectangle(_colorSet.VisibleBrush, null, new Rect(0.0, firstLine, _progressiveScroll.ActualWidth, viewportHeight));

				// Render various marks
				_changeRenderer.Colors = _colorSet;
				_changeRenderer.Render(drawingContext);

				_highlightRenderer.Colors = _colorSet;
				_highlightRenderer.Render(drawingContext);

				_markerRenderer.Colors = _colorSet;
				_markerRenderer.Render(drawingContext);

				if (CursorBorderEnabled)
				{
					drawingContext.DrawRectangle(null, _colorSet.VisibleBorderPen, new Rect(0.5, firstLine + 0.5, _progressiveScroll.ActualWidth - 1.0, viewportHeight - 1.0));
				}
			}
		}
	}
}
