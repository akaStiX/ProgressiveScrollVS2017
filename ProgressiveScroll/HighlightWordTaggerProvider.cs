using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio;

namespace ProgressiveScroll
{
	[Export(typeof(IVsTextViewCreationListener))]
	[Export(typeof(IViewTaggerProvider))]
	[ContentType("code")]
	[TextViewRole(PredefinedTextViewRoles.Editable)]
	[TagType(typeof(TextMarkerTag))]
	internal class HighlightWordTaggerProvider : IViewTaggerProvider, IVsTextViewCreationListener
	{
		[Import]
		internal ITextSearchService TextSearchService { get; set; }

		[Import]
		internal ITextStructureNavigatorSelectorService TextStructureNavigatorSelector { get; set; }

		[Import(typeof(IVsEditorAdaptersFactoryService))]
		internal IVsEditorAdaptersFactoryService _editorFactory = null;

		private static Dictionary<ITextView, HighlightWordTagger> _taggers = new Dictionary<ITextView, HighlightWordTagger>();
		public static Dictionary<ITextView, HighlightWordTagger> Taggers { get { return _taggers; } }

		public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
		{
			//provide highlighting only on the top buffer
			if (textView.TextBuffer != buffer)
				return null;

			ITextStructureNavigator textStructureNavigator =
				TextStructureNavigatorSelector.GetTextStructureNavigator(buffer);

			HighlightWordTagger tagger = new HighlightWordTagger(textView, buffer, TextSearchService, textStructureNavigator);
			Taggers[textView] = tagger;
			return tagger as ITagger<T>;
		}

		public void VsTextViewCreated(IVsTextView textViewAdapter)
		{
			IWpfTextView textView = _editorFactory.GetWpfTextView(textViewAdapter);
			if (textView == null)
				return;

			WordSelectionCommandFilter commandFilter = new WordSelectionCommandFilter(textView);
			Taggers[textView].CommandFilter = commandFilter;

			IOleCommandTarget next;
			if (textViewAdapter != null)
			{
				int hr = textViewAdapter.AddCommandFilter(commandFilter, out next);

				if (hr == VSConstants.S_OK)
				{
					commandFilter._added = true;

					//you'll need the next target for Exec and QueryStatus
					if (next != null)
					{
						commandFilter._nextTarget = next;
					}
				}
			}
		}
	}
}
