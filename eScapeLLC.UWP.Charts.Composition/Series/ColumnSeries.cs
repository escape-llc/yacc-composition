using eScape.Core;
using eScape.Host;
using eScapeLLC.UWP.Charts.Composition.Events;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using System.Reflection;
using System.ServiceModel.Channels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Markup;

namespace eScapeLLC.UWP.Charts.Composition {
	/// <summary>
	/// CompositionShapeContainer(proj) -> .Shapes [CompositionSpriteShape(model) ...]
	/// Container takes the P matrix, Shapes each take the (same) M matrix.
	/// </summary>
	public class ColumnSeries : CategoryValueSeries, IRequireEnterLeave, IProvideSeriesItemValues, IProvideSeriesItemLayout,
		IDataSourceRenderSession<ColumnSeries.Series_RenderState>,
		IConsumer<Phase_RenderTransforms>, IConsumer<Axis_Extents>, IConsumer<DataSource_RenderStart> {
		static LogTools.Flag _trace = LogTools.Add("ColumnSeries", LogTools.Level.Error);
		#region inner
		/// <summary>
		/// Item state.
		/// </summary>
		public class Series_ItemState : ItemState_CategoryValue<CompositionShape> {
			public Series_ItemState(int index, double categoryOffset, double value, CompositionShape css) : base(index, categoryOffset, value, css) {
			}
		}
		/// <summary>
		/// Render state.
		/// </summary>
		class Series_RenderState : RenderState_ShapeContainer<Series_ItemState> {
			internal readonly IProvideConsume bus;
			internal Series_RenderState(List<ItemStateCore> state, IProvideConsume bus) : base(state) {
				this.bus = bus;
			}
		}
		/// <summary>
		/// Shim for render session.
		/// </summary>
		class Series_RenderSession : RenderSession<Series_RenderState> {
			internal Series_RenderSession(IDataSourceRenderSession<Series_RenderState> series, Series_RenderState state) :base(series, state) { }
		}
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
				if(isi is Series_ItemState sis) {
					var invert = sis.DataValue < 0 ? -1 : 1;
					double hw = width / 2.0, hh = Math.Abs(sis.DataValue / 2.0);
					var (xx, yy) = MappingSupport.MapComponents(sis.Component1 + hw + offset.X*hw, (sis.DataValue / 2.0) + offset.Y*hh*invert, c1axis, c2axis);
					var (dx, dy) = MappingSupport.MapComponents(1, sis.DataValue > 0 ? 1 : -1, c1axis, c2axis);
					var center = new Vector2((float)xx, (float)yy);
					return (Project(center), new Point(dx, dy));
				}
				return null;
			}
		}
		#endregion
		#region ctor
		public ColumnSeries() {
			ItemState = new List<ItemStateCore>();
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
		/// How to create the elements for this series.
		/// </summary>
		public IElementFactory ElementFactory { get; set; }
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
		/// Data needed for current state.
		/// </summary>
		protected List<ItemStateCore> ItemState { get; set; }
		#endregion
		#region helpers
		void UpdateModelTransform() {
			if(CategoryAxis.Orientation == AxisOrientation.Horizontal) {
				Model = MatrixSupport.ModelFor(CategoryAxis.Minimum, CategoryAxis.Maximum + 1, ValueAxis.Minimum, ValueAxis.Maximum);
			}
			else {
				Model = MatrixSupport.ModelFor(ValueAxis.Minimum, ValueAxis.Maximum, CategoryAxis.Minimum, CategoryAxis.Maximum + 1);
			}
			foreach(Series_ItemState item in ItemState) {
				// apply new model transform
				if (item.Element != null) {
					item.Element.TransformMatrix = Model;
				}
			}
		}
		#endregion
		#region IDataSourceRenderSession<ColumnSeries_RenderState>
		void IDataSourceRenderSession<Series_RenderState>.Preamble(Series_RenderState state, IChartRenderContext icrc) {
			ResetLimits();
			Model = Matrix3x2.Identity;
		}
		void IDataSourceRenderSession<Series_RenderState>.Render(Series_RenderState state, int index, object item) {
			if (ElementFactory == null) return;
			if (ValueBinding == null) return;
			// safe to conduct business
			if (ValueBinding.GetDouble(item, out double? value_val)) {
				state.ix = index;
				// short-circuit if it's NaN or NULL
				if (!value_val.HasValue || double.IsNaN(value_val.Value)) {
					return;
				}
				var (xx, yy) = MappingSupport.MapComponents(BarWidth, Math.Abs(value_val.Value), CategoryAxis.Orientation, ValueAxis.Orientation);
				var ctx = new ColumnElementContext(state.container.Compositor, index, BarOffset, value_val.Value, xx, yy, CategoryAxis, ValueAxis);
				var element = ElementFactory.CreateElement(ctx);
				element.Comment = $"{Name}[{index}]";
				var istate = new Series_ItemState(index, BarOffset, value_val.Value, element);
				var offset = istate.OffsetForColumn(CategoryAxis.Orientation, ValueAxis.Orientation);
				_trace.Verbose($"{Name}[{index}] val:{value_val} dim:{xx:F2},{yy:F2} offset:{offset.X},{offset.Y}");
				element.Offset = offset;
				state.Add(istate, element);
				UpdateLimits(index, value_val.Value, 0);
			}
		}
		void IDataSourceRenderSession<Series_RenderState>.RenderComplete(Series_RenderState state) {
			// broadcast series extents
			var msgcx = new Series_Extents(Name, DataSourceName, CategoryAxisName, Component1Minimum, Component1Maximum);
			var msgvx = new Series_Extents(Name, DataSourceName, ValueAxisName, Component2Minimum, Component2Maximum);
			state.bus.Consume(msgcx);
			state.bus.Consume(msgvx);
		}
		void IDataSourceRenderSession<Series_RenderState>.Postamble(Series_RenderState state) {
			// install elements
			Layer.Use(sv => {
				sv.Shapes.Clear();
				sv.Shapes.Add(state.container);
			});
			ItemState = state.itemstate;
		}
		#endregion
		#region handlers
		public void Consume(DataSource_RenderStart message) {
			if (message.Name != DataSourceName) return;
			if (message.ExpectedItemType == null) return;
			ValueBinding = Binding.For(message.ExpectedItemType, ValueMemberName);
			if (!string.IsNullOrEmpty(LabelMemberName)) {
				LabelBinding = Binding.For(message.ExpectedItemType, LabelMemberName);
			}
			else {
				LabelBinding = ValueBinding;
			}
			if (ValueBinding == null) return;
			message.Register(new Series_RenderSession(this, new Series_RenderState(new List<ItemStateCore>(), message.Bus)));
		}
		/// <summary>
		/// Axis extents participate in the Model transform.
		/// </summary>
		/// <param name="message"></param>
		public void Consume(Axis_Extents message) {
			if (message.AxisName == CategoryAxisName) {
				CategoryAxis = message;
				if (double.IsNaN(CategoryAxis.Minimum) || double.IsNaN(CategoryAxis.Maximum)) return;
				UpdateModelTransform();
			}
			else if(message.AxisName == ValueAxisName) {
				ValueAxis = message;
				if (double.IsNaN(ValueAxis.Minimum) || double.IsNaN(ValueAxis.Maximum)) return;
				UpdateModelTransform();
			}
		}
		/// <summary>
		/// Render area participates in the Projection transform.
		/// </summary>
		/// <param name="message"></param>
		public void Consume(Phase_RenderTransforms message) {
			if (CategoryAxis == null || ValueAxis == null) return;
			if (ItemState.Count == 0) return;
			var rctx = message.ContextFor(this);
			var xaxis = CategoryAxis.Orientation == AxisOrientation.Horizontal ? CategoryAxis.Reversed : ValueAxis.Reversed;
			var yaxis = CategoryAxis.Orientation == AxisOrientation.Vertical ? CategoryAxis.Reversed : ValueAxis.Reversed;
			var q = MatrixSupport.QuadrantFor(!xaxis, !yaxis);
			var proj = MatrixSupport.ProjectForQuadrant(q, rctx.SeriesArea);
			Layer.Use(sv => {
				foreach (var shx in sv.Shapes) {
					shx.TransformMatrix = proj;
				}
			});
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
			Layer = icelc.CreateCompositionLayer();
			_trace.Verbose($"{Name} enter v:{ValueAxisName} {ValueAxis} c:{CategoryAxisName} {CategoryAxis} d:{DataSourceName}");
		}
		public void Leave(IChartEnterLeaveContext icelc) {
			_trace.Verbose($"{Name} leave");
			icelc.DeleteCompositionLayer(Layer);
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
