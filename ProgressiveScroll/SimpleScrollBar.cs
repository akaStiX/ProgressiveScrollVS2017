using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Outlining;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.Utilities;

namespace ProgressiveScroll
{
	class SimpleScrollBar : IVerticalScrollBar
	{
		IScrollMapFactoryService _scrollMapFactory;
		private ScrollMapWrapper _scrollMap = new ScrollMapWrapper();
		private IWpfTextView _textView;
		private IWpfTextViewMargin _realScrollBarMargin;
		private IVerticalScrollBar _realScrollBar;
		private bool _useElidedCoordinates = false;

		double _trackSpanTop;
		double _trackSpanBottom;

		private class ScrollMapWrapper : IScrollMap
		{
			private IScrollMap _scrollMap;

			public ScrollMapWrapper()
			{
			}

			public IScrollMap ScrollMap
			{
				get { return _scrollMap; }
				set
				{
					if (_scrollMap != null)
					{
						_scrollMap.MappingChanged -= OnMappingChanged;
					}

					_scrollMap = value;

					_scrollMap.MappingChanged += OnMappingChanged;

					this.OnMappingChanged(this, new EventArgs());
				}
			}

			void OnMappingChanged(object sender, EventArgs e)
			{
				EventHandler handler = this.MappingChanged;
				if (handler != null)
					handler(sender, e);
			}

			public double GetCoordinateAtBufferPosition(SnapshotPoint bufferPosition)
			{
				return _scrollMap.GetCoordinateAtBufferPosition(bufferPosition);
			}

			public bool AreElisionsExpanded
			{
				get { return _scrollMap.AreElisionsExpanded; }
			}

			public SnapshotPoint GetBufferPositionAtCoordinate(double coordinate)
			{
				return _scrollMap.GetBufferPositionAtCoordinate(coordinate);
			}

			public double Start
			{
				get { return _scrollMap.Start; }
			}

			public double End
			{
				get { return _scrollMap.End; }
			}

			public double ThumbSize
			{
				get { return _scrollMap.ThumbSize; }
			}

			public ITextView TextView
			{
				get { return _scrollMap.TextView; }
			}

			public double GetFractionAtBufferPosition(SnapshotPoint bufferPosition)
			{
				return _scrollMap.GetFractionAtBufferPosition(bufferPosition);
			}

			public SnapshotPoint GetBufferPositionAtFraction(double fraction)
			{
				return _scrollMap.GetBufferPositionAtFraction(fraction);
			}

			public event EventHandler MappingChanged;
		}

		/// <summary>
		/// If true, map to the view's scrollbar; else map to the scrollMap.
		/// </summary>
		public bool UseElidedCoordinates
		{
			get { return _useElidedCoordinates; }
			set
			{
				if (value != _useElidedCoordinates)
				{
					_useElidedCoordinates = value;
					this.ResetScrollMap();
				}
			}
		}

		private void ResetScrollMap()
		{
			if (_useElidedCoordinates && this.UseRealScrollBarTrackSpan)
			{
				_scrollMap.ScrollMap = _realScrollBar.Map;
			}
			else
			{
				_scrollMap.ScrollMap = _scrollMapFactory.Create(_textView, !_useElidedCoordinates);
			}
		}

		private void ResetTrackSpan()
		{
			if (this.UseRealScrollBarTrackSpan)
			{
				_trackSpanTop = _realScrollBar.TrackSpanTop;
				_trackSpanBottom = _realScrollBar.TrackSpanBottom;
			}
			else
			{
				_trackSpanTop = 0.0;
				_trackSpanBottom = _textView.ViewportHeight;
			}

			//Ensure that the length of the track span is never 0.
			_trackSpanBottom = Math.Max(_trackSpanTop + 1.0, _trackSpanBottom);
		}

		private bool UseRealScrollBarTrackSpan
		{
			get
			{
				return (_realScrollBar != null) && (_realScrollBarMargin.VisualElement.Visibility == Visibility.Visible);
			}
		}

		void OnMappingChanged(object sender, EventArgs e)
		{
			this.RaiseTrackChangedEvent();
		}

		private void RaiseTrackChangedEvent()
		{
			EventHandler handler = this.TrackSpanChanged;
			if (handler != null)
				handler(this, new EventArgs());
		}

		public SimpleScrollBar(IWpfTextViewHost host, IWpfTextViewMargin containerMargin, IScrollMapFactoryService scrollMapFactory, bool useElidedCoordinates)
		{
			_textView = host.TextView;

			_realScrollBarMargin = containerMargin.GetTextViewMargin(PredefinedMarginNames.VerticalScrollBar) as IWpfTextViewMargin;

			System.Diagnostics.Debug.Print("container width: " + _realScrollBarMargin.MarginSize);

			if (_realScrollBarMargin != null)
			{
				_realScrollBar = _realScrollBarMargin as IVerticalScrollBar;
				if (_realScrollBar != null)
				{
					_realScrollBarMargin.VisualElement.IsVisibleChanged += OnScrollBarIsVisibleChanged;
					_realScrollBar.TrackSpanChanged += OnScrollBarTrackSpanChanged;
				}
			}
			this.ResetTrackSpan();

			_scrollMapFactory = scrollMapFactory;
			_useElidedCoordinates = useElidedCoordinates;
			this.ResetScrollMap();

			_scrollMap.MappingChanged += delegate { this.RaiseTrackChangedEvent(); };

			//container.SizeChanged += OnContainerSizeChanged;
		}

		void OnScrollBarIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			this.ResetTrackSpan();

			if (_useElidedCoordinates)
				this.ResetScrollMap();  //This will indirectly cause RaiseTrackChangedEvent to be called.
			else
				this.RaiseTrackChangedEvent();
		}

		void OnContainerSizeChanged(object sender, EventArgs e)
		{
			if (!this.UseRealScrollBarTrackSpan)
			{
				this.ResetTrackSpan();
				this.RaiseTrackChangedEvent();
			}
		}

		void OnScrollBarTrackSpanChanged(object sender, EventArgs e)
		{
			if (this.UseRealScrollBarTrackSpan)
			{
				this.ResetTrackSpan();
				this.RaiseTrackChangedEvent();
			}
		}

		#region IVerticalScrollBar Members
		public IScrollMap Map
		{
			get { return _scrollMap; }
		}

		public double GetYCoordinateOfBufferPosition(SnapshotPoint bufferPosition)
		{
			double scrollMapPosition = _scrollMap.GetCoordinateAtBufferPosition(bufferPosition);
			return this.GetYCoordinateOfScrollMapPosition(scrollMapPosition);
		}

		public double GetYCoordinateOfScrollMapPosition(double scrollMapPosition)
		{
			double minimum = _scrollMap.Start;
			double maximum = _scrollMap.End;
			double height = maximum - minimum;

			return this.TrackSpanTop + ((scrollMapPosition - minimum) * this.TrackSpanHeight) / (height + _scrollMap.ThumbSize);
		}

		public SnapshotPoint GetBufferPositionOfYCoordinate(double y)
		{
			double minimum = _scrollMap.Start;
			double maximum = _scrollMap.End;
			double height = maximum - minimum;

			double scrollCoordinate = minimum + (y - this.TrackSpanTop) * (height + _scrollMap.ThumbSize) / this.TrackSpanHeight;

			return _scrollMap.GetBufferPositionAtCoordinate(scrollCoordinate);
		}

		public double TrackSpanTop
		{
			get { return _trackSpanTop; }
		}

		public double TrackSpanBottom
		{
			get { return _trackSpanBottom; }
		}

		public double TrackSpanHeight
		{
			get { return _trackSpanBottom - _trackSpanTop; }
		}

		public double ThumbHeight
		{
			get
			{
				double minimum = _scrollMap.Start;
				double maximum = _scrollMap.End;
				double height = maximum - minimum;

				return _scrollMap.ThumbSize / (height + _scrollMap.ThumbSize) * this.TrackSpanHeight;
			}
		}

		public event EventHandler TrackSpanChanged;
		#endregion
	}
}
