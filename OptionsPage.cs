using System;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio;
using System.ComponentModel;

namespace ProgressiveScroll
{
	public class OptionNames
	{
		public const string PageCategoryName = "Progressive Scroll";
		public const string PageName = "General";

		public const string ScrollBarWidth = "ScrollBarWidth";
		public const string RenderTextEnabled = "RenderTextEnabled";
		public const string CursorOpacity = "CursorOpacity";
		public const string CursorBorderEnabled = "CursorBorderEnabled";
		public const string SplitterEnabled = "SplitterEnabled";
		public const string ErrorsEnabled = "ErrorsEnabled";
		public const string AltHighlight = "AltHighlight";
		public const string MatchCase = "MatchCase";
		public const string MatchWholeWord = "MatchWholeWord";
	}

	[ClassInterface(ClassInterfaceType.AutoDual)]
	[System.ComponentModel.DesignerCategory("")] // Prevents this file from being opened in design mode.
	[ComVisible(true)]
	public class OptionsPage : DialogPage
	{
		private int _scrollBarWidth = 128;
		private bool _renderTextEnabled = true;
		private double _cursorOpacity = 0.125;
		private bool _cursorBorderEnabled = false;
		private bool _splitterEnabled = false;
		private bool _errorsEnabled = false;
		private bool _altHighlight = false;
		private bool _matchCase = true;
		private bool _matchWholeWord = true;

		[Category("General")]
		[DisplayName("Width")]
		[Description("Width of the scrollbar")]
		public int ScrollBarWidth
		{
			get { return _scrollBarWidth; }
			set { _scrollBarWidth = Math.Min(Math.Max(value, 16), 512); }
		}

		[Category("General")]
		[DisplayName("Display Code")]
		[Description("Displays the code in the scrollbar.")]
		public bool RenderTextEnabled
		{
			get { return _renderTextEnabled; }
			set { _renderTextEnabled = value; }
		}

		[Category("General")]
		[DisplayName("Visible Region Opacity")]
		[Description("Opacity of the visible region.")]
		public double CursorOpacity
		{
			get { return _cursorOpacity; }
			set { _cursorOpacity = Math.Min(Math.Max(value, 0.0), 1.0); }
		}

		[Category("General")]
		[DisplayName("Display Border")]
		[Description("Displays a border around the visible region.")]
		public bool CursorBorderEnabled
		{
			get { return _cursorBorderEnabled; }
			set { _cursorBorderEnabled = value; }
		}

		[Category("General")]
		[DisplayName("Display Splitter")]
		[Description("Displays the splitter control.")]
		public bool SplitterEnabled
		{
			get { return _splitterEnabled; }
			set { _splitterEnabled = value; }
		}

		[Category("General")]
		[DisplayName("Display Error Marks")]
		[Description("Displays marks for errors in the scrollbar.")]
		public bool ErrorsEnabled
		{
			get { return _errorsEnabled; }
			set { _errorsEnabled = value; }
		}

		[Category("General")]
		[DisplayName("Alt highlight")]
		[Description("Requires the user to hold Alt to highlight text.")]
		public bool AltHighlight
		{
			get { return _altHighlight; }
			set { _altHighlight = value; }
		}

		[Category("General")]
		[DisplayName("Match Case")]
		[Description("Only highlight case-sensitive matches for double-clicked text.")]
		public bool MatchCase
		{
			get { return _matchCase; }
			set { _matchCase = value; }
		}

		[Category("General")]
		[DisplayName("Match Whole Word")]
		[Description("Only highlight whole-word matches for double-clicked text.")]
		public bool MatchWholeWord
		{
			get { return _matchWholeWord; }
			set { _matchWholeWord = value; }
		}

		protected override void OnApply(PageApplyEventArgs e)
		{
			base.OnApply(e);

			if (e.ApplyBehavior == ApplyKind.Apply)
			{
				// Update all ProgressiveScroll objects

				Options.ScrollBarWidth = _scrollBarWidth;
				Options.RenderTextEnabled = _renderTextEnabled;
				Options.CursorOpacity = _cursorOpacity;
				Options.CursorBorderEnabled = _cursorBorderEnabled;
				Options.SplitterEnabled = _splitterEnabled;
				Options.ErrorsEnabled = _errorsEnabled;
				Options.AltHighlight = _altHighlight;
				Options.MatchCase = _matchCase;
				Options.MatchWholeWord = _matchWholeWord;

				ProgressiveScroll.SettingsChanged();
			}
		}
	}

	[PackageRegistration(UseManagedResourcesOnly = true)]
	[Guid("3E81D486-9A46-458A-BB83-7655DD0E18A4")]
	[ProvideOptionPage(typeof(OptionsPage), OptionNames.PageCategoryName, OptionNames.PageName, 0, 0, true)]
	//Microsoft.VisualStudio.VSConstants.UICONTEXT_NoSolution
	[ProvideAutoLoad("ADFC4E64-0397-11D1-9F4E-00A0C911004F")]
	public sealed class Options : Package
	{
		public static bool IsVS10;
		public static int ScrollBarWidth;
		public static bool RenderTextEnabled;
		public static double CursorOpacity;
		public static bool CursorBorderEnabled;
		public static bool SplitterEnabled;
		public static bool ErrorsEnabled;
		public static bool AltHighlight;
		public static bool MatchCase;
		public static bool MatchWholeWord;

		protected override void Initialize()
		{
			base.Initialize();

			DTE dte = (DTE)GetService(typeof(DTE));
			IsVS10 = (dte.Version == "10.0");

			EnvDTE.Properties props = dte.get_Properties(OptionNames.PageCategoryName, OptionNames.PageName);

			ScrollBarWidth = (int)props.Item(OptionNames.ScrollBarWidth).Value;
			RenderTextEnabled = (bool)props.Item(OptionNames.RenderTextEnabled).Value;
			CursorOpacity = (double)props.Item(OptionNames.CursorOpacity).Value;
			CursorBorderEnabled = (bool)props.Item(OptionNames.CursorBorderEnabled).Value;
			SplitterEnabled = (bool)props.Item(OptionNames.SplitterEnabled).Value;
			ErrorsEnabled = (bool)props.Item(OptionNames.ErrorsEnabled).Value;
			AltHighlight = (bool)props.Item(OptionNames.AltHighlight).Value;
			MatchCase = (bool)props.Item(OptionNames.MatchCase).Value;
			MatchWholeWord = (bool)props.Item(OptionNames.MatchWholeWord).Value;
		}
	}
}
