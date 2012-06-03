using System;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Outlining;

namespace ProgressiveScroll
{
	/// <summary>
	/// A class detailing the margin's visual definition including both size and content.
	/// </summary>
	class ProgressiveScroll : Canvas, IWpfTextViewMargin
	{
		public const string MarginName = "ProgressiveScroll";
		private bool _isDisposed = false;

		private ProgressiveScrollElement _progressiveScrollElement;

		/// <summary>
		/// Creates a <see cref="ProgressiveScroll"/> for a given <see cref="IWpfTextView"/>.
		/// </summary>
		/// <param name="textView">The <see cref="IWpfTextView"/> to attach the margin to.</param>
		public ProgressiveScroll(IWpfTextViewHost textViewHost, IOutliningManager outliningManager, IVerticalScrollBar scrollBar, ProgressiveScrollFactory factory)
		{
			//base.Background = Brushes.Transparent;
			//base.ClipToBounds = false;

			if (textViewHost == null)
			{
				throw new ArgumentNullException("textViewHost");
			}

			this._progressiveScrollElement = new ProgressiveScrollElement(textViewHost.TextView, outliningManager, scrollBar, factory);
		}

		private void ThrowIfDisposed()
		{
			if (_isDisposed)
				throw new ObjectDisposedException(MarginName);
		}

		#region IWpfTextViewMargin Members

		/// <summary>
		/// The <see cref="Sytem.Windows.FrameworkElement"/> that implements the visual representation
		/// of the margin.
		/// </summary>
		public System.Windows.FrameworkElement VisualElement
		{
			// Since this margin implements Canvas, this is the object which renders
			// the margin.
			get
			{
				ThrowIfDisposed();
				return _progressiveScrollElement;
			}
		}

		#endregion

		#region ITextViewMargin Members

		public double MarginSize
		{
			// Since this is a horizontal margin, its width will be bound to the width of the text view.
			// Therefore, its size is its height.
			get
			{
				ThrowIfDisposed();
				return this.ActualHeight;
			}
		}

		public bool Enabled
		{
			// The margin should always be enabled
			get
			{
				ThrowIfDisposed();
				return true;
			}
		}

		/// <summary>
		/// Returns an instance of the margin if this is the margin that has been requested.
		/// </summary>
		/// <param name="marginName">The name of the margin requested</param>
		/// <returns>An instance of ProgressiveScroll or null</returns>
		public ITextViewMargin GetTextViewMargin(string marginName)
		{
			return (marginName == ProgressiveScroll.MarginName) ? (IWpfTextViewMargin)this : null;
		}

		public void Dispose()
		{
			if (!_isDisposed)
			{
				GC.SuppressFinalize(this);
				_isDisposed = true;
			}
		}
		#endregion
	}
}
