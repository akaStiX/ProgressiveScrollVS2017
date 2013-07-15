using System.Windows;
using Microsoft.VisualStudio.Text.Operations;

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
	using Microsoft.VisualStudio.Text.Classification;
	using Microsoft.VisualStudio.Editor;
	using EnvDTE;
	using Microsoft.VisualStudio.Shell;

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
		internal IScrollMapFactoryService _scrollMapFactory { get; set; }

		[Import]
		internal IOutliningManagerService _outliningManagerService { get; set; }

		[Import]
		internal IViewTagAggregatorFactoryService _tagAggregatorFactoryService { get; private set; }

		[Import]
		internal IEditorFormatMapService FormatMapService { get; set; }

		private IServiceProvider ServiceProvider { get; set; }

		[ImportingConstructor]
		private ProgressiveScrollFactory([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider)
		{
			ServiceProvider = serviceProvider;
		}

		public static readonly List<string> RejectedRoles = new List<string>() { "DIFF", "VSMERGEDEFAULT" };

		public IWpfTextViewMargin CreateMargin(IWpfTextViewHost textViewHost, IWpfTextViewMargin containerMargin)
		{
			if (textViewHost.TextView.Roles.ContainsAny(RejectedRoles))
			{
				return null;
			}

			DTE dte = (DTE) ServiceProvider.GetService(typeof (DTE));

			// Hide the real scroll bar
			IWpfTextViewMargin realScrollBar =
				containerMargin.GetTextViewMargin(PredefinedMarginNames.VerticalScrollBar) as IWpfTextViewMargin;
			if (realScrollBar != null)
			{
				realScrollBar.VisualElement.MinWidth = 0.0;
				realScrollBar.VisualElement.Width = 0.0;
			}

			IWpfTextView textView = textViewHost.TextView;

			SimpleScrollBar scrollBar = new SimpleScrollBar(
				textView,
				containerMargin,
				_scrollMapFactory);

			ProgressiveScroll progressiveScroll = new ProgressiveScroll(
				containerMargin,
				textView,
				_outliningManagerService.GetOutliningManager(textView),
				_tagAggregatorFactoryService.CreateTagAggregator<ChangeTag>(textView),
				_tagAggregatorFactoryService.CreateTagAggregator<IVsVisibleTextMarkerTag>(textView),
				_tagAggregatorFactoryService.CreateTagAggregator<IErrorTag>(textView),
				dte.Debugger,
				scrollBar,
				new ColorSet(FormatMapService.GetEditorFormatMap(textView)));

			return progressiveScroll;
		}
	}
}
