using eScape.Core;
using eScape.Host;
using eScapeLLC.UWP.Composition.Charts.Events;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Templated Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234235

namespace eScapeLLC.UWP.Composition.Charts {
	#region ChartDataSourceCollection
	/// <summary>
	/// This is to appease the XAML infrastruction which eschews generic classes as property type.
	/// </summary>
	public class ChartDataSourceCollection : ObservableCollection<DataSource> { }
	#endregion
	#region ChartComponentCollection
	/// <summary>
	/// This is to appease the XAML infrastruction which eschews generic classes as property type.
	/// </summary>
	public class ChartComponentCollection : ObservableCollection<ChartComponent> { }
	#endregion
	#region LayoutState
	/// <summary>
	/// Keeps track of layout state between refreshes.
	/// </summary>
	public class LayoutState {
		#region properties
		/// <summary>
		/// Current dimensions.
		/// MUST NOT be (NaN,NaN) or (0,0).
		/// </summary>
		public Size Dimensions { get; set; }
		/// <summary>
		/// The "starting" layout rectangle.
		/// MAY account for Padding.
		/// Initialized by <see cref="InitializeLayoutContext"/>
		/// </summary>
		public Rect LayoutRect { get; private set; }
		/// <summary>
		/// The size of LayoutRect.
		/// Initialized by <see cref="InitializeLayoutContext"/>
		/// </summary>
		public Size LayoutDimensions { get; private set; }
		/// <summary>
		/// Current layout context.
		/// Initialized by <see cref="InitializeLayoutContext"/>
		/// </summary>
		public DefaultLayoutContext Layout { get; set; }
		/// <summary>
		/// Value to provide for <see cref="IChartRenderContext.Type"/>.
		/// </summary>
		public RenderType Type { get; set; }
		#endregion
		#region data
		/// <summary>
		/// Cache for render contexts.
		/// </summary>
		readonly Dictionary<ChartComponent, DefaultRenderContext> rendercache = new Dictionary<ChartComponent, DefaultRenderContext>();
		#endregion
		#region public
		/// <summary>
		/// Whether the given dimensions are different from <see cref="Dimensions"/>
		/// </summary>
		/// <param name="sz">New dimensions.</param>
		/// <returns></returns>
		public bool IsSizeChanged(Size sz) {
			return (Dimensions.Width != sz.Width || Dimensions.Height != sz.Height);
		}
		/// <summary>
		/// Calculate the initial layout rect.
		/// </summary>
		/// <param name="padding">Amount to subtract from rect.</param>
		/// <returns>Rectangle minus padding.</returns>
		Rect Initial(Thickness padding) {
			// ensure w/h are GE zero
			var wid = padding.Left + padding.Right >= Dimensions.Width ? Dimensions.Width : Dimensions.Width - padding.Left - padding.Right;
			var hgt = padding.Top + padding.Bottom >= Dimensions.Height ? Dimensions.Height : Dimensions.Height - padding.Top - padding.Bottom;
			return new Rect(padding.Left, padding.Top, wid, hgt);
		}
		/// <summary>
		/// Recreate the layout context.
		/// Sets <see cref="LayoutRect"/>, <see cref="LayoutDimensions"/>, <see cref="Layout"/>.
		/// Clears <see cref="rendercache"/>.
		/// </summary>
		/// <param name="padding"></param>
		public void InitializeLayoutContext(Thickness padding) {
			LayoutRect = Initial(padding);
			LayoutDimensions = new Size(LayoutRect.Width, LayoutRect.Height);
			Layout = new DefaultLayoutContext(LayoutDimensions, LayoutRect);
			rendercache.Clear();
		}
		/// <summary>
		/// Provide a render context for given component.
		/// Created contexts are cached until <see cref="InitializeLayoutContext"/> is called.
		/// <para/>
		/// Sets the <see cref="DefaultRenderContext.Type"/> to the current value of <see cref="Type"/>.
		/// </summary>
		/// <param name="cc">Component to provide context for.</param>
		/// <param name="surf">For ctor.</param>
		/// <param name="ccs">For ctor.</param>
		/// <param name="dc">For ctor.</param>
		/// <returns>New or cached instance.</returns>
		public DefaultRenderContext RenderFor(ChartComponent cc, Canvas surf, ObservableCollection<ChartComponent> ccs, object dc) {
			if (rendercache.ContainsKey(cc)) {
				var rc = rendercache[cc];
				rc.Type = Type;
				return rc;
			}
			var rect = Layout.For(cc);
			var drc = new DefaultRenderContext(surf, ccs, LayoutDimensions, rect, Layout.RemainingRect, dc);
			rendercache.Add(cc, drc);
			drc.Type = Type;
			return drc;
		}
		#endregion
	}
	#endregion
	#region ChartErrorEventArgs
	/// <summary>
	/// Represents the error event args.
	/// </summary>
	public class ChartErrorEventArgs : EventArgs {
		/// <summary>
		/// The validation results array.
		/// </summary>
		public ChartValidationResult[] Results { get; private set; }
		/// <summary>
		/// Ctor.
		/// </summary>
		/// <param name="cvr"></param>
		public ChartErrorEventArgs(params ChartValidationResult[] cvr) { Results = cvr; }
	}
	#endregion
	#region Chart
	[TemplatePart(Name = PART_Canvas, Type = typeof(Canvas))]
	public sealed class Chart : Control {
		static readonly LogTools.Flag _trace = LogTools.Add("Chart", LogTools.Level.Verbose);
		/// <summary>
		/// Control template part: canvas.
		/// </summary>
		public const String PART_Canvas = "PART_Canvas";
		#region properties
		/// <summary>
		/// The list of data sources.
		/// </summary>
		public ChartDataSourceCollection DataSources { get; private set; }
		/// <summary>
		/// The chart's visual components.
		/// Obtained from the XAML and programmatic.
		/// </summary>
		public ChartComponentCollection Components { get; private set; }
		/// <summary>
		/// Components that are IChartAxis.
		/// </summary>
		List<IChartAxis> Axes { get; set; }
		EventBus Bus { get; set; }
		/// <summary>
		/// Obtained from the templated parent.
		/// </summary>
		Canvas Surface { get; set; }
		/// <summary>
		/// Last-computed layout state.
		/// LayoutUpdated gets called frequently, so it gets debounced.
		/// </summary>
		LayoutState CurrentLayout { get; set; }
		/// <summary>
		/// Components that entered before the Surface was ready (via XAML).
		/// </summary>
		List<ChartComponent> DeferredEnter { get; set; }
		#endregion
		#region ctor
		public Chart() {
			this.DefaultStyleKey = typeof(Chart);
			DeferredEnter = new List<ChartComponent>();
			Axes = new List<IChartAxis>();
			DataSources = new ChartDataSourceCollection();
			DataSources.CollectionChanged += DataSources_CollectionChanged;
			Components = new ChartComponentCollection();
			Components.CollectionChanged += new NotifyCollectionChangedEventHandler(Components_CollectionChanged);
			LayoutUpdated += new EventHandler<object>(Chart_LayoutUpdated);
			SizeChanged += Chart_SizeChanged;
			DataContextChanged += Chart_DataContextChanged;
			Bus = new EventBus();
			CurrentLayout = new LayoutState();
			Bus.RegisterInstance(this);
		}
		#endregion
		#region events
		/// <summary>
		/// Event to receive notification of error info.
		/// This can help detect configuration or other runtime chart processing errors.
		/// </summary>
		public event TypedEventHandler<Chart, ChartErrorEventArgs> ChartError;
		#endregion
		#region extensions
		List<IChartLayer> Layers { get; set; } = new List<IChartLayer>();
		List<IChartCompositionLayer> Compositions { get; set; } = new List<IChartCompositionLayer>();
		/// <summary>
		/// Obtain UI elements from the control template.
		/// Happens Before Chart_LayoutUpdated.
		/// </summary>
		protected override void OnApplyTemplate() {
			try {
				Surface = GetTemplateChild(PART_Canvas) as Canvas;
				_trace.Verbose($"OnApplyTemplate ({Width}x{Height}) {Surface} d:{DeferredEnter.Count}");
				var celc = new DefaultEnterLeaveContext(Surface, Components, Layers, Compositions, DataContext);
				foreach (var cc in DeferredEnter) {
					ComponentEnter(celc, cc);
				}
				DeferredEnter.Clear();
				if (celc.Errors.Count > 0) {
					Report(celc.Errors.ToArray());
				}
			}
			finally {
				base.OnApplyTemplate();
			}
		}
		#endregion
		#region evhs
		private void Components_CollectionChanged(object sender, NotifyCollectionChangedEventArgs nccea) {
			_trace.Verbose($"ComponentsChanged {nccea.Action}");
			var celc = new DefaultEnterLeaveContext(Surface, Components, Layers, Compositions, DataContext);
			try {
				if (nccea.OldItems != null) {
					foreach (ChartComponent cc in nccea.OldItems) {
						_trace.Verbose($"leave '{cc.Name}' {cc}");
						//cc.RefreshRequest -= ChartComponent_RefreshRequest;
						ComponentLeave(celc, cc);
					}
				}
				if (nccea.NewItems != null) {
					foreach (ChartComponent cc in nccea.NewItems) {
						_trace.Verbose($"enter '{cc.Name}' {cc}");
						//cc.RefreshRequest -= ChartComponent_RefreshRequest;
						//cc.RefreshRequest += ChartComponent_RefreshRequest;
						cc.DataContext = DataContext;
						if (Surface != null) {
							ComponentEnter(celc, cc);
						}
						else {
							DeferredEnter.Add(cc);
						}
					}
				}
			}
			catch (Exception ex) {
				_trace.Error($"{Name} Components_CollectionChanged.unhandled: {ex}");
			}
			if (celc.Errors.Count > 0) {
				Report(celc.Errors.ToArray());
			}
			if (Surface != null) {
				InvalidateArrange();
			}
		}
		private void DataSources_CollectionChanged(object sender, NotifyCollectionChangedEventArgs nccea) {
			_trace.Verbose($"DataSourcesChanged {nccea.Action}");
			try {
				if (nccea.OldItems != null) {
					foreach (DataSource ds in nccea.OldItems) {
						_trace.Verbose($"leave '{ds.Name}' {ds}");
						//ds.RefreshRequest -= DataSource_RefreshRequest;
					}
				}
				if (nccea.NewItems != null) {
					foreach (DataSource ds in nccea.NewItems) {
						_trace.Verbose($"enter '{ds.Name}' {ds}");
						if (ds.Items != null && !ds.IsDirty && ds.Items.GetEnumerator().MoveNext()) {
							// force this dirty so it refreshes
							ds.IsDirty = true;
						}
						//ds.RefreshRequest -= DataSource_RefreshRequest;
						//ds.RefreshRequest += DataSource_RefreshRequest;
						ds.DataContext = DataContext;
					}
				}
			}
			catch (Exception ex) {
				_trace.Error($"{Name} DataSources_CollectionChanged.unhandled: {ex}");
			}
			if (Surface != null) {
				Refresh(CurrentLayout);
			}
		}
		private void Chart_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args) {
			_trace.Verbose($"DataContextChanged {args.NewValue}");
			Bus.Consume(args);
			args.Handled = true;
		}
		private void Chart_SizeChanged(object sender, SizeChangedEventArgs e) {
			// propagate to composition layers
			foreach(var layer in Compositions) {
				(layer as CompositionLayer).SizeChanged(e);
			}
		}
		private void Chart_LayoutUpdated(object sender, object e) {
			// This is (NaN,NaN) if we haven't been sized yet
			var sz = new Size(ActualWidth, ActualHeight);
			//_trace.Verbose($"LayoutUpdated ({sz.Width}x{sz.Height})");
			if (!double.IsNaN(sz.Width) && !double.IsNaN(sz.Height)) {
				// we are sized; see if dimensions actually changed
				if (sz.Width == 0 || sz.Height == 0) return;
				if (CurrentLayout.IsSizeChanged(sz)) {
					_trace.Verbose($"LayoutUpdated.trigger ({sz.Width}x{sz.Height})");
					var ls = new LayoutState() { Dimensions = sz, Layout = CurrentLayout.Layout };
					try {
						Refresh(ls);
					}
					catch (Exception ex) {
						_trace.Error($"{ex}");
					}
					finally {
						CurrentLayout = ls;
					}
				}
			}
		}
		#endregion
		#region helpers
		/// <summary>
		/// Report event(s).
		/// MUST be on Dispatcher thread!
		/// </summary>
		/// <param name="cvr">The event(s) to report.</param>
		void Report(params ChartValidationResult[] cvr) {
			ChartError?.Invoke(this, new ChartErrorEventArgs(cvr));
		}
		void ComponentEnter(IChartEnterLeaveContext icelc, ChartComponent cc) {
			// invoke IREL
			if (cc is IRequireEnterLeave irel) {
				irel.Enter(icelc);
			}
			Bus.RegisterInstance(cc);
		}
		void ComponentLeave(IChartEnterLeaveContext icelc, ChartComponent cc) {
			Bus.UnregisterInstance(cc);
			// invoke IREL
			if (cc is IRequireEnterLeave irel) {
				irel.Leave(icelc);
			}
		}
		/// <summary>
		/// Perform a full layout and rendering pass.
		/// At least ONE component reported as dirty.
		/// The full rendering sequence is: axis-reset, layout, render, transforms.
		/// SETs <see cref="LayoutState.Type"/> to FALSE.
		/// </summary>
		/// <param name="ls">Layout state.</param>
		void FullLayout(LayoutState ls) {
			ls.Type = RenderType.Full;
			var thk = new Thickness(Padding.Left + Margin.Left, Padding.Top + Margin.Top, Padding.Right + Margin.Right, Padding.Bottom + Margin.Bottom);
			ls.InitializeLayoutContext(Padding);
			_trace.Verbose($"full starting {ls.LayoutRect}");
			// Phase I: reset axes
			Bus.Consume(new Phase_InitializeAxes(Bus));
			// Phase II: claim space (IRequireLayout)
			Bus.Consume(new Phase_Layout(ls.Layout));
			// what's left is for the data series area
			_trace.Verbose($"remaining {ls.Layout.RemainingRect}");
			ls.Layout.FinalizeRects();
			Bus.Consume(new Phase_LayoutComplete(ls));
			// Phase III: data source rendering pipeline (IDataSourceRenderer)
			var ddsrc = new DefaultDataSourceRenderContext(Surface, Components, ls.LayoutDimensions, Rect.Empty, ls.Layout.RemainingRect, DataContext);
			foreach(var ds in DataSources) {
				ds.Render(Bus, ddsrc);
			}
			// axes receive extents from series broadcast on the EB
			//Phase_AxisLimits(ValueExtents_DataSeries.Items);
			// Phase IV: render non-axis components (IRequireRender)
			Bus.Consume(new Phase_RenderComponents(ls, Surface, Components, DataContext));
			// TODO axes receive extents from decorations broadcast on the EB
			//Phase_AxisLimits(ValueExtents_NotDataSeries.Items);
			// Phase V: axes finalized
			//Phase_AxesFinalized(ls);
			// Phase VI: post-axes finalized
			//Phase_RenderPostAxesFinalized(ls);
			// axes broadcast final extents on EB
			Bus.Consume(new Phase_FinalizeAxes(ls, Bus));
			// Phase VII: render axes (IRequireRender)
			Bus.Consume(new Phase_RenderAxes(ls, Surface, Components, DataContext));
			// Phase VIII: configure all transforms
			Bus.Consume(new Phase_RenderTransforms(ls, Surface, Components, DataContext));
		}
		void TransformsLayout(LayoutState ls) {
			ls.Type = RenderType.TransformsOnly;
			ls.InitializeLayoutContext(Padding);
			_trace.Verbose($"transforms-only starting {ls.LayoutRect}");
			//Phase_Layout(ls);
			Bus.Consume(new Phase_Layout(ls.Layout));
			_trace.Verbose($"remaining {ls.Layout.RemainingRect}");
			ls.Layout.FinalizeRects();
			Bus.Consume(new Phase_LayoutComplete(ls));
			//Phase_Transforms(ls);
			Bus.Consume(new Phase_RenderTransforms(ls, Surface, Components, DataContext));
		}
		/// <summary>
		/// Top-level render components.
		/// </summary>
		/// <param name="message"></param>
		void Refresh(LayoutState ls) {
			_trace.Verbose($"render-components {ls.Dimensions.Width}x{ls.Dimensions.Height}");
			if (ls.Dimensions.Width == 0 || ls.Dimensions.Height == 0) {
				return;
			}
			if (DataSources.Cast<DataSource>().Any((ds) => ds.IsDirty)) {
				FullLayout(ls);
			}
			else {
				TransformsLayout(ls);
			}
		}
		#endregion
	}
	#endregion
}
