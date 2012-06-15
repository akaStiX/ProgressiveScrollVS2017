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
using Microsoft.VisualStudio.Text.Classification;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Editor;

namespace ProgressiveScroll
{
	class ProgressiveScroll : Canvas, IWpfTextViewMargin
	{
		public IEditorFormatMapService FormatMapService { get; set; }
		public double DrawHeight
		{
			get { return ActualHeight + _splitterHeight + _horizontalScrollBarHeight; }
		}
		public const string MarginName = "ProgressiveScroll";
		public ColorSet Colors { get; set; }



		private bool _isDisposed = false;

		private IWpfTextViewHost _textViewHost;
		private IWpfTextView _textView;
		private IVerticalScrollBar _scrollBar;
		private ITagAggregator<IVsVisibleTextMarkerTag> _markerTagAggregator;
		private ProgressiveScrollView _progressiveScrollView;

		private readonly int _splitterHeight = 17;
		private readonly int _horizontalScrollBarHeight = 17;


		public ProgressiveScroll(
			IWpfTextViewHost textViewHost,
			IOutliningManager outliningManager,
			ITagAggregator<ChangeTag> changeTagAggregator,
			ITagAggregator<IVsVisibleTextMarkerTag> markerTagAggregator,
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
			_markerTagAggregator = markerTagAggregator;

			Background = Brushes.Transparent;

			Width = 128;

			_progressiveScrollView = new ProgressiveScrollView(
				textViewHost.TextView,
				outliningManager,
				changeTagAggregator,
				markerTagAggregator,
				scrollBar,
				this);

			RegisterEvents();
		}

		private void ThrowIfDisposed()
		{
			if (_isDisposed)
				throw new ObjectDisposedException(MarginName);
		}

		public System.Windows.FrameworkElement VisualElement
		{
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
			get
			{
				ThrowIfDisposed();
				return true;
			}
		}

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
			_textView.LayoutChanged += OnTextChanged;
			_scrollBar.TrackSpanChanged += OnViewChanged;
			_markerTagAggregator.TagsChanged += OnTagsChanged;
			HighlightWordTaggerProvider.Taggers[_textView].TagsChanged += OnTextChanged;

			this.MouseLeftButtonDown += OnMouseLeftButtonDown;
			this.MouseMove += OnMouseMove;
			this.MouseLeftButtonUp += OnMouseLeftButtonUp;
		}

		private void UnregisterEvents()
		{
			_textView.LayoutChanged -= OnTextChanged;
			_scrollBar.TrackSpanChanged -= OnViewChanged;
			_markerTagAggregator.TagsChanged -= OnTagsChanged;
			HighlightWordTaggerProvider.Taggers[_textView].TagsChanged -= OnTextChanged;

			this.MouseLeftButtonDown -= OnMouseLeftButtonDown;
			this.MouseMove -= OnMouseMove;
			this.MouseLeftButtonUp -= OnMouseLeftButtonUp;
		}

		private void OnTextChanged(object sender, EventArgs e)
		{
			_progressiveScrollView.TextDirty = true;
			this.InvalidateVisual();
		}

		private void OnViewChanged(object sender, EventArgs e)
		{
			this.InvalidateVisual();
		}

		private void OnTagsChanged(object sender, EventArgs e)
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
				Colors.RefreshColors();

				drawingContext.PushTransform(new TranslateTransform(0.0, -_splitterHeight));
				Rect viewRect = new Rect(0, 0, ActualWidth, DrawHeight);
				drawingContext.PushClip(new RectangleGeometry(viewRect));
				drawingContext.DrawRectangle(
					Colors.WhitespaceBrush,
					null,
					viewRect);

				this.VisualBitmapScalingMode = System.Windows.Media.BitmapScalingMode.Fant;

				_progressiveScrollView.Render(drawingContext);

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
