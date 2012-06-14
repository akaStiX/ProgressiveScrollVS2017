using System;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Outlining;
using System.Windows.Input;
using System.Windows;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Text.Document;

namespace ProgressiveScroll
{
	/// <summary>
	/// A class detailing the margin's visual definition including both size and content.
	/// </summary>
	class ProgressiveScroll : Grid, IWpfTextViewMargin
	{
		public const string MarginName = "ProgressiveScroll";
		private bool _isDisposed = false;

		private IWpfTextViewHost _textViewHost;
		private IWpfTextView _textView;
		private IVerticalScrollBar _scrollBar;
		private ProgressiveScrollElement _progressiveScrollElement;

		private readonly int _splitterHeight = 17;
		private readonly int _horizontalScrollBarHeight = 17;

		public double DrawHeight
		{
			get { return ActualHeight + _splitterHeight + _horizontalScrollBarHeight; }
		}


		/// <summary>
		/// Creates a <see cref="ProgressiveScroll"/> for a given <see cref="IWpfTextView"/>.
		/// </summary>
		/// <param name="textView">The <see cref="IWpfTextView"/> to attach the margin to.</param>
		public ProgressiveScroll(
			IWpfTextViewHost textViewHost,
			IOutliningManager outliningManager,
			ITagAggregator<ChangeTag> changeTagAggregator,
			IVerticalScrollBar scrollBar,
			ProgressiveScrollFactory factory)
		{
			if (textViewHost == null)
			{
				throw new ArgumentNullException("textViewHost");
			}

			_textViewHost = textViewHost;
			_textView = textViewHost.TextView;
			_scrollBar = scrollBar;

			Background = Brushes.Transparent;

			Width = 128;

			_progressiveScrollElement = new ProgressiveScrollElement(textViewHost.TextView, outliningManager, changeTagAggregator, scrollBar, this);

			RegisterEvents();
		}

		private void ThrowIfDisposed()
		{
			if (_isDisposed)
				throw new ObjectDisposedException(MarginName);
		}

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
				return this;
			}
		}

		public double MarginSize
		{
			get
			{
				this.ThrowIfDisposed();
				return this.ActualWidth;
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

		private void RegisterEvents()
		{
			_textView.LayoutChanged += OnLayoutChanged;
			_scrollBar.TrackSpanChanged += OnTrackSpanChanged;
			HighlightWordTaggerProvider.Taggers[_textView].TagsChanged += OnTagsChanged;

			this.MouseLeftButtonDown += OnMouseLeftButtonDown;
			this.MouseMove += OnMouseMove;
			this.MouseLeftButtonUp += OnMouseLeftButtonUp;
		}

		private void UnregisterEvents()
		{
			_textView.LayoutChanged -= OnLayoutChanged;
			_scrollBar.TrackSpanChanged -= OnTrackSpanChanged;
			HighlightWordTaggerProvider.Taggers[_textView].TagsChanged -= OnTagsChanged;

			this.MouseLeftButtonDown -= OnMouseLeftButtonDown;
			this.MouseMove -= OnMouseMove;
			this.MouseLeftButtonUp -= OnMouseLeftButtonUp;
		}

		private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
		{
			this.InvalidateVisual();
		}

		private void OnTrackSpanChanged(object sender, EventArgs e)
		{
			this.InvalidateVisual();
		}

		private void OnTagsChanged(object sender, SnapshotSpanEventArgs e)
		{
			this.InvalidateVisual();
		}

		void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			this.CaptureMouse();

			Point pt = e.GetPosition(this);
			this.ScrollViewToYCoordinate(pt.Y);
		}

		void OnMouseMove(object sender, MouseEventArgs e)
		{
			if (e.LeftButton == MouseButtonState.Pressed && this.IsMouseCaptured)
			{
				Point pt = e.GetPosition(this);
				this.ScrollViewToYCoordinate(pt.Y);
			}
		}

		void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			this.ReleaseMouseCapture();
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);

			// Don't bother drawing if we have no content or no area to draw in (implying there are no children)
			if (ActualWidth > 0.0)
			{
				drawingContext.PushTransform(new TranslateTransform(0.0, -_splitterHeight));
				Rect viewRect = new Rect(0, 0, ActualWidth, DrawHeight);
				drawingContext.PushClip(new RectangleGeometry(viewRect));
				drawingContext.DrawRectangle(
					new SolidColorBrush(Color.FromRgb(0, 0, 0)),
					null,
					viewRect);

				this.VisualBitmapScalingMode = System.Windows.Media.BitmapScalingMode.Fant;

				_progressiveScrollElement.Render(drawingContext);

				drawingContext.Pop();
			}
		}

		internal void ScrollViewToYCoordinate(double y)
		{
			y = y + _splitterHeight;
			double yLastLine = _scrollBar.TrackSpanBottom;

			if (y < yLastLine)
			{
				SnapshotPoint position = _scrollBar.GetBufferPositionOfYCoordinate(y);

				_textView.ViewScroller.EnsureSpanVisible(new SnapshotSpan(position, 0), EnsureSpanVisibleOptions.AlwaysCenter);
			}
			else
			{
				y = Math.Min(y, yLastLine + (_scrollBar.ThumbHeight / 2.0));
				double fraction = (y - yLastLine) / _scrollBar.ThumbHeight; // 0 to 0.5
				double dyDistanceFromTopOfViewport = _textView.ViewportHeight * (0.5 - fraction);
				SnapshotPoint end = new SnapshotPoint(_textView.TextSnapshot, _textView.TextSnapshot.Length);

				_textView.DisplayTextLineContainingBufferPosition(end, dyDistanceFromTopOfViewport, ViewRelativePosition.Top);
			}
		}
	}
}
