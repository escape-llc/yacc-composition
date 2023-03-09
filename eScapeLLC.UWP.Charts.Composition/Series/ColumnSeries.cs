using eScape.Core;
using eScape.Host;
using eScapeLLC.UWP.Charts.Composition.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;

namespace eScapeLLC.UWP.Charts.Composition {
	/// <summary>
	/// Item state.
	/// <para/>
	/// NOTE THIS CAN NO LONGER BE an Inner Class or XAML will not load it!
	/// </summary>
	public class ColumnSeries_ItemState : ItemState_CategoryValue<CompositionShape> {
		public ColumnSeries_ItemState(int index, double categoryOffset, double value) : base(index, categoryOffset, value) { }
	}
	/// <summary>
	/// CompositionShapeContainer(proj) -> .Shapes [CompositionSpriteShape(model) ...]
	/// Container takes the P matrix, Shapes each take the (same) M matrix.
	/// </summary>
	public class ColumnSeries : CategoryValue_ShapePerItem<ColumnSeries_ItemState>,
		IRequireEnterLeave, IProvideSeriesItemValues, IProvideSeriesItemLayout,
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
		/// <summary>
		/// Call <see cref="IAnimationController.InitTransform(Matrix3x2)"/> exactly once.
		/// </summary>
		bool didInitTransform = false;
		#endregion
		#region helpers
		#endregion
		#region extension points
		protected override Visual CreateLegendVisual(Compositor cx) {
			var vis = cx.CreateShapeVisual();
			var rectangle = cx.CreateRectangleGeometry();
			rectangle.Size = LegendSupport.DesiredSize;
			var sprite = cx.CreateSpriteShape(rectangle);
			if (ElementFactory != null) {
				ElementFactory.ApplyStyle(sprite);
			}
			else {
				sprite.FillBrush = cx.CreateColorBrush(Colors.BlueViolet);
			}
			vis.Shapes.Add(sprite);
			return vis;
		}
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
		/// <param name="cx">Use to create composition objects.</param>
		/// <param name="state">Item state.</param>
		/// <returns>New shape.</returns>
		protected override CompositionShape CreateShape(Compositor cx, ColumnSeries_ItemState state) {
			var (xx, yy) = MappingSupport.MapComponents(BarWidth, CategoryAxis.Orientation, Math.Abs(state.DataValue), ValueAxis.Orientation);
			var ctx = new ColumnElementContext(cx, state.Index, BarOffset, state.DataValue, xx, yy, CategoryAxis, ValueAxis);
			var element = ElementFactory.CreateElement(ctx);
			element.Comment = $"{Name}[{state.Index}]";
			element.TransformMatrix = Model;
			_trace.Verbose($"{Name}[{state.Index}] create-shape val:{state.DataValue} dim:{xx:F2},{yy:F2}");
			return element;
		}
		/// <summary>
		/// Create the context for <see cref="IAnimationController"/>.
		/// </summary>
		/// <param name="item">Source item.</param>
		/// <param name="it">Transition info.</param>
		/// <returns>New instance.</returns>
		protected override IElementFactoryContext CreateAnimateContext(ColumnSeries_ItemState item, ItemTransition it) => new CategoryValueContext(Container.Compositor, item, CategoryAxis, ValueAxis, it);
		#endregion
		#region render pipeline event extensions
		/// <summary>
		/// <inheritdoc/>
		/// </summary>
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
		/// <summary>
		/// <inheritdoc/>
		/// </summary>
		protected override void UpdateModelTransform() {
			Matrix3x2 model = CategoryAxis.Orientation == AxisOrientation.Horizontal
			? MatrixSupport.ModelFor(CategoryAxis.Minimum, CategoryAxis.Maximum, ValueAxis.Minimum, ValueAxis.Maximum)
			: MatrixSupport.ModelFor(ValueAxis.Minimum, ValueAxis.Maximum, CategoryAxis.Minimum, CategoryAxis.Maximum);
			if (model == Model) return;
			if(!didInitTransform) {
				Animate?.InitTransform(model);
				didInitTransform = true;
			}
			Model = model;
			if(AnimationFactory != null) {
				var ctx = new DefaultContext(Container.Compositor, CategoryAxis, ValueAxis);
				Animate.Transform(ctx, Model);
			}
			else {
				foreach (ColumnSeries_ItemState item in ItemState.Cast<ColumnSeries_ItemState>().Where(xx => xx != null && xx.Element != null)) {
					// apply new model transform
					item.Element.TransformMatrix = Model;
				}
			}
		}
		/// <summary>
		/// <inheritdoc/>
		/// </summary>
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
			Animate = AnimationFactory?.CreateAnimationController(compositor);
			_trace.Verbose($"{Name} enter v:{ValueAxisName} {ValueAxis} c:{CategoryAxisName} {CategoryAxis} d:{DataSourceName}");
		}
		public void Leave(IChartEnterLeaveContext icelc) {
			_trace.Verbose($"{Name} leave");
			Animate?.Dispose();
			Animate = null;
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
