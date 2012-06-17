using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using Microsoft.Win32;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel;

namespace ProgressiveScroll
{
	public class OptionNames
	{
		public const string PageCategoryName = "Progressive Scroll";
		public const string PageName = "General";

		public const string ScrollBarWidth = "ScrollBarWidth";
	}

	[ClassInterface(ClassInterfaceType.AutoDual)]
	[CLSCompliant(false), ComVisible(true)]
	[System.ComponentModel.DesignerCategory("")] // Prevents this file from being opened in design mode.
	public class GeneralOptionPage : DialogPage
	{
		private int _width = 128;

		[Category("Appearance")]
		[DisplayName("Width")]
		[Description("Width of the scrollbar")]
		public int ScrollBarWidth
		{
			get { return _width; }
			set { _width = Math.Min(Math.Max(value, 16), 512); }
		}

		protected override void OnApply(PageApplyEventArgs e)
		{
			base.OnApply(e);
			if (e.ApplyBehavior == ApplyKind.Apply)
			{
				// Update all ProgressiveScroll objects
				ProgressiveScroll.SettingsChanged(this);
			}
		}
	}

	[PackageRegistration(UseManagedResourcesOnly = true)]
	[Guid("3E81D486-9A46-458A-BB83-7655DD0E18A4")]
	[ProvideOptionPage(typeof(GeneralOptionPage), OptionNames.PageCategoryName, OptionNames.PageName, 0, 0, true)]
	public sealed class ProgressiveScrollOptions : Package
	{
		public ProgressiveScrollOptions()
		{
		}

		/*public GeneralOptionPage GetOptions()
		{
			return (GeneralOptionPage)GetDialogPage(typeof(GeneralOptionPage));
		}*/

	}
}
