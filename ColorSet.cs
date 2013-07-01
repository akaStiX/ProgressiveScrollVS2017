using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Windows;
using Microsoft.VisualStudio.Text.Editor;

namespace ProgressiveScroll
{
	public class FormatNames
	{
		public const string Text = "Progressive Scroll Text";
		public const string VisibleRegion = "Progressive Scroll Visible Region";
		public const string Comments = "Progressive Scroll Comments";
		public const string Strings = "Progressive Scroll Strings";
		public const string Changes = "Progressive Scroll Changes";
		public const string UnsavedChanges = "Progressive Scroll Unsaved Changes";
		public const string Highlights = "Progressive Scroll Highlights";
		public const string Breakpoints = "Progressive Scroll Breakpoints";
		public const string Bookmarks = "Progressive Scroll Bookmarks";
		public const string Errors = "Progressive Scroll Errors";
	}

	public class ClassificationNames
	{
		public const string Highlights = "PSHighlightWordClassification";
	}


	[Export(typeof(EditorFormatDefinition))]
	[Name(FormatNames.Text)]
	[UserVisible(true)]
	internal class TextFormatDefinition : MarkerFormatDefinition
	{
		public TextFormatDefinition()
		{
			DisplayName = FormatNames.Text;
			BackgroundColor = Color.FromRgb(238, 238, 238);
			ForegroundColor = Colors.Gray;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[Name(FormatNames.VisibleRegion)]
	[UserVisible(true)]
	internal class VisibleRegionFormatDefinition : MarkerFormatDefinition
	{
		public VisibleRegionFormatDefinition()
		{
			DisplayName = FormatNames.VisibleRegion;
			BackgroundColor = Colors.Gray;
			ForegroundColor = Colors.Black;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[Name(FormatNames.Comments)]
	[UserVisible(true)]
	internal class CommentsFormatDefinition : MarkerFormatDefinition
	{
		public CommentsFormatDefinition()
		{
			DisplayName = FormatNames.Comments;
			BackgroundColor = Colors.Black;
			BackgroundCustomizable = false;
			ForegroundColor = Colors.Green;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[Name(FormatNames.Strings)]
	[UserVisible(true)]
	internal class StringsFormatDefinition : MarkerFormatDefinition
	{
		public StringsFormatDefinition()
		{
			DisplayName = FormatNames.Strings;
			BackgroundColor = Colors.Black;
			BackgroundCustomizable = false;
			ForegroundColor = Color.FromRgb(190, 80, 80);
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[Name(FormatNames.Changes)]
	[UserVisible(true)]
	internal class ChangesFormatDefinition : MarkerFormatDefinition
	{
		public ChangesFormatDefinition()
		{
			DisplayName = FormatNames.Changes;
			BackgroundColor = Colors.Black;
			BackgroundCustomizable = false;
			ForegroundColor = Color.FromRgb(108, 226, 108);
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[Name(FormatNames.UnsavedChanges)]
	[UserVisible(true)]
	internal class UnsavedChangesFormatDefinition : MarkerFormatDefinition
	{
		public UnsavedChangesFormatDefinition()
		{
			DisplayName = FormatNames.UnsavedChanges;
			BackgroundColor = Colors.Black;
			BackgroundCustomizable = false;
			ForegroundColor = Color.FromRgb(255, 238, 98);
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[ClassificationType(ClassificationTypeNames = ClassificationNames.Highlights)]
	[Name(FormatNames.Highlights)]
	[UserVisible(true)]
	[Order(After = Priority.High)]
	public sealed class HighlightsFormatDefinition : ClassificationFormatDefinition
	{
		public HighlightsFormatDefinition()
		{
			DisplayName = FormatNames.Highlights;
			BackgroundColor = Colors.Orange;
			ForegroundColor = Colors.Black;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[Name(FormatNames.Breakpoints)]
	[UserVisible(true)]
	internal class BreakpointsFormatDefinition : MarkerFormatDefinition
	{
		public BreakpointsFormatDefinition()
		{
			DisplayName = FormatNames.Breakpoints;
			BackgroundColor = Colors.Black;
			BackgroundCustomizable = false;
			ForegroundColor = Colors.Red;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[Name(FormatNames.Bookmarks)]
	[UserVisible(true)]
	internal class BookmarksFormatDefinition : MarkerFormatDefinition
	{
		public BookmarksFormatDefinition()
		{
			DisplayName = FormatNames.Bookmarks;
			BackgroundColor = Colors.Black;
			BackgroundCustomizable = false;
			ForegroundColor = Colors.Blue;
		}
	}

	[Export(typeof(EditorFormatDefinition))]
	[Name(FormatNames.Errors)]
	[UserVisible(true)]
	internal class ErrorsFormatDefinition : MarkerFormatDefinition
	{
		public ErrorsFormatDefinition()
		{
			DisplayName = FormatNames.Errors;
			BackgroundColor = Colors.Black;
			BackgroundCustomizable = false;
			ForegroundColor = Colors.Maroon;
		}
	}

	class ColorSet
	{
		private readonly IEditorFormatMap _formatMap;

		public SolidColorBrush WhitespaceBrush { get; private set; }
		public SolidColorBrush TextBrush { get; private set; }
		public SolidColorBrush CommentsBrush { get; private set; }
		public SolidColorBrush StringsBrush { get; private set; }
		public SolidColorBrush VisibleRegionBrush { get; private set; }
		public Pen VisibleRegionBorderPen { get; private set; }
		public SolidColorBrush ChangesBrush { get; private set; }
		public SolidColorBrush UnsavedChangesBrush { get; private set; }
		public SolidColorBrush HighlightsBrush { get; private set; }
		public SolidColorBrush BreakpointsBrush { get; private set; }
		public SolidColorBrush BookmarksBrush { get; private set; }
		public SolidColorBrush ErrorsBrush { get; private set; }

		public ColorSet(IEditorFormatMap formatMap)
		{
			_formatMap = formatMap;
			_formatMap.FormatMappingChanged += OnFormatMappingChanged;
			ReloadColors();
		}

		private void OnFormatMappingChanged(object sender, FormatItemsEventArgs e)
		{
			ProgressiveScroll.SettingsChanged();
		}

		public void ReloadColors()
		{
			ResourceDictionary resDict = _formatMap.GetProperties(FormatNames.Text);
			WhitespaceBrush = (SolidColorBrush)resDict[EditorFormatDefinition.BackgroundBrushId];
			TextBrush = (SolidColorBrush)resDict[EditorFormatDefinition.ForegroundBrushId];

			resDict = _formatMap.GetProperties(FormatNames.VisibleRegion);
			Color c = (Color)resDict[EditorFormatDefinition.BackgroundColorId];
			VisibleRegionBrush = new SolidColorBrush(Color.FromArgb((byte)(Options.CursorOpacity * 255), c.R, c.G, c.B));
			VisibleRegionBorderPen = new Pen((SolidColorBrush)resDict[EditorFormatDefinition.ForegroundBrushId], 1.0);

			resDict = _formatMap.GetProperties(FormatNames.Comments);
			CommentsBrush = (SolidColorBrush)resDict[EditorFormatDefinition.ForegroundBrushId];

			resDict = _formatMap.GetProperties(FormatNames.Strings);
			StringsBrush = (SolidColorBrush)resDict[EditorFormatDefinition.ForegroundBrushId];

			resDict = _formatMap.GetProperties(FormatNames.Changes);
			ChangesBrush = (SolidColorBrush)resDict[EditorFormatDefinition.ForegroundBrushId];

			resDict = _formatMap.GetProperties(FormatNames.UnsavedChanges);
			UnsavedChangesBrush = (SolidColorBrush)resDict[EditorFormatDefinition.ForegroundBrushId];

			resDict = _formatMap.GetProperties(FormatNames.Highlights);
			HighlightsBrush = (SolidColorBrush)resDict[EditorFormatDefinition.BackgroundBrushId];

			resDict = _formatMap.GetProperties(FormatNames.Breakpoints);
			BreakpointsBrush = (SolidColorBrush)resDict[EditorFormatDefinition.ForegroundBrushId];

			resDict = _formatMap.GetProperties(FormatNames.Bookmarks);
			BookmarksBrush = (SolidColorBrush)resDict[EditorFormatDefinition.ForegroundBrushId];

			resDict = _formatMap.GetProperties(FormatNames.Errors);
			ErrorsBrush = (SolidColorBrush)resDict[EditorFormatDefinition.ForegroundBrushId];
		}
	}
}
