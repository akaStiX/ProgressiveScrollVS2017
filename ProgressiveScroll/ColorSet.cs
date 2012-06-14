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
			this.BackgroundColor = Colors.Black;
			this.ForegroundColor = Colors.White;
			this.DisplayName = "Progressive Scroll Text";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[Name("MarkerFormatDefinition/PSCommentFormatDefinition")]
	[UserVisible(true)]
	internal class CommentFormatDefinition : MarkerFormatDefinition
	{
		public CommentFormatDefinition()
		{
			this.BackgroundColor = Colors.Black;
			this.BackgroundCustomizable = false;
			this.ForegroundColor = Color.FromRgb(255, 128, 255);
			this.DisplayName = "Progressive Scroll Comments";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[Name("MarkerFormatDefinition/PSStringFormatDefinition")]
	[UserVisible(true)]
	internal class StringFormatDefinition : MarkerFormatDefinition
	{
		public StringFormatDefinition()
		{
			this.BackgroundColor = Colors.Black;
			this.BackgroundCustomizable = false;
			this.ForegroundColor = Colors.White;
			this.DisplayName = "Progressive Scroll Strings";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[Name("MarkerFormatDefinition/PSChangesFormatDefinition")]
	[UserVisible(true)]
	internal class ChangesFormatDefinition : MarkerFormatDefinition
	{
		public ChangesFormatDefinition()
		{
			this.BackgroundColor = Colors.Black;
			this.BackgroundCustomizable = false;
			this.ForegroundColor = Color.FromRgb(108, 226, 108);
			this.DisplayName = "Progressive Scroll Changes";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[Name("MarkerFormatDefinition/PSUnsavedChangesFormatDefinition")]
	[UserVisible(true)]
	internal class UnsavedChangesFormatDefinition : MarkerFormatDefinition
	{
		public UnsavedChangesFormatDefinition()
		{
			this.BackgroundColor = Colors.Black;
			this.BackgroundCustomizable = false;
			this.ForegroundColor = Color.FromRgb(255, 238, 98);
			this.DisplayName = "Progressive Scroll Unsaved Changes";
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[Name("MarkerFormatDefinition/PSHighlightWordFormatDefinition")]
	[UserVisible(true)]
	internal class HighlightWordFormatDefinition : MarkerFormatDefinition
	{
		public HighlightWordFormatDefinition()
		{
			this.BackgroundColor = Colors.Orange;
			this.ForegroundColor = Colors.Orange;
			this.DisplayName = "Progressive Scroll Highlights";
			this.ZOrder = 5;
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

		public ColorSet(ITextView textView, IEditorFormatMapService formatMapService)
		{
			_textView = textView;
			_formatMapService = formatMapService;
			RefreshColors();
		}

		public void RefreshColors()
		{
			IEditorFormatMap formatMap = _formatMapService.GetEditorFormatMap(_textView);

			ResourceDictionary textReferenceDict = formatMap.GetProperties("MarkerFormatDefinition/PSTextFormatDefinition");
			WhitespaceBrush = (SolidColorBrush)textReferenceDict[EditorFormatDefinition.BackgroundBrushId];
			TextBrush = (SolidColorBrush)textReferenceDict[EditorFormatDefinition.ForegroundBrushId];
			// Note: No clue how I should expose the opacity.
			VisibleBrush = new SolidColorBrush(Color.FromArgb(32, TextBrush.Color.R, TextBrush.Color.G, TextBrush.Color.B));

			textReferenceDict = formatMap.GetProperties("MarkerFormatDefinition/PSCommentFormatDefinition");
			CommentBrush = (SolidColorBrush)textReferenceDict[EditorFormatDefinition.ForegroundBrushId];

			textReferenceDict = formatMap.GetProperties("MarkerFormatDefinition/PSStringFormatDefinition");
			StringBrush = (SolidColorBrush)textReferenceDict[EditorFormatDefinition.ForegroundBrushId];

			textReferenceDict = formatMap.GetProperties("MarkerFormatDefinition/PSChangesFormatDefinition");
			ChangedBrush = (SolidColorBrush)textReferenceDict[EditorFormatDefinition.ForegroundBrushId];

			textReferenceDict = formatMap.GetProperties("MarkerFormatDefinition/PSUnsavedChangesFormatDefinition");
			UnsavedChangedBrush = (SolidColorBrush)textReferenceDict[EditorFormatDefinition.ForegroundBrushId];

			textReferenceDict = formatMap.GetProperties("MarkerFormatDefinition/PSHighlightWordFormatDefinition");
			HighlightBrush = (SolidColorBrush)textReferenceDict[EditorFormatDefinition.ForegroundBrushId];
		}

	}
}
