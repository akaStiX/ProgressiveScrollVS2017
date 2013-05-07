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
			_progressiveScroll = progressiveScroll;

			_textRenderer = new TextRenderer(textView, outliningManager, scrollBar);
			_changeRenderer = new ChangeRenderer(textView, changeTagAggregator, scrollBar);
			_highlightRenderer = new HighlightRenderer(textView, scrollBar);
			_markerRenderer = new MarkerRenderer(textView, markerAggregator, errorTagAggregator, debugger, scrollBar);

			TextDirty = true;
		}

	}
}
