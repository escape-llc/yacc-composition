using eScape.Core;
using eScape.Host;
using eScapeLLC.UWP.Charts.Composition.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace eScapeLLC.UWP.Charts.Composition {
	public class CategoryAxis : AxisCommon,
		IRequireEnterLeave, IChartAxis, IOperationController<CategoryAxis.Axis_ItemState>,
		IConsumer<Phase_InitializeAxes>, IConsumer<Phase_AxisExtents>, IConsumer<Phase_Layout>,
		IConsumer<Phase_DataSourceOperation>, IConsumer<Phase_Transforms> {
		static readonly LogTools.Flag _trace = LogTools.Add("CategoryAxis", LogTools.Level.Error);
		#region inner
		class Axis_ItemState : ItemStateCore {
			internal FrameworkElement Element;
			internal TextShim label;
			public Axis_ItemState(int index) : base(index) { }
			public void ResetElement() { Element = null; }
			public void SetElement(FrameworkElement cs) { Element = cs; }
		}
		#endregion
		#region DPs
		/// <summary>
		/// Identifies <see cref="LabelTemplate"/> dependency property.
		/// </summary>
		public static readonly DependencyProperty LabelTemplateProperty = DependencyProperty.Register(
			nameof(LabelTemplate), typeof(DataTemplate), typeof(CategoryAxis), new PropertyMetadata(null)
		);
		/// <summary>
		/// Identifies <see cref="LabelStyle"/> dependency property.
		/// </summary>
		public static readonly DependencyProperty LabelStyleProperty = DependencyProperty.Register(
			nameof(LabelStyle), typeof(Style), typeof(CategoryAxis), new PropertyMetadata(null)
		);
		#endregion
		#region properties
		/// <summary>
		/// Which data source to target.
		/// </summary>
		public string DataSourceName { get; set; }
		/// <summary>
		/// Whether to reverse the direction of the axis.
		/// </summary>
		public bool Reverse { get; set; }
		/// <summary>
		/// Select the label value.  TODO Use "." to bind to the entire data object in <see cref="ObjectShim"/>.
		/// </summary>
		public string LabelMemberPath { get; set; }
		/// <summary>
		/// The style to apply to labels.
		/// </summary>
		public Style LabelStyle { get { return (Style)GetValue(LabelStyleProperty); } set { SetValue(LabelStyleProperty, value); } }
		/// <summary>
		/// If set, the template to use for labels.
		/// This overrides <see cref="AxisCommon.LabelStyle"/>.
		/// If this is not set, then <see cref="TextBlock"/>s are used and <see cref="AxisCommon.LabelStyle"/> applied to them.
		/// </summary>
		public DataTemplate LabelTemplate { get { return (DataTemplate)GetValue(LabelTemplateProperty); } set { SetValue(LabelTemplateProperty, value); } }
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
		/// The layer to manage components.
		/// </summary>
		protected IChartLayer Layer { get; set; }
		/// <summary>
		/// List of active TextBlocks for labels.
		/// </summary>
		protected List<ItemStateCore> ItemState { get; set; }
		protected Binding LabelBinding { get; set; }
		#endregion
		#region ctor
		public CategoryAxis() {
			Type = AxisType.Category;
			Side = Side.Bottom;
			ItemState = new List<ItemStateCore>();
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
		#region IListController<CategoryAxis.Axis_ItemState>
		bool IsSelected(Axis_ItemState item) {
			if (LabelSelector != null) {
				// ask the label selector
				var ox = LabelSelector.Convert(null /*TODO FIX*/, typeof(bool), null, System.Globalization.CultureInfo.CurrentUICulture.Name);
				if (ox is bool bx) {
					return bx;
				}
				else {
					return ox != null;
				}
			}
			return true;
		}
		void UpdateStyle(Axis_ItemState item) {
			if (item == null || item.Element == null) return;
			// restore binding if we are using a LabelFormatter
			if (LabelFormatter != null && LabelStyle != null) {
				BindTo(this, nameof(LabelStyle), item.Element, FrameworkElement.StyleProperty);
			}
			//var text = tick.Value.ToString(string.IsNullOrEmpty(LabelFormatString) ? "G" : LabelFormatString);
			if (LabelFormatter != null) {
				// call for Style, String override
				var format = LabelFormatter.Convert(null /*TODO FIX*/, typeof(Tuple<Style, string>), null, System.Globalization.CultureInfo.CurrentUICulture.Name);
				if (format is Tuple<Style, string> ovx) {
					if (ovx.Item1 != null) {
						item.Element.Style = ovx.Item1;
					}
					if (ovx.Item2 != null) {
						item.label.Text = ovx.Item2;
					}
				}
			}
		}
		void Entering(Axis_ItemState item) {
			if (item != null && item.Element != null) {
				Layer.Add(item.Element);
			}
		}
		void Exiting(Axis_ItemState item) {
			if (item != null && item.Element != null) {
				Layer.Remove(item.Element);
				item.ResetElement();
			}
		}
		void IOperationController<Axis_ItemState>.EnteringItem(int index, ItemTransition it, Axis_ItemState item) {
			item.Reindex(index);
			bool elementSelected2 = IsSelected(item);
			if (elementSelected2) {
				item.SetElement(CreateElement(item.label));
			}
			UpdateStyle(item);
			Entering(item);
		}
		void IOperationController<Axis_ItemState>.LiveItem(int index, ItemTransition it, Axis_ItemState item) {
			item.Reindex(index);
			bool elementSelected = IsSelected(item);
			if (elementSelected && item.Element == null) {
				item.SetElement(CreateElement(item.label));
				Entering(item);
			}
			else if (!elementSelected && item.Element != null) {
				Exiting(item);
			}
			UpdateStyle(item);
		}
		void IOperationController<Axis_ItemState>.ExitingItem(int index, ItemTransition it, Axis_ItemState item) {
			if (item.Element != null) {
				Exiting(item);
			}
		}
		#endregion
		#region virtual data source handlers
		protected virtual void Reset(DataSource_Reset reset) {
			var ops = reset.CreateOperations(ItemState, Entering);
			UpdateCore(ops);
		}
		protected virtual void SlidingWindow(DataSource_SlidingWindow slidingWindow) {
			var ops = slidingWindow.CreateOperations(ItemState, Entering);
			UpdateCore(ops);
		}
		protected virtual void Add(DataSource_Add add) {
			var ops = add.CreateOperations(ItemState, Entering);
			UpdateCore(ops);
		}
		protected virtual void Remove(DataSource_Remove remove) {
			var ops = remove.CreateOperations<Axis_ItemState>(ItemState);
			UpdateCore(ops);
		}
		#endregion
		#region handlers
		void IConsumer<Phase_Layout>.Consume(Phase_Layout message) {
			var space = AxisMargin + /*AxisLineThickness + */ (Orientation == AxisOrientation.Horizontal ? MinHeight : MinWidth);
			message.Context.ClaimSpace(this, Side, space);
		}
		void IConsumer<Phase_InitializeAxes>.Consume(Phase_InitializeAxes message) {
			ResetLimits();
			var msg = new Axis_Extents(Name, Minimum, Maximum, Side, Type, Reverse);
			message.Register(msg);
		}
		void IConsumer<Phase_DataSourceOperation>.Consume(Phase_DataSourceOperation message) {
			if (message.Operation.Name != DataSourceName) return;
			if(message.Operation is DataSource_Typed dstt) {
				LabelBinding = Binding.For(dstt.ItemType, LabelMemberPath);
				if (LabelBinding == null) return;
			}
			switch (message.Operation) {
				case DataSource_Add add:
					Add(add);
					break;
				case DataSource_Remove remove:
					Remove(remove);
					break;
				case DataSource_Reset reset:
					Reset(reset);
					break;
				case DataSource_SlidingWindow sw:
					SlidingWindow(sw);
					break;
			}
		}
		void IConsumer<Phase_AxisExtents>.Consume(Phase_AxisExtents message) {
			foreach(var xx in message.Extents.Where(ax => ax.AxisName == Name)) {
				Extents(xx);
			}
			var msg = new Axis_Extents(Name, Minimum, Maximum, Side, Type, Reverse);
			message.Register(msg);
		}
		float YOffSetFor() {
			if(Side == Side.Top) return 1;
			return 0;
		}
		void IConsumer<Phase_Transforms>.Consume(Phase_Transforms message) {
			if (ItemState.Count == 0) return;
			if (double.IsNaN(Minimum) || double.IsNaN(Maximum)) return;
			var rctx = message.ContextFor(this);
			var pmatrix = ProjectionFor(rctx.Area, Reverse);
			var matx = Matrix3x2.Multiply(pmatrix.model, pmatrix.proj);
			double dx = 0, dy = 0;
			foreach (Axis_ItemState state in ItemState) {
				if (state.Element == null) continue;
				var point = new Vector2(state.Index + (Reverse ? 1 : 0), YOffSetFor());
				var dc = Vector2.Transform(point, matx);
				try {
					state.Element.Translation = new Vector3((float)(dc.X + dx), (float)(dc.Y + dy), 0);
				}
				catch (Exception) { //eat it
				}
			}
		}
		#endregion
		#region helpers
		/// <summary>
		/// Fabricate state used for entering items.
		/// </summary>
		/// <param name="items">Item source.</param>
		/// <returns>New instance.</returns>
		IEnumerable<Axis_ItemState> Entering(System.Collections.IList items) {
			for (int ix = 0; ix < items.Count; ix++) {
				var state = CreateState(ix, items[ix]);
				yield return state;
			}
		}
		/// <summary>
		/// Core part of the update cycle.
		/// </summary>
		/// <param name="items">Sequence of item operations.</param>
		void UpdateCore(IEnumerable<ItemStateOperation<Axis_ItemState>> items) {
			var itemstate = new List<ItemStateCore>();
			foreach (var item in items) {
				item.Execute(this, itemstate);
			}
			ItemState = itemstate;
		}
		FrameworkElement CreateElement(TextShim text) {
			var fe = default(FrameworkElement);
			if (LabelTemplate != null) {
				fe = LabelTemplate.LoadContent() as FrameworkElement;
			}
			else {
				fe = new TextBlock() {
					HorizontalAlignment = HorizontalAlignment.Left,
					HorizontalTextAlignment = TextAlignment.Left,
					VerticalAlignment = VerticalAlignment.Center,
				};
				ChartComponent.BindTo(text, nameof(TextShim.Text), fe, TextBlock.TextProperty);
			}
			fe.TranslationTransition = new Vector3Transition() {
				Duration = TimeSpan.FromMilliseconds(300),
				Components = Vector3TransitionComponents.X | Vector3TransitionComponents.Y
			};
			if (LabelStyle != null) {
				BindTo(this, nameof(LabelStyle), fe, FrameworkElement.StyleProperty);
			}
			fe.DataContext = text;
			return fe;
		}
		Axis_ItemState CreateState(int index, object item) {
			var istate = new Axis_ItemState(index);
			// set up label VM shim
			if(LabelBinding is SelfBinding) {
				istate.label = new ObjectShim() { CustomValue = item };
			}
			else if (LabelBinding.GetString(item, out string label)) {
				istate.label = new TextShim() { Text = label };
			}
			else {
				// fake it
				istate.label = new ObjectShim() { Text = item.ToString(), CustomValue = item };
			}
			return istate;
		}
		#endregion
	}
}
