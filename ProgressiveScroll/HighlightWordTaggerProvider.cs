using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace ProgressiveScroll
{
	[Export(typeof (IVsTextViewCreationListener))]
	[Export(typeof (IViewTaggerProvider))]
	[ContentType("code")]
	[TextViewRole(PredefinedTextViewRoles.Document)]
	[TagType(typeof (ClassificationTag))]
	internal class HighlightWordTaggerProvider : IViewTaggerProvider, IVsTextViewCreationListener
	{
		[Import(typeof (IVsEditorAdaptersFactoryService))]
		internal IVsEditorAdaptersFactoryService EditorFactory = null;

		[Import]
		internal ITextSearchService TextSearchService { get; set; }

		[Import]
		internal ITextStructureNavigatorSelectorService TextStructureNavigatorSelector { get; set; }

		[Import]
		public IClassificationTypeRegistryService Registry { get; set; }



		private static readonly Dictionary<ITextView, HighlightWordTagger> _taggers =
			new Dictionary<ITextView, HighlightWordTagger>();

		public static Dictionary<ITextView, HighlightWordTagger> Taggers
		{
			get { return _taggers; }
		}

		#region IViewTaggerProvider Members

		public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
		{
			// Provide highlighting only on the top buffer
			// HTML files for instance will fail here
			if (textView.TextBuffer != buffer)
				return null;

			// This checks for diff/merge type views
			if (textView.Roles.ContainsAny(ProgressiveScrollFactory.RejectedRoles))
			{
				return null;
			}

			ITextStructureNavigator textStructureNavigator =
				TextStructureNavigatorSelector.GetTextStructureNavigator(buffer);

			IClassificationType classificationType = Registry.GetClassificationType("PSHighlightWordFormatDefinition");

			var tagger = new HighlightWordTagger(textView, buffer, TextSearchService, textStructureNavigator, classificationType);
			Taggers[textView] = tagger;
			return tagger as ITagger<T>;
		}

		#endregion

		#region IVsTextViewCreationListener Members

		public void VsTextViewCreated(IVsTextView textViewAdapter)
		{
			IWpfTextView textView = EditorFactory.GetWpfTextView(textViewAdapter);
			if (textView == null)
				return;

			var commandFilter = new WordSelectionCommandFilter(textView);
			Taggers[textView].CommandFilter = commandFilter;

			IOleCommandTarget next;

			if (textViewAdapter == null)
				return;

			int hr = textViewAdapter.AddCommandFilter(commandFilter, out next);

			if (hr != VSConstants.S_OK)
				return;

			commandFilter._added = true;

			//you'll need the next target for Exec and QueryStatus
			if (next != null)
			{
				commandFilter._nextTarget = next;
			}
		}

		#endregion
	}
}