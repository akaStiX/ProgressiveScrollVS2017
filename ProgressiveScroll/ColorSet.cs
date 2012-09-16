using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Windows;
using Microsoft.VisualStudio.Text.Editor;

namespace ProgressiveScroll
{
	[Export(typeof(EditorFormatDefinition))]
	[Name("MarkerFormatDefinition/PSTextFormatDefinition")]
	[UserVisible(true)]
	internal class TextFormatDefinition : MarkerFormatDefinition
	{
		public TextFormatDefinition()
		{
			DisplayName = "Progressive Scroll Text";
			BackgroundColor = Color.FromRgb(238, 238, 238);
			ForegroundColor = Colors.Gray;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[Name("MarkerFormatDefinition/PSCommentFormatDefinition")]
	[UserVisible(true)]
	internal class CommentFormatDefinition : MarkerFormatDefinition
	{
		public CommentFormatDefinition()
		{
			DisplayName = "Progressive Scroll Comments";
			BackgroundColor = Colors.Black;
			BackgroundCustomizable = false;
			ForegroundColor = Colors.Green;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[Name("MarkerFormatDefinition/PSStringFormatDefinition")]
	[UserVisible(true)]
	internal class StringFormatDefinition : MarkerFormatDefinition
	{
		public StringFormatDefinition()
		{
			DisplayName = "Progressive Scroll Strings";
			BackgroundColor = Colors.Black;
			BackgroundCustomizable = false;
			ForegroundColor = Color.FromRgb(190, 80, 80);
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[Name("MarkerFormatDefinition/PSChangesFormatDefinition")]
	[UserVisible(true)]
	internal class ChangesFormatDefinition : MarkerFormatDefinition
	{
		public ChangesFormatDefinition()
		{
			DisplayName = "Progressive Scroll Changes";
			BackgroundColor = Colors.Black;
			BackgroundCustomizable = false;
			ForegroundColor = Color.FromRgb(108, 226, 108);
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[Name("MarkerFormatDefinition/PSUnsavedChangesFormatDefinition")]
	[UserVisible(true)]
	internal class UnsavedChangesFormatDefinition : MarkerFormatDefinition
	{
		public UnsavedChangesFormatDefinition()
		{
			DisplayName = "Progressive Scroll Unsaved Changes";
			BackgroundColor = Colors.Black;
			BackgroundCustomizable = false;
			ForegroundColor = Color.FromRgb(255, 238, 98);
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = "PSHighlightWordFormatDefinition")]
	[Name("ClassificationFormatDefinition/PSHighlightWordFormatDefinition")]
	[UserVisible(true)]
	[Order(After = Priority.High)]
	public sealed class RegionForeground : ClassificationFormatDefinition
	{
		public RegionForeground()
		{
			DisplayName = "Progressive Scroll Highlights";
			BackgroundColor = Colors.Orange;
			ForegroundColor = Colors.Black;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[Name("MarkerFormatDefinition/PSBreakpointFormatDefinition")]
	[UserVisible(true)]
	internal class BreakpointFormatDefinition : MarkerFormatDefinition
	{
		public BreakpointFormatDefinition()
		{
			DisplayName = "Progressive Scroll Breakpoints";
			BackgroundColor = Colors.Black;
			BackgroundCustomizable = false;
			ForegroundColor = Colors.Red;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[Name("MarkerFormatDefinition/PSBookmarkFormatDefinition")]
	[UserVisible(true)]
	internal class BookmarkFormatDefinition : MarkerFormatDefinition
	{
		public BookmarkFormatDefinition()
		{
			DisplayName = "Progressive Scroll Bookmarks";
			BackgroundColor = Colors.Black;
			BackgroundCustomizable = false;
			ForegroundColor = Colors.Blue;
		}
	}

	class ColorSet
	{
		private ITextView _textView;
		private IEditorFormatMapService _formatMapService;

		public SolidColorBrush WhitespaceBrush { get; private set; }
		public SolidColorBrush TextBrush { get; private set; }
		public SolidColorBrush CommentBrush { get; private set; }
		public SolidColorBrush StringBrush { get; private set; }
		public SolidColorBrush VisibleBrush { get; private set; }
		public SolidColorBrush ChangedBrush { get; private set; }
		public SolidColorBrush UnsavedChangedBrush { get; private set; }
		public SolidColorBrush HighlightBrush { get; private set; }
		public SolidColorBrush BreakpointBrush { get; private set; }
		public SolidColorBrush BookmarkBrush { get; private set; }

		public double CursorOpacity { get; set; }

		public ColorSet(ITextView textView, IEditorFormatMapService formatMapService, double cursorOpacity)
		{
			_textView = textView;
			_formatMapService = formatMapService;
			CursorOpacity = cursorOpacity;
			RefreshColors();
		}

		public void RefreshColors()
		{
			IEditorFormatMap formatMap = _formatMapService.GetEditorFormatMap(_textView);

			ResourceDictionary textReferenceDict = formatMap.GetProperties("MarkerFormatDefinition/PSTextFormatDefinition");
			WhitespaceBrush = (SolidColorBrush)textReferenceDict[EditorFormatDefinition.BackgroundBrushId];
			TextBrush = (SolidColorBrush)textReferenceDict[EditorFormatDefinition.ForegroundBrushId];
			VisibleBrush = new SolidColorBrush(Color.FromArgb((byte)(CursorOpacity * 255), TextBrush.Color.R, TextBrush.Color.G, TextBrush.Color.B));

			textReferenceDict = formatMap.GetProperties("MarkerFormatDefinition/PSCommentFormatDefinition");
			CommentBrush = (SolidColorBrush)textReferenceDict[EditorFormatDefinition.ForegroundBrushId];

			textReferenceDict = formatMap.GetProperties("MarkerFormatDefinition/PSStringFormatDefinition");
			StringBrush = (SolidColorBrush)textReferenceDict[EditorFormatDefinition.ForegroundBrushId];

			textReferenceDict = formatMap.GetProperties("MarkerFormatDefinition/PSChangesFormatDefinition");
			ChangedBrush = (SolidColorBrush)textReferenceDict[EditorFormatDefinition.ForegroundBrushId];

			textReferenceDict = formatMap.GetProperties("MarkerFormatDefinition/PSUnsavedChangesFormatDefinition");
			UnsavedChangedBrush = (SolidColorBrush)textReferenceDict[EditorFormatDefinition.ForegroundBrushId];

			textReferenceDict = formatMap.GetProperties("ClassificationFormatDefinition/PSHighlightWordFormatDefinition");
			HighlightBrush = (SolidColorBrush)textReferenceDict[EditorFormatDefinition.BackgroundBrushId];

			textReferenceDict = formatMap.GetProperties("MarkerFormatDefinition/PSBreakpointFormatDefinition");
			BreakpointBrush = (SolidColorBrush)textReferenceDict[EditorFormatDefinition.ForegroundBrushId];

			textReferenceDict = formatMap.GetProperties("MarkerFormatDefinition/PSBookmarkFormatDefinition");
			BookmarkBrush = (SolidColorBrush)textReferenceDict[EditorFormatDefinition.ForegroundBrushId];
		}

	}
}
