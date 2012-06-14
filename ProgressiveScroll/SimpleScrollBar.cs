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

		private double _extraHeight = 34.0;

		double _trackSpanTop;
		double _trackSpanBottom;
		double _scale = 1.0;

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
			_scrollMap.ScrollMap = _scrollMapFactory.Create(_textView, !_useElidedCoordinates);
		}

		private void ResetTrackSpan()
		{
			_scale = (_realScrollBarMargin.VisualElement.ActualHeight + _extraHeight) / _textView.VisualSnapshot.LineCount;
			_scale = Math.Min(_scale, 1.0);

			_trackSpanTop = 0;
			_trackSpanBottom = _scale * _textView.VisualSnapshot.LineCount;
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

		public SimpleScrollBar(IWpfTextView textView, IWpfTextViewMargin containerMargin, IScrollMapFactoryService scrollMapFactory, bool useElidedCoordinates)
		{
			_textView = textView;
			_textView.LayoutChanged += OnLayoutChanged;

			_realScrollBarMargin = containerMargin.GetTextViewMargin(PredefinedMarginNames.VerticalScrollBar) as IWpfTextViewMargin;

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
		}

		void OnScrollBarIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			this.ResetTrackSpan();

			if (_useElidedCoordinates)
				this.ResetScrollMap();  //This will indirectly cause RaiseTrackChangedEvent to be called.
			else
				this.RaiseTrackChangedEvent();
		}

		void OnScrollBarTrackSpanChanged(object sender, EventArgs e)
		{
			if (this.UseRealScrollBarTrackSpan)
			{
				this.ResetTrackSpan();
				this.RaiseTrackChangedEvent();
			}
		}

		void OnLayoutChanged(object sender, EventArgs e)
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
			return _scale * _scrollMap.GetCoordinateAtBufferPosition(bufferPosition);
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
			return _scrollMap.GetBufferPositionAtCoordinate(y / _scale);
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
				return _textView.ViewportHeight / _textView.LineHeight;
			}
		}

		public event EventHandler TrackSpanChanged;
		#endregion
	}
}
