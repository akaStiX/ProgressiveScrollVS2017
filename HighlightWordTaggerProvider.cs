using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace ProgressiveScroll
{
	[Export(typeof (IViewTaggerProvider))]
	[ContentType("text")]
	[TextViewRole(PredefinedTextViewRoles.Document)]
	[TagType(typeof (ClassificationTag))]
	internal class HighlightWordTaggerProvider : IViewTaggerProvider
	{
		[Import]
		internal ITextSearchService TextSearchService { get; set; }

		[Import]
		internal ITextStructureNavigatorSelectorService TextStructureNavigatorSelector { get; set; }

		[Import]
		public IClassificationTypeRegistryService Registry { get; set; }


		// HACK: Keeps track of all the HighlightWordTaggers
		private static readonly Dictionary<ITextView, HighlightWordTagger> _taggers =
			new Dictionary<ITextView, HighlightWordTagger>();

		public static Dictionary<ITextView, HighlightWordTagger> Taggers
		{
			get { return _taggers; }
		}

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

			IClassificationType classificationType = Registry.GetClassificationType(ClassificationNames.Highlights);

			var tagger = new HighlightWordTagger(textView as IWpfTextView, buffer, TextSearchService, textStructureNavigator, classificationType);
			Taggers[textView] = tagger;
			return tagger as ITagger<T>;
		}
	}
}