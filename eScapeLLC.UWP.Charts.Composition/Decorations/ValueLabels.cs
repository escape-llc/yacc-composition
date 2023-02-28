using eScape.Core;
using eScape.Host;
using eScapeLLC.UWP.Charts.Composition.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace eScapeLLC.UWP.Charts.Composition {
	#region ILabelSelectorContext
	/// <summary>
	/// Context passed to the <see cref="IValueConverter"/> for <see cref="Style"/> selection.
	/// </summary>
	public interface ILabelSelectorContext {
		/// <summary>
		/// The source of the item values.
		/// </summary>
		IProvideSeriesItemValues Source { get; }
		/// <summary>
		/// The value in question.
		/// </summary>
		ISeriesItemValue ItemValue { get; }
	}
	#endregion
	public class ValueLabels : ChartComponent, IRequireEnterLeave,
		IConsumer<Phase_DataSourceOperation>, IConsumer<Phase_Transforms> {
		static readonly LogTools.Flag _trace = LogTools.Add("ValueLabels", LogTools.Level.Error);
		#region inner
		class Item_State : ItemStateCore {
			internal FrameworkElement Element;
			internal readonly ISeriesItem Item;
			internal readonly ISeriesItemValueDouble ValueSource;
			internal readonly object CustomValue;
			readonly Point LabelOffset;
			Vector2 Center;
			Point Direction;
			public Item_State(ISeriesItem isi, ISeriesItemValueDouble value, object customValue, Point offset) : base(isi.Index) {
				Item = isi;
				ValueSource = value;
				CustomValue = customValue;
				LabelOffset = offset;
			}
			internal void Layout(Vector2 position, Point direction, RenderType type) {
				Center = position;
				Direction = direction;
				Element.InvalidateMeasure();
				Element.InvalidateArrange();
			}
			internal void Locate(float width, float height) {
				if(Element != null) {
					float hw = width / 2.0f, hh = height / 2.0f;
					float dx = Center.X - hw + (float)(LabelOffset.X * Direction.X) * hw;
					float dy = Center.Y - hh + (float)(LabelOffset.Y * Direction.Y) * hh;
					Element.Translation = new Vector3(dx, dy, 0);
				}
			}
		}
		#endregion
		#region SelectorContext
		/// <summary>
		/// Default implementation of <see cref="ILabelSelectorContext"/>.
		/// </summary>
		protected class SelectorContext : ILabelSelectorContext {
			/// <summary>
			/// <see cref="ILabelSelectorContext.Source"/>.
			/// </summary>
			public IProvideSeriesItemValues Source { get; private set; }
			/// <summary>
			/// <see cref="ILabelSelectorContext.ItemValue"/>.
			/// </summary>
			public ISeriesItemValue ItemValue { get; private set; }
			/// <summary>
			/// Ctor.
			/// </summary>
			/// <param name="ipsiv"></param>
			/// <param name="isiv"></param>
			public SelectorContext(IProvideSeriesItemValues ipsiv, ISeriesItemValue isiv) { Source = ipsiv; ItemValue = isiv; }
		}
		#endregion
		#region DPs
		/// <summary>
		/// Identifies <see cref="ValueChannel"/> dependency property.
		/// </summary>
		public static readonly DependencyProperty ValueChannelProperty = DependencyProperty.Register(
			nameof(ValueChannel), typeof(int), typeof(ValueLabels), new PropertyMetadata(0, new PropertyChangedCallback(PropertyChanged_ValueDirty))
		);
		/// <summary>
		/// Identifies <see cref="SourceName"/> dependency property.
		/// </summary>
		public static readonly DependencyProperty SourceNameProperty = DependencyProperty.Register(
			nameof(SourceName), typeof(string), typeof(ValueLabels), new PropertyMetadata(null, new PropertyChangedCallback(PropertyChanged_ValueDirty))
		);
		/// <summary>
		/// Identifies <see cref="LabelStyle"/> dependency property.
		/// </summary>
		public static readonly DependencyProperty LabelStyleProperty = DependencyProperty.Register(
			nameof(LabelStyle), typeof(Style), typeof(ValueLabels), new PropertyMetadata(null)
		);
		/// <summary>
		/// Identifies <see cref="LabelTemplate"/> dependency property.
		/// </summary>
		public static readonly DependencyProperty LabelTemplateProperty = DependencyProperty.Register(
			nameof(LabelTemplate), typeof(DataTemplate), typeof(ValueLabels), new PropertyMetadata(null)
		);
		#endregion
		#region ctor
		public ValueLabels() {
			ItemState = new List<ItemStateCore>();
		}
		#endregion
		#region properties
		/// <summary>
		/// The name of the source in the Components collection.
		/// The item values are obtained from this series.
		/// </summary>
		public string SourceName { get { return (string)GetValue(SourceNameProperty); } set { SetValue(SourceNameProperty, value); } }
		/// <summary>
		/// The value channel to display values for.
		/// </summary>
		public int ValueChannel { get { return (int)GetValue(ValueChannelProperty); } set { SetValue(ValueChannelProperty, value); } }
		/// <summary>
		/// The style to apply to (non-templated) labels.
		/// When using <see cref="LabelFormatter"/> this style can be overriden.
		/// </summary>
		public Style LabelStyle { get { return (Style)GetValue(LabelStyleProperty); } set { SetValue(LabelStyleProperty, value); } }
		/// <summary>
		/// If set, the template to use for labels.
		/// This overrides <see cref="LabelStyle"/>.
		/// If this is not set, then <see cref="TextBlock"/>s are used and <see cref="LabelStyle"/> applied to them.
		/// </summary>
		public DataTemplate LabelTemplate { get { return (DataTemplate)GetValue(LabelTemplateProperty); } set { SetValue(LabelTemplateProperty, value); } }
		/// <summary>
		/// Alternate format string for labels.
		/// </summary>
		public string LabelFormatString { get; set; }
		/// <summary>
		/// LabelOffset is translation from the "center" of the TextBlock.
		/// Units are PX coordinates, in Half-dimension based on TextBlock size.
		/// Y-up is negative.
		/// Default value is (0,0).
		/// </summary>
		public Point LabelOffset { get; set; } = new Point(0, 0);
		/// <summary>
		/// Placment offset is translation from "center" of a region.
		/// Units are WORLD coordinates.
		/// Y-up is positive.
		/// Default value is (0,0).
		/// </summary>
		public Point PlacementOffset { get; set; } = new Point(0, 0);
		/// <summary>
		/// Converter to use as the element <see cref="FrameworkElement.Style"/> and <see cref="TextShim.Text"/> selector.
		/// These are already set to their "standard" values before this is called, so it MAY selectively opt out of setting them.
		/// The <see cref="IValueConverter.Convert"/> targetType parameter is used to determine which value is requested.
		/// Uses <see cref="Tuple{Style,String}"/> for style/label override.  Return a new value or NULL (in each "slot") to opt in/out.
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
		/// The layer for components.
		/// </summary>
		protected IChartLayer Layer { get; set; }
		/// <summary>
		/// Current item state.
		/// </summary>
		protected List<ItemStateCore> ItemState { get; set; }
		public IProvideConsume Bus { get; set; }
		/// <summary>
		/// Dereferenced component to interrogate for values.
		/// </summary>
		protected ChartComponent Source { get; set; }
		#endregion
		#region helpers
		/// <summary>
		/// Generic DP property change handler.
		/// Calls ChartComponent.Refresh(ValueDirty, Unknown).
		/// </summary>
		/// <param name="ddo"></param>
		/// <param name="dpcea"></param>
		protected static void PropertyChanged_ValueDirty(DependencyObject ddo, DependencyPropertyChangedEventArgs dpcea) {
			var cc = ddo as ValueLabels;
			cc.Refresh(RefreshRequestType.ValueDirty, AxisUpdateState.Unknown);
		}
		/// <summary>
		/// Mark self as dirty and invoke the RefreshRequest event.
		/// </summary>
		/// <param name="rrt">Request type.</param>
		/// <param name="aus">Axis update status.</param>
		protected void Refresh(RefreshRequestType rrt, AxisUpdateState aus) {
			//RefreshRequest?.Invoke(this, new RefreshRequestEventArgs(rrt, aus, this));
			Forward?.Forward(new Component_RefreshRequest(new Component_Operation(this, rrt, aus)));
		}
		protected void EnsureComponents(IChartComponentContext icrc) {
			var icei = icrc as IChartErrorInfo;
			if (LabelTemplate == null) {
				//if (Theme?.TextBlockTemplate == null) {
				//	icei?.Report(new ChartValidationResult(NameOrType(), $"No {nameof(LabelTemplate)} and {nameof(Theme.TextBlockTemplate)} was not found", new[] { nameof(LabelTemplate), nameof(Theme.TextBlockTemplate) }));
				//}
			}
			if (Source == null && !string.IsNullOrEmpty(SourceName)) {
				Source = icrc.Find(SourceName);
			}
			else {
				icei?.Report(new ChartValidationResult(NameOrType(), $"Source '{SourceName}' was not found", new[] { nameof(Source), nameof(SourceName) }));
			}
		}
		/// <summary>
		/// Create the shim along with setting the initial (default) text value.
		/// </summary>
		/// <param name="isiv">Source value.</param>
		/// <returns>New instance.</returns>
		DataTemplateShim CreateShim(ISeriesItemValueDouble isiv) {
			var txt = isiv.DoubleValue.ToString(string.IsNullOrEmpty(LabelFormatString) ? "G" : LabelFormatString);
			if (isiv is ISeriesItemValueCustom isivc) {
				return new ObjectShim() { Visibility = Visibility, Text = txt, CustomValue = isivc.CustomValue };
			}
			return new TextShim() { Visibility = Visibility, Text = txt };
		}
		/// <summary>
		/// Element factory for recycler.
		/// Comes from a <see cref="DataTemplate"/> if the <see cref="LabelTemplate"/> was set.
		/// Otherwise comes from <see cref="IChartTheme.TextBlockTemplate"/>.
		/// </summary>
		/// <param name="isiv">Item value.</param>
		/// <returns>New element or NULL.</returns>
		FrameworkElement CreateElement(ISeriesItemValueDouble isiv) {
			var fe = default(FrameworkElement);
			if (LabelTemplate != null) {
				fe = LabelTemplate.LoadContent() as FrameworkElement;
			}
			//else if (Theme.TextBlockTemplate != null) {
			//	fe = Theme.TextBlockTemplate.LoadContent() as FrameworkElement;
			//	if (LabelStyle != null) {
			//		BindTo(this, nameof(LabelStyle), fe, FrameworkElement.StyleProperty);
			//	}
			//}
			if (fe != null) {
				// complete configuration
				var shim = CreateShim(isiv);
				// connect the shim to template root element's Visibility
				BindTo(shim, nameof(Visibility), fe, UIElement.VisibilityProperty);
				fe.DataContext = shim;
				fe.SizeChanged += Element_SizeChanged;
			}
			return fe;
		}
		/// <summary>
		/// Undo any bookkeeping done in <see cref="ElementPipeline"/>.
		/// </summary>
		/// <param name="fes"></param>
		protected void TeardownElements(IEnumerable<FrameworkElement> fes) {
			foreach (var fe in fes) {
				fe.DataContext = null;
				fe.SizeChanged -= Element_SizeChanged;
			}
		}
		/// <summary>
		/// Re-initialize a recycled element for a new application.
		/// </summary>
		/// <param name="target"></param>
		/// <param name="fe"></param>
		/// <param name="shim"></param>
		void RecycleElement(ISeriesItemValueDouble target, FrameworkElement fe, TextShim shim) {
			// recycling; update values
			var txt = target.DoubleValue.ToString(string.IsNullOrEmpty(LabelFormatString) ? "G" : LabelFormatString);
			shim.Visibility = Visibility;
			shim.Text = txt;
			if (shim is ObjectShim oshim && target is ISeriesItemValueCustom isivc2) {
				oshim.CustomValue = isivc2.CustomValue;
			}
			// restore binding if we are using a LabelFormatter
			if (LabelFormatter != null && LabelStyle != null) {
				BindTo(this, nameof(LabelStyle), fe, FrameworkElement.StyleProperty);
			}
		}
		/// <summary>
		/// If <see cref="LabelFormatter"/> and Element are defined, evaluate the formatter
		/// and apply the results.
		/// </summary>
		/// <param name="ipsiv">Used for evaluation context.</param>
		/// <param name="state">Target state.  The Element MUST be assigned for any effect.</param>
		void ApplyFormatter(IProvideSeriesItemValues ipsiv, Item_State state) {
			if (LabelFormatter == null) return;
			if (state.Element == null) return;
			var ctx = new SelectorContext(ipsiv, state.ValueSource);
			// TODO could call for typeof(object) and replace CustomValue
			var format = LabelFormatter.Convert(ctx, typeof(Tuple<Style, String>), null, System.Globalization.CultureInfo.CurrentUICulture.Name);
			if (format is Tuple<Style, String> ovx) {
				if (ovx.Item1 != null) {
					// TODO use error control because style may not match the template
					state.Element.Style = ovx.Item1;
				}
				if (ovx.Item2 != null) {
					if (state.Element.DataContext is TextShim ts) {
						ts.Text = ovx.Item2;
					}
				}
			}
		}
		/// <summary>
		/// If <see cref="LabelSelector"/> is defined, evaluate it and return the results.
		/// </summary>
		/// <param name="ipsiv">Used for evaluation context.</param>
		/// <param name="target">Item value.</param>
		/// <returns>true: select for label; false: not selected.</returns>
		bool ApplySelector(IProvideSeriesItemValues ipsiv, ISeriesItemValueDouble target) {
			if (LabelSelector == null) return true;
			// apply
			var ctx = new SelectorContext(ipsiv, target);
			var ox = LabelSelector.Convert(ctx, typeof(bool), null, System.Globalization.CultureInfo.CurrentUICulture.Name);
			if (ox is bool bx) {
				return bx;
			}
			else {
				return ox != null;
			}
		}
		/// <summary>
		/// Extract value info that matches our <see cref="ValueChannel"/>.
		/// </summary>
		/// <param name="siv">Candidate.</param>
		/// <returns>Matching value or NULL.</returns>
		ISeriesItemValueDouble ValueFor(ISeriesItem siv) {
			ISeriesItemValue target = null;
			if (siv is ISeriesItemValue isiv) {
				if (isiv.Channel == ValueChannel) {
					target = isiv;
				}
			}
			else if (siv is ISeriesItemValues isivs) {
				target = isivs.YValues.SingleOrDefault(yv => yv.Channel == ValueChannel);
			}
			return target as ISeriesItemValueDouble;
		}
		/// <summary>
		/// Create the state only; defer UI creation.
		/// </summary>
		/// <param name="siv">Source.  SHOULD be <see cref="ISeriesItemValueDouble"/>.</param>
		/// <returns>New instance or NULL.</returns>
		Item_State CreateState(ISeriesItem siv) {
			ISeriesItemValueDouble target = ValueFor(siv);
			if (target != null && !double.IsNaN(target.DoubleValue)) {
				var cv = target is ISeriesItemValueCustom isivc ? isivc.CustomValue : null;
				return new Item_State(siv, target, cv, LabelOffset);
			}
			return null;
		}
		#endregion
		#region evhs
		/// <summary>
		/// Follow-up handler to re-position the label element at exactly the right spot after it's done with (asynchronous) measure/arrange.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void Element_SizeChanged(object sender, SizeChangedEventArgs e) {
#if false
			var vm = fe.DataContext as TextShim;
			_trace.Verbose($"{Name} sizeChanged ps:{e.PreviousSize} ns:{e.NewSize} text:{vm?.Text}");
#endif
			var fe = sender as FrameworkElement;
			var state = ItemState.Cast<Item_State>().SingleOrDefault((sis) => sis.Element == fe);
			if (state != null) {
				//_trace.Verbose($"{Name}[{state.Index}] loc:{state.CanvasLocation} xvao:{state.XValueAfterOffset} yv:{state.Value} ns:{e.NewSize} ds:{fe.DesiredSize} offs:{LabelOffset}");
				state.Locate((float)e.NewSize.Width, (float)e.NewSize.Height);
			}
		}
		#endregion
		#region IRequireEnterLeave
		long token;
		public void Enter(IChartEnterLeaveContext icelc) {
			EnsureComponents(icelc as IChartComponentContext);
			Layer = icelc.CreateLayer();
			// TODO this should be replaced by messaging
			//if (Source is IProvideSeriesItemUpdates ipsiu) {
			//	ipsiu.ItemUpdates += Ipsiu_ItemUpdates;
			//}
			//_trace.Verbose($"{Name} enter s:{SourceName} {Source} v:{ValueAxis} c:{CategoryAxis}");
			//token = RegisterPropertyChangedCallback(UIElement.VisibilityProperty, PropertyChanged_Visibility);
		}
		public void Leave(IChartEnterLeaveContext icelc) {
			_trace.Verbose($"{Name} leave");
			//UnregisterPropertyChangedCallback(VisibilityProperty, token);
			// TODO this should be replaced by messaging
			//if (Source is IProvideSeriesItemUpdates ipsiu) {
			//	ipsiu.ItemUpdates -= Ipsiu_ItemUpdates;
			//}
			icelc.DeleteLayer(Layer);
			Layer = null;
		}
		#endregion
		#region handlers
		void IConsumer<Phase_DataSourceOperation>.Consume(Phase_DataSourceOperation message) {
			if (LabelTemplate == null /*&& Theme?.TextBlockTemplate == null */) {
				// already reported an error so this should be no surprise
				return;
			}
			var icrc = message.ContextFor(this);
			if (/*Source is IProvideSeriesItemUpdates && */icrc.Type == RenderType.Incremental) {
				// already handled it
				return;
			}
			if (Source is IProvideSeriesItemValues ipsiv) {
				// preamble
				var elements = ItemState.Cast<Item_State>().Select(ms => ms.Element).Where(el => el != null);
				var recycler = new Recycler<FrameworkElement, ISeriesItemValueDouble>(elements, CreateElement);
				var itemstate = new List<ItemStateCore>();
				// render
				foreach (var siv in ipsiv.SeriesItemValues) {
					var istate = CreateState(siv);
					if (istate != null) {
						itemstate.Add(istate);
						if (ApplySelector(ipsiv, istate.ValueSource)) {
							var (created, element) = recycler.Next(istate.ValueSource);
							if (!created && element.DataContext is TextShim shim) {
								RecycleElement(istate.ValueSource, element, shim);
							}
							istate.Element = element;
							ApplyFormatter(ipsiv, istate);
						}
					}
				}
				// postamble
				ItemState = itemstate;
				TeardownElements(recycler.Unused);
				Layer.Remove(recycler.Unused);
				Layer.Add(recycler.Created);
			}
		}
		void IConsumer<Phase_Transforms>.Consume(Phase_Transforms message) {
			if (ItemState.Count == 0) return;
			var icrc = message.ContextFor(this);
			_trace.Verbose($"{Name} transforms a:{icrc.Area} source:{Source?.Name} type:{icrc.Type}");
			if(Source is IProvideSeriesItemLayout ipsil) {
				var session = ipsil.Create(icrc.SeriesArea);
				foreach (Item_State state in ItemState) {
					if (state.Element == null) continue;
					_trace.Verbose($"{Name} el:{state.Element} ds:{state.Element.DesiredSize} as:{state.Element.ActualWidth},{state.Element.ActualHeight}");
					// Recalc and Position element now because it WILL NOT invoke EVH if size didn't actually change
					var position = session.Layout(state.ValueSource as ISeriesItem, PlacementOffset);
					_trace.Verbose($"\tpt:[{state.Index},{state.ValueSource?.DoubleValue}] layout:{position}");
					if (position != null) {
						state.Layout(position.Value.center, position.Value.direction, icrc.Type);
						state.Locate(state.Element.ActualSize.X, state.Element.ActualSize.Y);
					}
				}
			}
		}
		#endregion
	}
}
