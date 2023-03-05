using eScape.Core;
using eScape.Host;
using eScapeLLC.UWP.Charts.Composition.Events;
using eScapeLLC.UWP.Charts.Composition.Factory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;

namespace eScapeLLC.UWP.Charts.Composition {
	/// <summary>
	/// Item state.
	/// <para/>
	/// NOTE THIS CAN NO LONGER BE an Inner Class or XAML will not load it!
	/// </summary>
	public class ColumnSeries_ItemState : ItemState_CategoryValue<CompositionShape> {
		public ColumnSeries_ItemState(int index, double categoryOffset, double value) : base(index, categoryOffset, value) {
		}
		public void Reindex(int idx) { Index = idx; }
	}
	/// <summary>
	/// CompositionShapeContainer(proj) -> .Shapes [CompositionSpriteShape(model) ...]
	/// Container takes the P matrix, Shapes each take the (same) M matrix.
	/// </summary>
	public class ColumnSeries : CategoryValueSeries<ColumnSeries_ItemState>,
		IRequireEnterLeave, IProvideSeriesItemValues, IProvideSeriesItemLayout, IOperationController<ColumnSeries_ItemState>,
		IConsumer<Phase_Transforms> {
		static readonly LogTools.Flag _trace = LogTools.Add("ColumnSeries", LogTools.Level.Error);
		#region inner
		/// <summary>
		/// Label placement session.
		/// </summary>
		class Series_LayoutSession : LayoutSession {
			readonly double width;
			readonly AxisOrientation c1axis;
			readonly AxisOrientation c2axis;
			internal Series_LayoutSession(Matrix3x2 model, Matrix3x2 projection, double width, AxisOrientation c1axis, AxisOrientation c2axis) : base(model, projection) {
				this.width = width;
				this.c1axis = c1axis;
				this.c2axis = c2axis;
			}
			public override (Vector2 center, Point direction)? Layout(ISeriesItem isi, Point offset) {
				if(isi is ColumnSeries_ItemState sis) {
					var invert = sis.DataValue < 0 ? -1 : 1;
					double hw = width / 2.0, hh = Math.Abs(sis.DataValue / 2.0);
					var center = MappingSupport.ToVector(sis.Component1 + hw + offset.X * hw, c1axis, (sis.DataValue / 2.0) + offset.Y * hh * invert, c2axis);
					var (dx, dy) = MappingSupport.MapComponents(1, c1axis, sis.DataValue > 0 ? 1 : -1, c2axis);
					return (Project(center), new Point(dx, dy));
				}
				return null;
			}
		}
		#endregion
		#region ctor
		public ColumnSeries() {
		}
		#endregion
		#region properties
		/// <summary>
		/// Fractional offset into the "cell" of the category axis.
		/// BarOffset + BarWidth &lt;= 1.0
		/// </summary>
		public double BarOffset { get; set; } = 0.25;
		/// <summary>
		/// Fractional width in the "cell" of the category axis.
		/// BarOffset + BarWidth &lt;= 1.0
		/// </summary>
		public double BarWidth { get; set; } = 0.5;
		/// <summary>
		/// Return current state as read-only.
		/// </summary>
		public IEnumerable<ISeriesItem> SeriesItemValues => ItemState.AsReadOnly();
		#endregion
		#region internal
		protected IChartCompositionLayer Layer { get; set; }
		/// <summary>
		/// Maintained from axis extents.
		/// </summary>
		protected Matrix3x2 Model { get; set; }
		/// <summary>
		/// Holds all the shapes for this series.
		/// </summary>
		protected CompositionContainerShape Container { get; set; }
		#endregion
		#region helpers
		void UpdateOffset(ColumnSeries_ItemState item) {
			if (item.Element == null) return;
			if (AnimationFactory != null) {
				_trace.Verbose($"{Name}[{item.Index}] update-offset val:{item.DataValue} from:{item.Element.Offset.X},{item.Element.Offset.Y}");
				var ctx = new CategoryValueContext(Container.Compositor, item, CategoryAxis, ValueAxis, ItemTransition.None);
				AnimationFactory.Offset(ctx, item.Element);
			}
			else {
				var offset = item.OffsetForColumn(CategoryAxis.Orientation, ValueAxis.Orientation);
				_trace.Verbose($"{Name}[{item.Index}] update-offset val:{item.DataValue} from:{item.Element.Offset.X},{item.Element.Offset.Y} to:{offset.X},{offset.Y}");
				item.Element.Offset = offset;
			}
		}
		#endregion
		#region IListController<>
		void IOperationController<ColumnSeries_ItemState>.LiveItem(int index, ItemTransition it, ColumnSeries_ItemState state) {
			_trace.Verbose($"{Name}.Live index:{index} it:{it} st[{state.Index}]:{state.DataValue} el:{state.Element}");
			state.Reindex(index);
			bool elementSelected = IsSelected(state);
			if (elementSelected && state.Element == null) {
				state.SetElement(CreateShape(Container.Compositor, index, state.DataValue));
				Entering(state, it);
				UpdateStyle(state);
				UpdateOffset(state);
			}
			else if (!elementSelected && state.Element != null) {
				Exiting(state, it);
			}
			else {
				UpdateStyle(state);
				UpdateOffset(state);
			}
		}
		void IOperationController<ColumnSeries_ItemState>.EnteringItem(int index, ItemTransition it, ColumnSeries_ItemState state) {
			_trace.Verbose($"{Name}.Entering index:{index} it:{it} st[{state.Index}]:{state.DataValue} el:{state.Element}");
			state.Reindex(index);
			bool elementSelected = IsSelected(state);
			if (elementSelected) {
				state.SetElement(CreateShape(Container.Compositor, index, state.DataValue));
				Entering(state, it);
				UpdateStyle(state);
				UpdateOffset(state);
			}
		}
		void IOperationController<ColumnSeries_ItemState>.ExitingItem(int index, ItemTransition it, ColumnSeries_ItemState state) {
			_trace.Verbose($"{Name}.Exiting index:{index} it:{it} st[{state.Index}]:{state.DataValue} el:{state.Element}");
			if (state.Element != null) {
				Exiting(state, it);
			}
		}
		#endregion
		#region extension points
		/// <summary>
		/// Create some state or NULL.
		/// </summary>
		/// <param name="index"></param>
		/// <param name="item"></param>
		/// <returns>NULL or new instance.</returns>
		protected override ColumnSeries_ItemState CreateState(int index, object item) {
			if (ValueBinding.GetDouble(item, out double? value_val)) {
				// short-circuit if it's NaN or NULL
				if (!value_val.HasValue || double.IsNaN(value_val.Value)) {
					return null;
				}
				var istate = new ColumnSeries_ItemState(index, BarOffset, value_val.Value);
				_trace.Verbose($"{Name}[{index}] create-state val:{istate.DataValue}");
				return istate;
			}
			return null;
		}
		/// <summary>
		/// Create shape with <see cref="ElementFactory"/>.
		/// </summary>
		/// <param name="cx"></param>
		/// <param name="index"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		protected virtual CompositionShape CreateShape(Compositor cx, int index, double value) {
			var (xx, yy) = MappingSupport.MapComponents(BarWidth, CategoryAxis.Orientation, Math.Abs(value), ValueAxis.Orientation);
			var ctx = new ColumnElementContext(cx, index, BarOffset, value, xx, yy, CategoryAxis, ValueAxis);
			var element = ElementFactory.CreateElement(ctx);
			element.Comment = $"{Name}[{index}]";
			element.TransformMatrix = Model;
			_trace.Verbose($"{Name}[{index}] create-shape val:{value} dim:{xx:F2},{yy:F2}");
			return element;
		}
		/// <summary>
		/// Item is entering the chart.
		/// </summary>
		/// <param name="item"></param>
		protected virtual void Entering(ColumnSeries_ItemState item, ItemTransition it) {
			if (item == null || item.Element == null) return;
			if (AnimationFactory != null) {
				var ctx = new CategoryValueContext(Container.Compositor, item, CategoryAxis, ValueAxis, it);
				AnimationFactory.Enter(ctx, item.Element, Container.Shapes);
			}
			else {
				if (it == ItemTransition.Head) {
					Container.Shapes.Insert(0, item.Element);
				}
				else {
					Container.Shapes.Add(item.Element);
				}
			}
		}
		/// <summary>
		/// Item is exiting the chart.
		/// </summary>
		/// <param name="item"></param>
		protected virtual void Exiting(ColumnSeries_ItemState item, ItemTransition it) {
			if (item == null || item.Element == null) return;
			if (AnimationFactory != null) {
				var ctx = new CategoryValueContext(Container.Compositor, item, CategoryAxis, ValueAxis, it);
				AnimationFactory.Exit(ctx, item.Element, Container.Shapes, co => {
					item.ResetElement();
				});
			}
			else {
				Container.Shapes.Remove(item.Element);
				item.ResetElement();
			}
		}
		protected virtual void UpdateStyle(ColumnSeries_ItemState item) {
		}
		protected virtual bool IsSelected(ColumnSeries_ItemState item) {
			return true;
		}
		#endregion
		#region data operation extensions
		protected override void ComponentExtents() {
			if (Pending == null) return;
			_trace.Verbose($"{Name} component-extents");
			ResetLimits();
			Model = Matrix3x2.Identity;
			int index = 0;
			foreach (var op in Pending.Where(xx => xx is ItemsWithOffset<ColumnSeries_ItemState>)) {
				foreach (var item in (op as ItemsWithOffset<ColumnSeries_ItemState>).Items) {
					UpdateLimits(index, item.DataValue, 0);
					index++;
				}
			}
			UpdateLimits(index);
		}
		bool did = false;
		protected override void UpdateModelTransform() {
			Matrix3x2 model = CategoryAxis.Orientation == AxisOrientation.Horizontal
			? MatrixSupport.ModelFor(CategoryAxis.Minimum, CategoryAxis.Maximum, ValueAxis.Minimum, ValueAxis.Maximum)
			: MatrixSupport.ModelFor(ValueAxis.Minimum, ValueAxis.Maximum, CategoryAxis.Minimum, CategoryAxis.Maximum);
			if (model == Model) return;
			if(!did) {
				AnimationFactory?.InitTransform(model);
				did = true;
			}
			Model = model;
			if(AnimationFactory != null) {
				var ctx = new DefaultContext(Container.Compositor, CategoryAxis, ValueAxis);
				AnimationFactory.Transform(ctx, Model);
			}
			else {
				foreach (ColumnSeries_ItemState item in ItemState.Cast<ColumnSeries_ItemState>().Where(xx => xx != null && xx.Element != null)) {
					// apply new model transform
					item.Element.TransformMatrix = Model;
				}
			}
		}
		protected override void ModelComplete() {
			if (Pending == null) return;
			if (Container == null) return;
			_trace.Verbose($"{Name} model-complete");
			try {
				UpdateCore(this, Pending);
			}
			finally {
				Pending = null;
			}
		}
		#endregion
		#region handlers
		/// <summary>
		/// Render area participates in the Projection transform.
		/// </summary>
		/// <param name="message"></param>
		void IConsumer<Phase_Transforms>.Consume(Phase_Transforms message) {
			if (CategoryAxis == null || ValueAxis == null) return;
			if (ItemState.Count == 0) return;
			if (Container == null) return;
			var rctx = message.ContextFor(this);
			var xaxis = CategoryAxis.Orientation == AxisOrientation.Horizontal ? CategoryAxis.Reversed : ValueAxis.Reversed;
			var yaxis = CategoryAxis.Orientation == AxisOrientation.Vertical ? CategoryAxis.Reversed : ValueAxis.Reversed;
			var q = MatrixSupport.QuadrantFor(!xaxis, !yaxis);
			var proj = MatrixSupport.ProjectForQuadrant(q, rctx.SeriesArea);
			Container.TransformMatrix = proj;
		}
		#endregion
		#region IRequireEnterLeave
		public void Enter(IChartEnterLeaveContext icelc) {
			EnsureAxes(icelc as IChartComponentContext);
			EnsureValuePath(icelc as IChartComponentContext);
			var icei = icelc as IChartErrorInfo;
			if (ElementFactory == null) {
				icei?.Report(new ChartValidationResult(NameOrType(), $"Property '{nameof(ElementFactory)}' was not set", new[] { nameof(ElementFactory) }));
			}
			Compositor compositor = Window.Current.Compositor;
			Container = compositor.CreateContainerShape();
			Container.Comment = $"container_{Name}";
			Layer = icelc.CreateLayer(Container);
			AnimationFactory?.Prepare(compositor);
			_trace.Verbose($"{Name} enter v:{ValueAxisName} {ValueAxis} c:{CategoryAxisName} {CategoryAxis} d:{DataSourceName}");
		}
		public void Leave(IChartEnterLeaveContext icelc) {
			_trace.Verbose($"{Name} leave");
			AnimationFactory?.Unprepare(Window.Current.Compositor);
			Container = null;
			icelc.DeleteLayer(Layer);
			Layer = null;
		}
		#endregion
		#region IProvideSeriesItemLayout
		public ILayoutSession Create(Rect area) {
			var xaxis = CategoryAxis.Orientation == AxisOrientation.Horizontal ? CategoryAxis.Reversed : ValueAxis.Reversed;
			var yaxis = CategoryAxis.Orientation == AxisOrientation.Vertical ? CategoryAxis.Reversed : ValueAxis.Reversed;
			var q = MatrixSupport.QuadrantFor(!xaxis, !yaxis);
			var proj = MatrixSupport.ProjectForQuadrant(q, area);
			return new Series_LayoutSession(Model, proj, BarWidth, CategoryAxis.Orientation, ValueAxis.Orientation);
		}
		#endregion
	}
}
