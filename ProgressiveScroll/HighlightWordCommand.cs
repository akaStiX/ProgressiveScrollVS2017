using System;
using System.Windows.Input;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text.Editor;

namespace ProgressiveScroll
{

	// Detects double click input
	class HighlightWordCommand : IOleCommandTarget
	{
		public IOleCommandTarget NextTarget { private get; set; }

		public bool Selected { get; private set; }
		public bool Unselected { get; private set; }

		public HighlightWordCommand(IWpfTextView textView)
		{
		}


		#region IOleCommandTarget Members

		// Determines which events are handled
		int IOleCommandTarget.QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
		{
			if (pguidCmdGroup == VSConstants.VSStd2K &&
				cCmds == 1)
			{
				bool altPressed = !Options.AltHighlight || (Keyboard.PrimaryDevice.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;

				if (prgCmds[0].cmdID == (uint)VSConstants.VSStd2KCmdID.DOUBLECLICK && altPressed)
				{
					//prgCmds[0].cmdf = OLECMDF_SUPPORTED | OLECMDF_ENABLED;
					prgCmds[0].cmdf = 1 | 2;
					return 0;
				}
				else if (prgCmds[0].cmdID == (uint)VSConstants.VSStd2KCmdID.ECMD_LEFTCLICK)
				{
					Selected = false;
					Unselected = false;
				}
			}

			if (NextTarget != null)
			{
				return NextTarget.QueryStatus(pguidCmdGroup, cCmds, prgCmds, pCmdText);
			}
			else
			{
				return VSConstants.S_OK;
			}
		}

		int IOleCommandTarget.Exec(ref Guid pguidCmdGroup, uint nCmdId, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
		{
			if (pguidCmdGroup == VSConstants.VSStd2K)
			{
				if (nCmdId == (uint)VSConstants.VSStd2KCmdID.DOUBLECLICK)
				{
					Unselected = false;
					Selected = true;
				}
				else if (nCmdId == (uint)VSConstants.VSStd2KCmdID.CANCEL)
				{
					Unselected = true;
					Selected = false;
					CommandChanged(this, EventArgs.Empty);
				}
			}

			if (NextTarget != null)
			{
				return NextTarget.Exec(ref pguidCmdGroup, nCmdId, nCmdexecopt, pvaIn, pvaOut);
			}
			else
			{
				return VSConstants.S_OK;
			}
		}

		#endregion

		public event EventHandler CommandChanged;
	}
}
