using eScape.Core;
using eScape.Host;
using eScapeLLC.UWP.Charts.Composition.Events;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Windows.UI.Composition;
using Windows.UI.Xaml;

namespace eScapeLLC.UWP.Charts.Composition {
	public class LineSeries : CategoryValueSeries,
		IRequireEnterLeave, IProvideSeriesItemValues,
		IConsumer<Phase_RenderTransforms> {
		static LogTools.Flag _trace = LogTools.Add("LineSeries", LogTools.Level.Error);
		#region inner
		public class Series_ItemState : ItemState_CategoryValue<CompositionSpriteShape> {
			public Series_ItemState(int index, double categoryOffset, double value, CompositionSpriteShape css) : base(index, categoryOffset, value, css) {
			}
		}
		class Series_RenderState : RenderState_ShapeContainer<Series_ItemState> {
			internal readonly IProvideConsume bus;
			// appears to not work when AnyCPU is used
			internal readonly CanvasPathBuilder Builder = new CanvasPathBuilder(new CanvasDevice());
			internal Series_RenderState(List<ItemStateCore> state, IProvideConsume bus) : base(state) {
				this.bus = bus;
			}
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
		/// <summary>
		/// Holds all the shapes for this series.
		/// </summary>
		protected CompositionContainerShape Container { get; set; }
		#endregion
		#region data source operations
		protected override void Reset(DataSource_Reset dsr) {
			var itemstate = new List<ItemStateCore>();
			var shape = GetShape(dsr.Items, itemstate);
			// remove update install elements
			ResetLimits();
			Model = Matrix3x2.Identity;
			for (int ix = 0; ix < itemstate.Count; ix++) {
				Series_ItemState state = itemstate[ix] as Series_ItemState;
				if (state == null) continue;
				UpdateLimits(ix, state.DataValue, 0);
				// no geometry updates
			}
			UpdateLimits(itemstate.Count);
			Container.Shapes.Clear();
			Container.Shapes.Add(shape);
			ItemState = itemstate;
		}
		#endregion
		#region handlers
		/// <summary>
		/// Render area participates in the Projection transform.
		/// </summary>
		/// <param name="message"></param>
		void IConsumer<Phase_RenderTransforms>.Consume(Phase_RenderTransforms message) {
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
		CompositionShape GetShape(IList items, List<ItemStateCore> itemstate) {
			// appears to not work when AnyCPU is used
			CanvasPathBuilder builder = new CanvasPathBuilder(new CanvasDevice());
			using (builder) {
				Series_ItemState prev = null;
				for (int ix = 0; ix < items.Count; ix++) {
					var state = CreateState(builder, ix, prev == null, items[ix]);
					itemstate.Add(state);
					prev = state;
				}
				builder.EndFigure(CanvasFigureLoop.Open);
				var geom = CanvasGeometry.CreatePath(builder);
				var path = new CompositionPath(geom);
				var ctx = new PathGeometryContext(Container.Compositor, itemstate.Count, LineOffset, double.NaN, CategoryAxis, ValueAxis, path);
				var shape = ElementFactory.CreateElement(ctx);
				shape.Comment = $"{Name}";
				return shape;
			}
		}
		Series_ItemState CreateState(CanvasPathBuilder cpb, int index, bool beginf, object item) {
			if (ValueBinding.GetDouble(item, out double? value_val)) {
				// short-circuit if it's NaN or NULL
				if (!value_val.HasValue || double.IsNaN(value_val.Value)) {
					return null;
				}
				var (xx, yy) = MappingSupport.MapComponents(index + LineOffset, value_val.Value, CategoryAxis.Orientation, ValueAxis.Orientation);
				var pt = new Vector2((float)xx, (float)yy);
				if (beginf) {
					cpb.BeginFigure(pt);
				}
				else {
					cpb.AddLine(pt);
				}
				var istate = new Series_ItemState(index, LineOffset, value_val.Value, null);
				_trace.Verbose($"{Name}[{index}] val:{value_val} dim:{xx:F2},{yy:F2}");
				return istate;
			}
			return null;
		}
		protected override void UpdateModelTransform() {
			if (CategoryAxis.Orientation == AxisOrientation.Horizontal) {
				Model = MatrixSupport.ModelFor(CategoryAxis.Minimum, CategoryAxis.Maximum, ValueAxis.Minimum, ValueAxis.Maximum);
			}
			else {
				Model = MatrixSupport.ModelFor(ValueAxis.Minimum, ValueAxis.Maximum, CategoryAxis.Minimum, CategoryAxis.Maximum);
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
			Compositor compositor = Window.Current.Compositor;
			Container = compositor.CreateContainerShape();
			Container.Comment = $"container_{Name}";
			Layer = icelc.CreateCompositionLayer(Container);
			_trace.Verbose($"{Name} enter v:{ValueAxisName} {ValueAxis} c:{CategoryAxisName} {CategoryAxis} d:{DataSourceName}");
		}
		public void Leave(IChartEnterLeaveContext icelc) {
			_trace.Verbose($"{Name} leave");
			Container = null;
			icelc.DeleteCompositionLayer(Layer);
			Layer = null;
		}
		#endregion
	}
}
