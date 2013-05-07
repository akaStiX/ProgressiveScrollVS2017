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
	[System.Flags]
	public enum Parts
	{
		Marks = 1,
		Text = 2,
		TextContent = 4,
		All = Marks | Text | TextContent
	}

	internal class ProgressiveScroll : Canvas, IWpfTextViewMargin
	{
		public const string MarginName = "ProgressiveScroll";

		private static readonly List<ProgressiveScroll> ProgressiveScrollDict = new List<ProgressiveScroll>();

		private readonly ITagAggregator<IErrorTag> _errorTagAggregator;
		private readonly ITagAggregator<IVsVisibleTextMarkerTag> _markerTagAggregator;
		private readonly SimpleScrollBar _scrollBar;
		private readonly IWpfTextView _textView;
		private IWpfTextViewMargin _containerMargin;

		private DrawingVisual MarksVisual;
		private List<Visual> Visuals = new List<Visual>();

		private const int BottomMargin = 3;
		private bool _isDisposed;

		public ProgressiveScroll(
			IWpfTextViewMargin containerMargin,
			IWpfTextView textView,
			IOutliningManager outliningManager,
			ITagAggregator<ChangeTag> changeTagAggregator,
			ITagAggregator<IVsVisibleTextMarkerTag> markerTagAggregator,
			ITagAggregator<IErrorTag> errorTagAggregator,
			Debugger debugger,
			SimpleScrollBar scrollBar,
			ColorSet colors)
		{
			_containerMargin = containerMargin;

			ProgressiveScrollDict.Add(this);

			Colors = colors;
			_textView = textView;
			_scrollBar = scrollBar;
			_markerTagAggregator = markerTagAggregator;
			_errorTagAggregator = errorTagAggregator;

			RegisterEvents();
			InitSettings();

			_textRenderer = new TextRenderer(this, _textView, outliningManager);
			if (Options.RenderTextEnabled)
			{
				Visuals.Add(_textRenderer.TextVisual);
			}

			MarksVisual = new DrawingVisual();
			Visuals.Add(MarksVisual);

			_changeRenderer = new ChangeRenderer(_textView, changeTagAggregator, scrollBar);
			_highlightRenderer = new HighlightRenderer(_textView, scrollBar);
			_markerRenderer = new MarkerRenderer(_textView, markerTagAggregator, errorTagAggregator, debugger, scrollBar);

			foreach (var visual in Visuals)
			{
				AddVisualChild(visual);
			}
		}

		public ColorSet Colors { get; set; }

		public IEditorFormatMapService FormatMapService { get; set; }

		public double ClipHeight
		{
			get { return ActualHeight; }
		}

		public double DrawHeight
		{
			get { return Math.Max(ClipHeight - BottomMargin, 0); }
		}

		private TextRenderer _textRenderer;
		private ChangeRenderer _changeRenderer;
		private HighlightRenderer _highlightRenderer;
		private MarkerRenderer _markerRenderer;
		public MarkerRenderer MarkerRenderer { get { return _markerRenderer; } }


		#region IWpfTextViewMargin Members

		public FrameworkElement VisualElement
		{
			get { return this; }
		}

		protected override int VisualChildrenCount
		{
			get { return Visuals.Count; }
		}

		public double MarginSize
		{
			get { return ActualWidth; }
		}

		public bool Enabled
		{
			get { return true; }
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

		protected override Visual GetVisualChild(int index)
		{
			if (index >= 0 || index < Visuals.Count)
				return Visuals[index];

			throw new ArgumentOutOfRangeException("index");
		}

		public static void SettingsChanged()
		{
			foreach (var progressiveScroll in ProgressiveScrollDict)
			{
				progressiveScroll.UpdateSettings();
				progressiveScroll.Invalidate(Parts.All);
			}
		}

		private void InitSettings()
		{
			ClipToBounds = true;
			Colors.ReloadColors();

			Background = Colors.WhitespaceBrush;

			_containerMargin.VisualElement.Margin =
				new Thickness(
					0.0,
					Options.SplitterEnabled ? 0.0 : -17.0,
					0.0,
					Options.IsVS11 ? 0.0 : -17.0);

			Width = Options.ScrollBarWidth;
			_scrollBar.Width = Width;
		}

		private void UpdateSettings()
		{
			Colors.ReloadColors();

			Background = Colors.WhitespaceBrush;

			_containerMargin.VisualElement.Margin =
				new Thickness(
					0.0,
					Options.SplitterEnabled ? 0.0 : -17.0,
					0.0,
					Options.IsVS11 ? 0.0 : -17.0);

			Width = Options.ScrollBarWidth;
			_scrollBar.Width = Width;

			// Hide/Show TextVisual
			if (Options.RenderTextEnabled && !Visuals.Contains(_textRenderer.TextVisual))
			{
				Visuals.Insert(0, _textRenderer.TextVisual);
				AddVisualChild(_textRenderer.TextVisual);
			}
			else
			if (!Options.RenderTextEnabled && Visuals.Contains(_textRenderer.TextVisual))
			{
				RemoveVisualChild(_textRenderer.TextVisual);
				Visuals.Remove(_textRenderer.TextVisual);
			}
		}

		private void RegisterEvents()
		{
			Loaded += OnLoaded;
			SizeChanged += OnSizeChanged;
			_textView.LayoutChanged += OnTextViewLayoutChanged;
			_markerTagAggregator.TagsChanged += OnTagsChanged;
			_errorTagAggregator.TagsChanged += OnTagsChanged;
			if (HighlightWordTaggerProvider.Taggers.ContainsKey(_textView))
			{
				HighlightWordTaggerProvider.Taggers[_textView].TagsChanged += OnHighlightChanged;
			}

			MouseLeftButtonDown += OnMouseLeftButtonDown;
			MouseMove += OnMouseMove;
			MouseLeftButtonUp += OnMouseLeftButtonUp;
			MouseRightButtonDown += OnMouseRightButtonDown;
		}

		private void OnLoaded(object sender, RoutedEventArgs e)
		{
			Invalidate(Parts.All);
		}

		private void OnSizeChanged(object sender, SizeChangedEventArgs e)
		{
			Invalidate(Parts.Marks | Parts.Text);
		}

		private void OnTextViewLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
		{
			Parts parts = 0;
			if (e.NewViewState.EditSnapshot != e.OldViewState.EditSnapshot ||
				e.NewViewState.VisualSnapshot != e.OldViewState.VisualSnapshot)
			{
				parts |= Parts.TextContent;
			}

			Invalidate(parts);
		}

		private void OnTagsChanged(object sender, EventArgs e)
		{
			Invalidate(Parts.Marks);
		}

		private void OnHighlightChanged(object sender, EventArgs e)
		{
			Invalidate(Parts.All);
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

		private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (HighlightWordTaggerProvider.Taggers.ContainsKey(_textView))
			{
				HighlightWordTaggerProvider.Taggers[_textView].Clear();
			}
		}

		private void Invalidate(Parts parts)
		{
			// Not visible anyway
			if (ActualWidth <= 0.0)
			{
				return;
			}

			if (Options.RenderTextEnabled)
			{
				_textRenderer.Invalidate(parts);
			}

			MarksVisual.Dispatcher.BeginInvoke(new Action(RenderMarks), DispatcherPriority.Render);
		}

		private void RenderMarks()
		{
			if (_textView.IsClosed)
			{
				return;
			}

			using (DrawingContext drawingContext = MarksVisual.RenderOpen())
			{
				// Render viewport
				double textHeight = Math.Min(_textRenderer.Height, DrawHeight);
				int viewportHeight = Math.Max((int)((_textView.ViewportHeight / _textView.LineHeight) * (textHeight / _textRenderer.Height)), 5);
				int firstLine = (int)_scrollBar.GetYCoordinateOfBufferPosition(new SnapshotPoint(_textView.TextViewLines.FirstVisibleLine.Snapshot, _textView.TextViewLines.FirstVisibleLine.Start));

				drawingContext.DrawRectangle(Colors.VisibleRegionBrush, null, new Rect(0.0, firstLine, ActualWidth, viewportHeight));

				// Render various marks
				_changeRenderer.Colors = Colors;
				_changeRenderer.Render(drawingContext);

				_highlightRenderer.Colors = Colors;
				_highlightRenderer.Render(drawingContext);

				_markerRenderer.Colors = Colors;
				_markerRenderer.Render(drawingContext);

				if (Options.CursorBorderEnabled)
				{
					drawingContext.DrawRectangle(null, Colors.VisibleRegionBorderPen, new Rect(0.5, firstLine + 0.5, ActualWidth - 1.0, viewportHeight - 1.0));
				}
			}
		}

		private void ScrollViewToYCoordinate(double y)
		{
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