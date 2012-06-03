namespace ProgressiveScroll
{
	using System.ComponentModel.Composition;
	using Microsoft.VisualStudio.Text.Editor;
	using Microsoft.VisualStudio.Text.Tagging;
	using Microsoft.VisualStudio.Utilities;
	using System.Collections.Generic;
	using System;
	using Microsoft.VisualStudio.Text.Outlining;

	/// <summary>
	/// Export a <see cref="IWpfTextViewMarginProvider"/>, which returns an instance of the margin for the editor
	/// to use.
	/// </summary>
	[Export(typeof(IWpfTextViewMarginProvider))]
	[Name(ProgressiveScroll.MarginName)]
	[Order(After = PredefinedMarginNames.VerticalScrollBar)]
	[MarginContainer(PredefinedMarginNames.VerticalScrollBarContainer)]
	[ContentType("code")]
	[TextViewRole(PredefinedTextViewRoles.Document)]
	internal sealed class ProgressiveScrollFactory : IWpfTextViewMarginProvider
	{
		[Import]
		internal IViewTagAggregatorFactoryService TagAggregatorFactoryService { get; set; }

		[Import]
		internal IScrollMapFactoryService _scrollMapFactory;

		[Import]
		internal IOutliningManagerService _outliningManagerService;

		private SimpleScrollBar _scrollBar;

		public IWpfTextViewMargin CreateMargin(IWpfTextViewHost textViewHost, IWpfTextViewMargin containerMargin)
		{
			_scrollBar = new SimpleScrollBar(
				textViewHost,
				containerMargin,
				_scrollMapFactory,
				true);

			return new ProgressiveScroll(textViewHost, _outliningManagerService.GetOutliningManager(textViewHost.TextView), _scrollBar, this);
		}
	}
}
