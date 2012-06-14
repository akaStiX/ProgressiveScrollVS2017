namespace ProgressiveScroll
{
	using System.ComponentModel.Composition;
	using Microsoft.VisualStudio.Text.Editor;
	using Microsoft.VisualStudio.Text.Tagging;
	using Microsoft.VisualStudio.Utilities;
	using System.Collections.Generic;
	using System;
	using Microsoft.VisualStudio.Text.Outlining;
	using Microsoft.VisualStudio.Text.Document;

	/// <summary>
	/// Export a <see cref="IWpfTextViewMarginProvider"/>, which returns an instance of the margin for the editor
	/// to use.
	/// </summary>
	[Export(typeof(IWpfTextViewMarginProvider))]
	[Name(ProgressiveScroll.MarginName)]
	[Order(After = PredefinedMarginNames.VerticalScrollBar)]
	[MarginContainer(PredefinedMarginNames.VerticalScrollBarContainer)]
	[ContentType("code")]
	[TextViewRole(PredefinedTextViewRoles.Editable)]
	internal sealed class ProgressiveScrollFactory : IWpfTextViewMarginProvider
	{
		[Import]
		internal IViewTagAggregatorFactoryService TagAggregatorFactoryService { get; set; }

		[Import]
		internal IScrollMapFactoryService _scrollMapFactory;

		[Import]
		internal IOutliningManagerService _outliningManagerService;

		[Import]
		internal IViewTagAggregatorFactoryService _tagAggregatorFactoryService { get; private set; }

		private SimpleScrollBar _scrollBar;

		public IWpfTextViewMargin CreateMargin(IWpfTextViewHost textViewHost, IWpfTextViewMargin containerMargin)
		{
			IWpfTextViewMargin realScrollBar = containerMargin.GetTextViewMargin(PredefinedMarginNames.VerticalScrollBar) as IWpfTextViewMargin;
			realScrollBar.VisualElement.MinWidth = 0.0;
			realScrollBar.VisualElement.Width = 0.0;

			_scrollBar = new SimpleScrollBar(
				textViewHost.TextView,
				containerMargin,
				_scrollMapFactory,
				true);

			return new ProgressiveScroll(
				textViewHost,
				_outliningManagerService.GetOutliningManager(textViewHost.TextView),
				_tagAggregatorFactoryService.CreateTagAggregator<ChangeTag>(textViewHost.TextView),
				_scrollBar,
				this);
		}
	}
}
