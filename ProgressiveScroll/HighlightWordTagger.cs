using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;

namespace ProgressiveScroll
{
	internal class HighlightWordTagger : ITagger<HighlightWordTag>
	{
		private readonly object _updateLock = new object();

		public HighlightWordTagger(
			ITextView view,
			ITextBuffer sourceBuffer,
			ITextSearchService textSearchService,
			ITextStructureNavigator textStructureNavigator,
			IClassificationType classificationType)
		{
			View = view;
			SourceBuffer = sourceBuffer;
			TextSearchService = textSearchService;
			TextStructureNavigator = textStructureNavigator;
			ClassificationType = classificationType;
			WordSpans = new NormalizedSnapshotSpanCollection();
			CurrentWord = null;
			View.Selection.SelectionChanged += SelectionChanged;
		}

		private ITextView View { get; set; }
		private ITextBuffer SourceBuffer { get; set; }
		private ITextSearchService TextSearchService { get; set; }
		private ITextStructureNavigator TextStructureNavigator { get; set; }
		private IClassificationType ClassificationType { get; set; }
		private NormalizedSnapshotSpanCollection WordSpans { get; set; }
		private SnapshotSpan? CurrentWord { get; set; }
		private SnapshotPoint RequestedPoint { get; set; }

		private HighlightWordCommand _command;

		public HighlightWordCommand Command
		{
			get { return _command; }
			set
			{
				if (_command != null)
				{
					_command.CommandChanged -= SelectionChanged;
				}

				_command = value;
				_command.CommandChanged += SelectionChanged;
			}
		}

		#region ITagger<HighlightWordTag> Members

		public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

		public IEnumerable<ITagSpan<HighlightWordTag>> GetTags(NormalizedSnapshotSpanCollection spans)
		{
			if (CurrentWord == null)
				yield break;

			// Hold on to a "snapshot" of the word spans and current word, so that we maintain the same
			// collection throughout
			SnapshotSpan currentWord = CurrentWord.Value;
			NormalizedSnapshotSpanCollection wordSpans = WordSpans;

			if (spans.Count == 0 || WordSpans.Count == 0)
				yield break;

			// If the requested snapshot isn't the same as the one our words are on, translate our spans to the expected snapshot
			if (spans[0].Snapshot != wordSpans[0].Snapshot)
			{
				wordSpans = new NormalizedSnapshotSpanCollection(
					wordSpans.Select(span => span.TranslateTo(spans[0].Snapshot, SpanTrackingMode.EdgeExclusive)));

				currentWord = currentWord.TranslateTo(spans[0].Snapshot, SpanTrackingMode.EdgeExclusive);
			}

			// First, yield back the word the cursor is under (if it overlaps)
			// Note that we'll yield back the same word again in the wordspans collection;
			// the duplication here is expected.
			if (spans.OverlapsWith(new NormalizedSnapshotSpanCollection(currentWord)))
				yield return new TagSpan<HighlightWordTag>(currentWord, new HighlightWordTag(ClassificationType));

			// Second, yield all the other words in the file
			foreach (SnapshotSpan span in NormalizedSnapshotSpanCollection.Overlap(spans, wordSpans))
			{
				yield return new TagSpan<HighlightWordTag>(span, new HighlightWordTag(ClassificationType));
			}
		}

		#endregion

		public void Clear()
		{
			RequestedPoint = new SnapshotPoint();
			SynchronousUpdate(RequestedPoint, new NormalizedSnapshotSpanCollection(), new SnapshotSpan?());
		}

		private void SelectionChanged(object sender, EventArgs e)
		{
			if (Command != null)
			{
				if (Command.Selected)
				{
					UpdateSelection(View.Caret.Position);
				}
				else
				if (Command.Unselected)
				{
					SynchronousUpdate(RequestedPoint, new NormalizedSnapshotSpanCollection(), null);
				}
			}
		}

		private void UpdateSelection(CaretPosition caretPosition)
		{
			SnapshotPoint? point = caretPosition.Point.GetPoint(SourceBuffer, caretPosition.Affinity);

			if (!point.HasValue)
				return;

			// If the new caret position is still within the current word (and on the same snapshot), we don't need to check it
			if (CurrentWord.HasValue
				&& CurrentWord.Value.Snapshot == View.TextSnapshot
				&& point.Value >= CurrentWord.Value.Start
				&& point.Value <= CurrentWord.Value.End)
			{
				return;
			}

			RequestedPoint = point.Value;
			UpdateWordAdornments();
		}

		private void UpdateWordAdornments()
		{
			SnapshotPoint currentRequest = RequestedPoint;
			var wordSpans = new List<SnapshotSpan>();

			// Find all words in the buffer like the one the caret is on
			TextExtent word = TextStructureNavigator.GetExtentOfWord(currentRequest);
			bool foundWord = true;

			// If we've selected something not worth highlighting, we might have missed a "word" by a little bit
			if (!WordExtentIsValid(currentRequest, word))
			{
				// Before we retry, make sure it is worthwhile
				if (word.Span.Start != currentRequest
					|| currentRequest == currentRequest.GetContainingLine().Start
					|| char.IsWhiteSpace((currentRequest - 1).GetChar()))
				{
					foundWord = false;
				}
				else
				{
					// Try again, one character previous.
					// If the caret is at the end of a word, pick up the word.
					word = TextStructureNavigator.GetExtentOfWord(currentRequest - 1);

					// If the word still isn't valid, we're done
					if (!WordExtentIsValid(currentRequest, word))
						foundWord = false;
				}
			}

			if (!foundWord)
			{
				//If we couldn't find a word, clear out the existing markers
				SynchronousUpdate(currentRequest, new NormalizedSnapshotSpanCollection(), null);
				return;
			}

			SnapshotSpan currentWord = word.Span;
			//If this is the current word, and the caret moved within a word, we're done.
			if (CurrentWord.HasValue && currentWord == CurrentWord)
				return;

			//Find the new spans
			var findData = new FindData(currentWord.GetText(), currentWord.Snapshot);
			findData.FindOptions = FindOptions.WholeWord | FindOptions.MatchCase;

			wordSpans.AddRange(TextSearchService.FindAll(findData));

			// If another change hasn't happened, do a real update
			if (currentRequest == RequestedPoint)
				SynchronousUpdate(currentRequest, new NormalizedSnapshotSpanCollection(wordSpans), currentWord);
		}

		private static bool WordExtentIsValid(SnapshotPoint currentRequest, TextExtent word)
		{
			return currentRequest.Snapshot.GetText(word.Span).Any(c => char.IsLetter(c) || char.IsNumber(c));
		}

		private void SynchronousUpdate(SnapshotPoint currentRequest, NormalizedSnapshotSpanCollection newSpans,
									   SnapshotSpan? newCurrentWord)
		{
			lock (_updateLock)
			{
				if (currentRequest != RequestedPoint)
					return;

				WordSpans = newSpans;
				CurrentWord = newCurrentWord;

				EventHandler<SnapshotSpanEventArgs> tempEvent = TagsChanged;
				if (tempEvent != null)
					tempEvent(this,
							  new SnapshotSpanEventArgs(new SnapshotSpan(SourceBuffer.CurrentSnapshot, 0,
																		 SourceBuffer.CurrentSnapshot.Length)));
			}
		}
	}
}
