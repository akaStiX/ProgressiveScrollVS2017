using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using EnvDTE;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Document;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Outlining;
using Microsoft.VisualStudio.Text.Tagging;

namespace ProgressiveScroll
{
	internal class ProgressiveScroll : Canvas, IWpfTextViewMargin
	{
		public const string MarginName = "ProgressiveScroll";

		private static readonly Dictionary<ProgressiveScroll, byte> ProgressiveScrollDict =
			new Dictionary<ProgressiveScroll, byte>();

		private readonly ITagAggregator<IErrorTag> _errorTagAggregator;
		private readonly int _horizontalScrollBarHeight = 17;
		private readonly ITagAggregator<IVsVisibleTextMarkerTag> _markerTagAggregator;
		private readonly ProgressiveScrollView _progressiveScrollView;
		private readonly SimpleScrollBar _scrollBar;
		private readonly IWpfTextView _textView;

		private const int BottomMargin = 3;
		private bool _isDisposed;
		private int _splitterHeight = 17;
		private IWpfTextViewHost _textViewHost;

		public ProgressiveScroll(
			IWpfTextViewHost textViewHost,
			IOutliningManager outliningManager,
			ITagAggregator<ChangeTag> changeTagAggregator,
			ITagAggregator<IVsVisibleTextMarkerTag> markerTagAggregator,
			ITagAggregator<IErrorTag> errorTagAggregator,
			Debugger debugger,
			SimpleScrollBar scrollBar,
			ProgressiveScrollFactory factory)
		{
			if (textViewHost == null)
			{
				throw new ArgumentNullException("textViewHost");
			}

			if (ProgressiveScrollFactory.IsVS11)
			{
				_horizontalScrollBarHeight = 0;
			}

			ProgressiveScrollDict.Add(this, 0);

			_textViewHost = textViewHost;
			_textView = textViewHost.TextView;
			_scrollBar = scrollBar;
			_markerTagAggregator = markerTagAggregator;
			_errorTagAggregator = errorTagAggregator;

			Background = Brushes.Transparent;

			Width = scrollBar.Width;

			_progressiveScrollView = new ProgressiveScrollView(
				textViewHost.TextView,
				outliningManager,
				changeTagAggregator,
				markerTagAggregator,
				errorTagAggregator,
				debugger,
				scrollBar,
				this);

			RegisterEvents();
		}

		public ColorSet Colors { get; set; }

		public ProgressiveScrollView ScrollView
		{
			get { return _progressiveScrollView; }
		}

		public IEditorFormatMapService FormatMapService { get; set; }

		public double ClipHeight
		{
			get { return ActualHeight + _splitterHeight + _horizontalScrollBarHeight; }
		}

		public double DrawHeight
		{
			get { return Math.Max(ClipHeight - BottomMargin, 0); }
		}

		public bool SplitterEnabled
		{
			get { return _splitterHeight == 0; }
			set { _splitterHeight = value ? 0 : 17; }
		}

		#region IWpfTextViewMargin Members

		public FrameworkElement VisualElement
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
				ThrowIfDisposed();
				return ActualWidth;
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
			return (marginName == MarginName) ? this : null;
		}

		public void Dispose()
		{
			if (!_isDisposed)
			{
				HighlightWordTaggerProvider.Taggers.Remove(_textView);
				ProgressiveScrollDict.Remove(this);
				GC.SuppressFinalize(this);
				_isDisposed = true;
			}
		}

		#endregion

		public static void SettingsChanged(GeneralOptionPage options)
		{
			foreach (var kv in ProgressiveScrollDict)
			{
				kv.Key.UpdateSettings(options);
			}
		}

		internal void UpdateSettings(GeneralOptionPage options)
		{
			Colors.CursorOpacity = options.CursorOpacity;
			Colors.RefreshColors();
			int newWidth = options.ScrollBarWidth;
			_scrollBar.Width = newWidth;
			Width = newWidth;
			SplitterEnabled = options.SplitterEnabled;
			_scrollBar.SplitterEnabled = options.SplitterEnabled;

			_progressiveScrollView.CursorBorderEnabled = options.CursorBorderEnabled;
			_progressiveScrollView.RenderTextEnabled = options.RenderTextEnabled;
			_progressiveScrollView.MarkerRenderer.ErrorsEnabled = options.ErrorsEnabled;
			_progressiveScrollView.TextDirty = true;

			WordSelectionCommandFilter.AltHighlight = options.AltHighlight;

			InvalidateAsync();
		}

		private void ThrowIfDisposed()
		{
			if (_isDisposed)
				throw new ObjectDisposedException(MarginName);
		}

		private void RegisterEvents()
		{
			_textView.LayoutChanged += OnTextChanged;
			_scrollBar.TrackSpanChanged += OnViewChanged;
			_markerTagAggregator.TagsChanged += OnTagsChanged;
			_errorTagAggregator.TagsChanged += OnTagsChanged;
			if (HighlightWordTaggerProvider.Taggers.ContainsKey(_textView))
			{
				HighlightWordTaggerProvider.Taggers[_textView].TagsChanged += OnTextChanged;
			}

			MouseLeftButtonDown += OnMouseLeftButtonDown;
			MouseMove += OnMouseMove;
			MouseLeftButtonUp += OnMouseLeftButtonUp;
		}

		private void OnTextChanged(object sender, EventArgs e)
		{
			_progressiveScrollView.TextDirty = true;
			InvalidateAsync();
		}

		private void OnViewChanged(object sender, EventArgs e)
		{
			InvalidateAsync();
		}

		private void OnTagsChanged(object sender, EventArgs e)
		{
			InvalidateAsync();
		}

		private void InvalidateAsync()
		{
			try
			{
				Dispatcher.BeginInvoke(
					DispatcherPriority.Normal,
					new Action(() => InvalidateVisual()));
			}
			catch (Exception)
			{
			}
		}

		private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			CaptureMouse();

			Point pt = e.GetPosition(this);
			ScrollViewToYCoordinate(pt.Y);
		}

		private void OnMouseMove(object sender, MouseEventArgs e)
		{
			if (e.LeftButton == MouseButtonState.Pressed && IsMouseCaptured)
			{
				Point pt = e.GetPosition(this);
				ScrollViewToYCoordinate(pt.Y);
			}
		}

		private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			ReleaseMouseCapture();
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);

			// Don't bother drawing if we have no content or no area to draw in (implying there are no children)
			if (ActualWidth > 0.0)
			{
				Colors.RefreshColors();

				drawingContext.PushTransform(new TranslateTransform(0.0, -_splitterHeight));
				var viewRect = new Rect(0, 0, ActualWidth, ClipHeight);
				drawingContext.PushClip(new RectangleGeometry(viewRect));
				drawingContext.DrawRectangle(
					Colors.WhitespaceBrush,
					null,
					viewRect);

				VisualBitmapScalingMode = BitmapScalingMode.Fant;

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
				y = Math.Min(y, yLastLine + (_scrollBar.ThumbHeight/2.0));
				double fraction = (y - yLastLine)/_scrollBar.ThumbHeight; // 0 to 0.5
				double dyDistanceFromTopOfViewport = _textView.ViewportHeight*(0.5 - fraction);
				var end = new SnapshotPoint(_textView.TextSnapshot, _textView.TextSnapshot.Length);

				_textView.DisplayTextLineContainingBufferPosition(end, dyDistanceFromTopOfViewport, ViewRelativePosition.Top);
			}
		}
	}
}