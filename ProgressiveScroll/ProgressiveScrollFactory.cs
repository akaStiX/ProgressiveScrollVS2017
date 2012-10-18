﻿namespace ProgressiveScroll
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

		public static bool IsVS11 { get; set; }

		public static readonly List<string> RejectedRoles = new List<string>() { "DIFF", "VSMERGEDEFAULT" };

		public IWpfTextViewMargin CreateMargin(IWpfTextViewHost textViewHost, IWpfTextViewMargin containerMargin)
		{
			if (textViewHost.TextView.Roles.ContainsAny(RejectedRoles))
			{
				return null;
			}

			// Get the width from the options
			DTE dte = (DTE)ServiceProvider.GetService(typeof(DTE));
			IsVS11 = (dte.Version == "11.0");
			EnvDTE.Properties props =
				dte.get_Properties(OptionNames.PageCategoryName, OptionNames.PageName);

			EnvDTE.Debugger debugger = (EnvDTE.Debugger)dte.Debugger;

			int width = (int)props.Item(OptionNames.ScrollBarWidth).Value;
			double cursorOpacity = (double)props.Item(OptionNames.CursorOpacity).Value;

			// Hide the real scroll bar
			IWpfTextViewMargin realScrollBar = containerMargin.GetTextViewMargin(PredefinedMarginNames.VerticalScrollBar) as IWpfTextViewMargin;
			realScrollBar.VisualElement.MinWidth = 0.0;
			realScrollBar.VisualElement.Width = 0.0;

			SimpleScrollBar scrollBar = new SimpleScrollBar(
				textViewHost.TextView,
				containerMargin,
				_scrollMapFactory,
				width);

			ProgressiveScroll progressiveScroll = new ProgressiveScroll(
				textViewHost,
				_outliningManagerService.GetOutliningManager(textViewHost.TextView),
				_tagAggregatorFactoryService.CreateTagAggregator<ChangeTag>(textViewHost.TextView),
				_tagAggregatorFactoryService.CreateTagAggregator<IVsVisibleTextMarkerTag>(textViewHost.TextView),
				_tagAggregatorFactoryService.CreateTagAggregator<IErrorTag>(textViewHost.TextView),
				debugger,
				scrollBar,
				this);

			progressiveScroll.Colors = new ColorSet(textViewHost.TextView, FormatMapService, cursorOpacity);
			progressiveScroll.ScrollView.RenderTextEnabled = (bool)props.Item(OptionNames.RenderTextEnabled).Value;
			progressiveScroll.ScrollView.CursorBorderEnabled = (bool)props.Item(OptionNames.CursorBorderEnabled).Value;
			progressiveScroll.SplitterEnabled = (bool)props.Item(OptionNames.SplitterEnabled).Value;
			scrollBar.SplitterEnabled = (bool)props.Item(OptionNames.SplitterEnabled).Value;
			progressiveScroll.ScrollView.MarkerRenderer.ErrorsEnabled = (bool)props.Item(OptionNames.ErrorsEnabled).Value;

			return progressiveScroll;
		}
	}
}
