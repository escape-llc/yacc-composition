using eScape.Core;
using eScape.Host;
using eScapeLLC.UWP.Charts.Composition.Events;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace eScapeLLC.UWP.Charts.Composition {
	#region IValueAxisLabelSelectorContext
	/// <summary>
	/// Context passed to the <see cref="IValueConverter"/> for value axis <see cref="Style"/> selection etc.
	/// </summary>
	public interface IValueAxisLabelSelectorContext : IAxisLabelSelectorContext {
		/// <summary>
		/// List of all tick values, in order of layout.
		/// MAY NOT be in sorted order!
		/// </summary>
		TickState[] AllTicks { get; }
		/// <summary>
		/// The computed tick interval.
		/// </summary>
		double TickInterval { get; }
		/// <summary>
		/// List of previously-generated ticks, in order of layout.
		/// MAY NOT be in sorted order!
		/// </summary>
		List<TickState> GeneratedTicks { get; }
	}
	#endregion
	#region ValueAxisSelectorContext
	/// <summary>
	/// Context for value axis selectors.
	/// </summary>
	public class ValueAxisSelectorContext : IValueAxisLabelSelectorContext {
		/// <summary>
		/// <see cref="IAxisLabelSelectorContext.Index"/>.
		/// </summary>
		public int Index { get; private set; }
		/// <summary>
		/// <see cref="IValueAxisLabelSelectorContext.AllTicks"/>.
		/// </summary>
		public TickState[] AllTicks { get; private set; }
		/// <summary>
		/// <see cref="IValueAxisLabelSelectorContext.TickInterval"/>.
		/// </summary>
		public double TickInterval { get; private set; }
		/// <summary>
		/// <see cref="IAxisLabelSelectorContext.Axis"/>.
		/// </summary>
		public IChartAxis Axis { get; private set; }
		/// <summary>
		/// <see cref="IAxisLabelSelectorContext.Area"/>.
		/// </summary>
		public Rect Area { get; private set; }
		/// <summary>
		/// <see cref="IValueAxisLabelSelectorContext.GeneratedTicks"/>.
		/// </summary>
		public List<TickState> GeneratedTicks { get; private set; }
		/// <summary>
		/// Ctor.
		/// </summary>
		/// <param name="ica"></param>
		/// <param name="rc"></param>
		/// <param name="ticks"></param>
		/// <param name="ti"></param>
		public ValueAxisSelectorContext(IChartAxis ica, Rect rc, TickState[] ticks, double ti) { Axis = ica; Area = rc; AllTicks = ticks; TickInterval = ti; GeneratedTicks = new List<TickState>(); }
		/// <summary>
		/// Set the current index.
		/// </summary>
		/// <param name="idx"></param>
		public void SetTick(int idx) { Index = idx; }
		/// <summary>
		/// Add to the list of generated ticks.
		/// </summary>
		/// <param name="dx"></param>
		public void Generated(TickState dx) { GeneratedTicks.Add(dx); }
	}
	#endregion
	#region ValueAxis
	public class ValueAxis : AxisCommon, IChartAxis, IRequireEnterLeave,
		IConsumer<Phase_InitializeAxes>, IConsumer<Phase_AxisExtents>, IConsumer<Phase_Layout>,
		IConsumer<Phase_ModelComplete>, IConsumer<Phase_RenderTransforms> {
		static readonly LogTools.Flag _trace = LogTools.Add("ValueAxis", LogTools.Level.Error);
		#region inner
		abstract class Axis_State : ItemStateCore {
			internal readonly FrameworkElement Element;
			internal readonly TickState Tick;
			// these are used for JIT re-positioning
			internal double dim;
			internal double yorigin;
			internal double xorigin;
			internal Axis_State(int index, FrameworkElement element, TickState tick) : base(index) {
				Element = element;
				Tick = tick;
			}
			protected abstract void SizeElement(FrameworkElement element);
			protected abstract Point GetLocation(FrameworkElement element);
			internal Point? UpdateLocation() {
				if (Element != null) {
					SizeElement(Element);
					var loc = GetLocation(Element);
					Element.Translation = new Vector3((float)loc.X, (float)loc.Y, 0);
					return loc;
				}
				return null;
			}
		}
		class AxisState_Vertical : Axis_State {
			internal AxisState_Vertical(FrameworkElement element, TickState tick) : base(tick.Index, element, tick) { }
			protected override Point GetLocation(FrameworkElement element) {
				return new Point(xorigin, yorigin - element.ActualHeight / 2);
			}
			protected override void SizeElement(FrameworkElement element) {
				element.Width = dim;
			}
		}
		class AxisState_Horizontal : Axis_State {
			internal AxisState_Horizontal(FrameworkElement element, TickState tick) : base(tick.Index, element, tick) { }
			protected override Point GetLocation(FrameworkElement element) {
				return new Point(xorigin - element.ActualWidth / 2, yorigin);
			}
			protected override void SizeElement(FrameworkElement element) {
				element.Height = dim;
			}
		}
		#endregion
		#region DPs
		/// <summary>
		/// Identifies <see cref="LabelTemplate"/> dependency property.
		/// </summary>
		public static readonly DependencyProperty LabelTemplateProperty = DependencyProperty.Register(
			nameof(LabelTemplate), typeof(DataTemplate), typeof(ValueAxis), new PropertyMetadata(null)
		);
		/// <summary>
		/// Identifies <see cref="LabelStyle"/> dependency property.
		/// </summary>
		public static readonly DependencyProperty LabelStyleProperty = DependencyProperty.Register(
			nameof(LabelStyle), typeof(Style), typeof(AxisCommon), new PropertyMetadata(null)
		);
		#endregion
		#region properties
		/// <summary>
		/// Whether to reverse the direction of the axis.
		/// </summary>
		public bool Reverse { get; set; }
		/// <summary>
		/// Alternate label format string.
		/// </summary>
		public string LabelFormatString { get; set; }
		/// <summary>
		/// The style to apply to labels.
		/// </summary>
		public Style LabelStyle { get { return (Style)GetValue(LabelStyleProperty); } set { SetValue(LabelStyleProperty, value); } }
		/// <summary>
		/// Converter to use as the element <see cref="FrameworkElement.Style"/> and <see cref="TextBlock.Text"/> selector.
		/// These are already set to their "standard" values before this is called, so it MAY selectively opt out of setting them.
		/// <para/>
		/// The <see cref="IValueConverter.Convert"/> targetType parameter is used to determine which value is requested.
		/// <para/>
		/// Uses <see cref="Tuple{Style,String}"/> for style/label override.  Return a new instance/NULL to opt in/out.
		/// </summary>
		public IValueConverter LabelFormatter { get; set; }
		/// <summary>
		/// Converter to use as the label creation selector.
		/// If it returns True, the label is created.
		/// The <see cref="IValueConverter.Convert"/> targetType parameter is <see cref="bool"/>.
		/// SHOULD return a <see cref="bool"/> but MAY return NULL/not-NULL.
		/// </summary>
		public IValueConverter LabelSelector { get; set; }
		/// <summary>
		/// If set, the template to use for labels.
		/// This overrides <see cref="AxisCommon.LabelStyle"/>.
		/// If this is not set, then <see cref="TextBlock"/>s are used and <see cref="AxisCommon.LabelStyle"/> applied to them.
		/// </summary>
		public DataTemplate LabelTemplate { get { return (DataTemplate)GetValue(LabelTemplateProperty); } set { SetValue(LabelTemplateProperty, value); } }
		/// <summary>
		/// The layer to manage components.
		/// </summary>
		protected IChartLayer Layer { get; set; }
		/// <summary>
		/// List of active TextBlocks for labels.
		/// </summary>
		protected List<ItemStateCore> AxisLabels { get; set; }
		protected Binding LabelBinding { get; set; }
		#endregion
		#region ctor
		public ValueAxis() {
			Type = AxisType.Value;
			Side = Side.Left;
			AxisLabels = new List<ItemStateCore>();
		}
		#endregion
		#region IRequireEnterLeave
		public void Enter(IChartEnterLeaveContext icelc) {
			Layer = icelc.CreateLayer();
		}
		public void Leave(IChartEnterLeaveContext icelc) {
			icelc.DeleteLayer(Layer);
			Layer = null;
		}
		#endregion
		#region handlers
		void IConsumer<Phase_InitializeAxes>.Consume(Phase_InitializeAxes message) {
			ResetLimits();
			var msg = new Axis_Extents(Name, Minimum, Maximum, Side, Type, Reverse);
			message.Register(msg);
		}
		void IConsumer<Phase_AxisExtents>.Consume(Phase_AxisExtents message) {
			ResetLimits();
			if (double.IsNaN(Minimum) || double.IsNaN(Maximum)) {
				foreach(var xx in message.Extents.Where(ax => ax.AxisName == Name)) {
					Extents(xx);
				}
			}
			var tc = new TickCalculator(Minimum, Maximum);
			var tix = tc.GetTicks().OrderBy(xx => xx.Index).ToImmutableArray();
			var msg = new Axis_Extents_TickValues(Name, Minimum, Maximum, Side, Type, Reverse, tix);
			message.Register(msg);
		}
		void IConsumer<Phase_Layout>.Consume(Phase_Layout message) {
			var space = AxisMargin + MinWidth;
			message.Context.ClaimSpace(this, Side, space);
		}
		void IConsumer<Phase_ModelComplete>.Consume(Phase_ModelComplete message) {
			if(double.IsNaN(Minimum) || double.IsNaN(Maximum)) return;
			var icrc = message.ContextFor(this);
			var padding = 2 * AxisMargin;
			var recycler = new Recycler<FrameworkElement, Axis_State>(AxisLabels.Cast<Axis_State>().Where(xx => xx.Element != null).Select(xx => xx.Element), state => {
				var fe = ElementFactory(state);
				if (Orientation == AxisOrientation.Vertical) {
					fe.Width = icrc.Area.Width - padding;
					if (fe is TextBlock tbb) {
						tbb.Padding = Side == Side.Right ? new Thickness(padding, 0, 0, 0) : new Thickness(0, 0, padding, 0);
					}
				}
				else {
					fe.Height = icrc.Area.Height - padding;
					if (fe is TextBlock tbb) {
						tbb.Padding = Side == Side.Bottom ? new Thickness(0, padding, 0, 0) : new Thickness(0, 0, 0, padding);
					}
				}
				return fe;
			});
			var tc = new TickCalculator(Minimum, Maximum);
			_trace.Verbose($"{Name} grid range:{tc.Range} tintv:{tc.TickInterval}");
			var itemstate = new List<ItemStateCore>();
			// materialize the ticks
			var tix = tc.GetTicks().OrderBy(xx => xx.Index).ToArray();
			var sc = new ValueAxisSelectorContext(this, icrc.Area, tix, tc.TickInterval);
			for (int ix = 0; ix < tix.Length; ix++) {
				//_trace.Verbose($"grid vx:{tick}");
				sc.SetTick(ix);
				var createit = true;
				if (LabelSelector != null) {
					// ask the label selector
					var ox = LabelSelector.Convert(sc, typeof(bool), null, System.Globalization.CultureInfo.CurrentUICulture.Name);
					if (ox is bool bx) {
						createit = bx;
					}
					else {
						createit = ox != null;
					}
				}
				if (!createit) continue;
				var (created, element) = recycler.Next(null);
				var tick = tix[ix];
				if (!created) {
					// restore binding if we are using a LabelFormatter
					if (LabelFormatter != null && LabelStyle != null) {
						BindTo(this, nameof(LabelStyle), element, StyleProperty);
					}
				}
				// default text
				var text = tick.Value.ToString(string.IsNullOrEmpty(LabelFormatString) ? "G" : LabelFormatString);
				if (LabelFormatter != null) {
					// call for Style, String override
					var format = LabelFormatter.Convert(sc, typeof(Tuple<Style, string>), null, System.Globalization.CultureInfo.CurrentUICulture.Name);
					if (format is Tuple<Style, string> ovx) {
						if (ovx.Item1 != null) {
							element.Style = ovx.Item1;
						}
						if (ovx.Item2 != null) {
							text = ovx.Item2;
						}
					}
				}
				var shim = new TextShim() { Text = text };
				element.DataContext = shim;
				BindTo(shim, nameof(Visibility), element, VisibilityProperty);
				var state = (Orientation == AxisOrientation.Horizontal
					? new AxisState_Horizontal(element, tick)
					: new AxisState_Vertical(element, tick) as ItemStateCore);
				sc.Generated(tick);
				itemstate.Add(state);
			}
			// VT and internal bookkeeping
			AxisLabels = itemstate;
			Layer.Remove(recycler.Unused);
			Layer.Add(recycler.Created);
			// for "horizontal" axis orientation it's important to get the TextBlocks sized
			// otherwise certain Style settings can resize it beyond the text bounds
			// use unparented element for text measuring
			var inf = new Size(Double.PositiveInfinity, Double.PositiveInfinity);
			foreach (Axis_State al in AxisLabels) {
				al.Element.Measure(inf);
			}
		}
		FrameworkElement ElementFactory(Axis_State _1) {
			var fe = default(FrameworkElement);
			if (LabelTemplate != null) {
				fe = LabelTemplate.LoadContent() as FrameworkElement;
			}
			else {
				// TODO bring back default template stuff
				fe = new TextBlock() {
					VerticalAlignment = VerticalAlignment.Center,
				};
			}
			fe.TranslationTransition = new Vector3Transition() {
				Duration = TimeSpan.FromMilliseconds(300),
				Components = Vector3TransitionComponents.X | Vector3TransitionComponents.Y
			};
			if (LabelStyle != null) {
				BindTo(this, nameof(LabelStyle), fe, FrameworkElement.StyleProperty);
			}
			fe.SizeChanged += Element_SizeChanged;
			return fe;
		}
		private void Element_SizeChanged(object sender, SizeChangedEventArgs e) {
#if false
			var vm = fe.DataContext as DataTemplateShim;
			_trace.Verbose($"{Name} sizeChanged ps:{e.PreviousSize} ns:{e.NewSize} text:{vm?.Text}");
#endif
			var fe = sender as FrameworkElement;
			if (fe.ActualWidth == 0 || fe.ActualHeight == 0) return;
			var state = AxisLabels.Cast<Axis_State>().SingleOrDefault(sis => sis.Element == fe);
			if (state != null) {
				var loc = state.UpdateLocation();
				_trace.Verbose($"{Name} sizeChanged[{state.Tick.Index}] loc:{loc} yv:{state.Tick.Value} o:({state.xorigin},{state.yorigin}) ns:{e.NewSize} ds:{fe.DesiredSize}");
			}
		}
		void IConsumer<Phase_RenderTransforms>.Consume(Phase_RenderTransforms message) {
			if (AxisLabels.Count == 0) return;
			if (double.IsNaN(Minimum) || double.IsNaN(Maximum)) return;
			var icrc = message.ContextFor(this);
			var scale = Orientation == AxisOrientation.Horizontal ? icrc.Area.Width / Range : icrc.Area.Height / Range;
			var pmatrix = ProjectionFor(icrc.Area, Reverse);
			var matx = Matrix3x2.Multiply(pmatrix.model, pmatrix.proj);
			_trace.Verbose($"{Name} transforms s:{scale:F3} matx:{matx} a:{icrc.Area} sa:{icrc.SeriesArea}");
			foreach (Axis_State state in AxisLabels) {
				if (state.Element == null) continue;
				var point = Orientation == AxisOrientation.Horizontal ? new Vector2((float)state.Tick.Value, 0) : new Vector2(0, (float)state.Tick.Value);
				var dc = Vector2.Transform(point, matx);
				switch (Orientation) {
					case AxisOrientation.Vertical:
						state.dim = icrc.Area.Width - AxisMargin;
						state.xorigin = icrc.Area.Left;
						state.yorigin = dc.Y;
						break;
					case AxisOrientation.Horizontal:
						state.dim = icrc.Area.Height - AxisMargin;
						state.xorigin = dc.X;
						state.yorigin = icrc.Area.Top + AxisMargin;
						break;
				}
				var loc = state.UpdateLocation();
				//_trace.Verbose($"{Name} el {state.Element.ActualWidth}x{state.Element.ActualHeight} v:{state.Tick.Value} @:({loc?.X},{loc?.Y})");
				if (icrc.Type != RenderType.TransformsOnly) {
					// doing render so (try to) trigger the SizeChanged handler
					state.Element.InvalidateMeasure();
					state.Element.InvalidateArrange();
				}
			}
		}
		#endregion
	}
	#endregion
}
