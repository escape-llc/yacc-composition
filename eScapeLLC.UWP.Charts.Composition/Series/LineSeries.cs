using eScape.Core;
using eScape.Host;
using eScapeLLC.UWP.Charts.Composition.Events;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Numerics;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;

namespace eScapeLLC.UWP.Charts.Composition {
	public class LineSeries : CategoryValueSeries, IRequireEnterLeave,
		IDataSourceRenderSession<LineSeries.Series_RenderState>,
		IConsumer<Phase_RenderTransforms>, IConsumer<Axis_Extents>, IConsumer<DataSource_RenderStart> {
		static LogTools.Flag _trace = LogTools.Add("LineSeries", LogTools.Level.Error);
		#region inner
		public class Series_ItemState : ItemState_CategoryValue<CompositionSpriteShape> {
			public Series_ItemState(int index, double categoryOffset, double value, CompositionSpriteShape css) : base(index, categoryOffset, value, css) {
			}
		}
		class Series_RenderState : RenderState_ShapeContainer<Series_ItemState> {
			internal readonly EventBus bus;
			// appears to not work when AnyCPU is used
			internal readonly CanvasPathBuilder Builder = new CanvasPathBuilder(new CanvasDevice());
			internal Series_RenderState(List<ItemStateCore> state, EventBus bus) : base(state) {
				this.bus = bus;
			}
		}
		class Series_RenderSession : RenderSession<Series_RenderState> {
			internal Series_RenderSession(IDataSourceRenderSession<Series_RenderState> series, Series_RenderState state) : base(series, state) { }
		}
		#endregion
		#region ctor
		public LineSeries() {
			ItemState = new List<ItemStateCore>();
		}
		#endregion
		#region properties
		public double LineOffset { get; set; }
		/// <summary>
		/// How to create the elements for this series.
		/// </summary>
		public IElementFactory ElementFactory { get; set; }
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
		#region handlers
		public void Consume(DataSource_RenderStart message) {
			if (ElementFactory == null) return;
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
			else if (message.AxisName == ValueAxisName) {
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
		#region helpers
		void UpdateModelTransform() {
			if (CategoryAxis.Orientation == AxisOrientation.Horizontal) {
				Model = MatrixSupport.ModelFor(CategoryAxis.Minimum, CategoryAxis.Maximum + 1, ValueAxis.Minimum, ValueAxis.Maximum);
			}
			else {
				Model = MatrixSupport.ModelFor(ValueAxis.Minimum, ValueAxis.Maximum, CategoryAxis.Minimum, CategoryAxis.Maximum + 1);
			}
			Layer.Use(sv => {
				if(sv.Shapes.Count > 0 && sv.Shapes[0] is CompositionContainerShape ccs) {
					foreach (var shx in ccs.Shapes) shx.TransformMatrix = Model;
				}
			});
			foreach (Series_ItemState item in ItemState) {
				// apply new model transform
				if (item.Element != null) {
					item.Element.TransformMatrix = Model;
				}
			}
		}
		#endregion
		#region IRequireEnterLeave
		public void Enter(IChartEnterLeaveContext icelc) {
			EnsureAxes(icelc as IChartComponentContext);
			EnsureValuePath(icelc as IChartComponentContext);
			var icei = icelc as IChartErrorInfo;
			//if (ElementFactory == null) {
			//	icei?.Report(new ChartValidationResult(NameOrType(), $"Property '{nameof(ElementFactory)}' was not set", new[] { nameof(ElementFactory) }));
			//}
			Layer = icelc.CreateCompositionLayer();
			_trace.Verbose($"{Name} enter v:{ValueAxisName} {ValueAxis} c:{CategoryAxisName} {CategoryAxis} d:{DataSourceName}");
		}
		public void Leave(IChartEnterLeaveContext icelc) {
			_trace.Verbose($"{Name} leave");
			icelc.DeleteCompositionLayer(Layer);
			Layer = null;
		}
		#endregion
		#region IDataSourceRenderSession<Series_RenderState>
		void IDataSourceRenderSession<Series_RenderState>.Preamble(Series_RenderState state, IChartRenderContext icrc) {
			ResetLimits();
			Model = Matrix3x2.Identity;
		}
		void IDataSourceRenderSession<Series_RenderState>.Render(Series_RenderState state, int index, object item) {
			if (ValueBinding == null) return;
			// safe to conduct business
			if (ValueBinding.GetDouble(item, out double? value_val)) {
				state.ix = index;
				// short-circuit if it's NaN or NULL
				if (!value_val.HasValue || double.IsNaN(value_val.Value)) {
					return;
				}
				var (xx, yy) = MappingSupport.MapComponents(index + LineOffset, value_val.Value, CategoryAxis.Orientation, ValueAxis.Orientation);
				var pt = new Vector2((float)xx, (float)yy);
				if (index == 0) {
					state.Builder.BeginFigure(pt);
				}
				else {
					state.Builder.AddLine(pt);
				}
				var istate = new Series_ItemState(index, LineOffset, value_val.Value, null);
				_trace.Verbose($"{Name}[{index}] val:{value_val} dim:{xx:F2},{yy:F2}");
				state.Add(istate, null);
				UpdateLimits(index, value_val.Value, 0);
			}
		}
		void IDataSourceRenderSession<Series_RenderState>.RenderComplete(Series_RenderState state) {
			state.Builder.EndFigure(CanvasFigureLoop.Open);
			// broadcast series extents
			var msgcx = new Series_Extents(Name, DataSourceName, CategoryAxisName, Component1Minimum, Component1Maximum);
			var msgvx = new Series_Extents(Name, DataSourceName, ValueAxisName, Component2Minimum, Component2Maximum);
			state.bus.Consume(msgcx);
			state.bus.Consume(msgvx);
		}
		void IDataSourceRenderSession<Series_RenderState>.Postamble(Series_RenderState state) {
			var geom = CanvasGeometry.CreatePath(state.Builder);
			var path = new CompositionPath(geom);
			var ctx = new PathGeometryContext(state.compositor, state.itemstate.Count, LineOffset, double.NaN, CategoryAxis, ValueAxis, path);
			var shape = ElementFactory.CreateElement(ctx);
			shape.Comment = $"{Name}";
			state.container.Shapes.Add(shape);
			state.Builder.Dispose();
			// install elements
			Layer.Use(sv => {
				sv.Shapes.Clear();
				sv.Shapes.Add(state.container);
			});
			ItemState = state.itemstate;
		}
		#endregion
	}
}
