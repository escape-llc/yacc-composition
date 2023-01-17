using eScape.Core;
using eScape.Host;
using eScapeLLC.UWP.Composition.Charts.Events;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using System.Reflection;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Markup;

namespace eScapeLLC.UWP.Composition.Charts {
	/// <summary>
	/// CompositionShapeContainer(proj) -> .Shapes [CompositionSpriteShape(model) ...]
	/// Container takes the P matrix, Shapes each take the (same) M matrix.
	/// </summary>
	public class ColumnSeries : ChartComponent, IRequireEnterLeave, IDataSourceRenderSession<ColumnSeries.ColumnSeries_RenderState>,
		IConsumer<Phase_RenderTransforms>, IConsumer<Axis_Extents>, IConsumer<DataSource_RenderStart> {
		static LogTools.Flag _trace = LogTools.Add("ColumnSeries", LogTools.Level.Error);
		#region inner
		/// <summary>
		/// Item state.
		/// </summary>
		public class ColumnSeries_ItemState : ItemState_CategoryValue<CompositionSpriteShape> {
			public ColumnSeries_ItemState(int index, double categoryOffset, double value, CompositionSpriteShape css) : base(index, categoryOffset, value, css) {
			}
		}
		/// <summary>
		/// Render state.
		/// </summary>
		class ColumnSeries_RenderState : RenderState_ShapeContainer<ColumnSeries_ItemState> {
			internal readonly EventBus bus;
			internal ColumnSeries_RenderState(List<ItemStateCore> state, EventBus bus) : base(state) {
				this.bus = bus;
			}
		}
		/// <summary>
		/// Shim for render session.
		/// </summary>
		class ColumnSeries_RenderSession : RenderSession<ColumnSeries_RenderState> {
			internal ColumnSeries_RenderSession(IDataSourceRenderSession<ColumnSeries_RenderState> series, ColumnSeries_RenderState state) :base(series, state) { }
		}
		#endregion
		#region ctor
		public ColumnSeries() {
			ItemState = new List<ItemStateCore>();
		}
		#endregion
		#region properties
		/// <summary>
		/// MUST match the name of a data source.
		/// </summary>
		public string DataSourceName { get; set; }
		/// <summary>
		/// MUST match the name of an axis.
		/// </summary>
		public string CategoryAxisName { get; set; }
		/// <summary>
		/// MUST match the name of an axis.
		/// </summary>
		public string ValueAxisName { get; set; }
		/// <summary>
		/// MUST match the name of a DAO member.
		/// </summary>
		public string ValueMemberName { get; set; }
		/// <summary>
		/// MUST match the name of a DAO member.  MAY be NULL.
		/// </summary>
		public string LabelMemberName { get; set; }
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
		/// The minimum value seen.
		/// </summary>
		public double Component2Minimum { get; protected set; } = double.NaN;
		/// <summary>
		/// The maximum value seen.
		/// </summary>
		public double Component2Maximum { get; protected set; } = double.NaN;
		/// <summary>
		/// The minimum category (value) seen.
		/// </summary>
		public double Component1Minimum { get; protected set; } = double.NaN;
		/// <summary>
		/// The maximum category (value) seen.
		/// </summary>
		public double Component1Maximum { get; protected set; } = double.NaN;
		/// <summary>
		/// Range of the values or <see cref="double.NaN"/> if <see cref="UpdateLimits(double, double)"/>or <see cref="UpdateLimits(double, double[])"/> was never called.
		/// </summary>
		public double Component2Range { get { return double.IsNaN(Component2Minimum) || double.IsNaN(Component2Maximum) ? double.NaN : Component2Maximum - Component2Minimum; } }
		public double Component1Range { get { return double.IsNaN(Component1Minimum) || double.IsNaN(Component1Maximum) ? double.NaN : Component1Maximum - Component1Minimum; } }
		/// <summary>
		/// How to create the elements for this series.
		/// </summary>
		public IColumnElementFactory ElementFactory { get; set; }
		#endregion
		#region internal
		protected Axis_Extents CategoryAxis { get; set; }
		protected Axis_Extents ValueAxis { get; set; }
		protected Binding ValueBinding { get; set; }
		/// <summary>
		/// Obtain alternate label if not NULL.
		/// </summary>
		protected Binding LabelBinding { get; set; }
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
		/// <summary>
		/// Update value and category limits.
		/// If a value is <see cref="double.NaN"/>, it is effectively ignored because NaN is NOT GT/LT ANY number, even itself.
		/// </summary>
		/// <param name="value_cat">Category. MAY be NaN.</param>
		/// <param name="value_vals">Values.  MAY contain NaN.</param>
		protected void UpdateLimits(double value_cat, IEnumerable<double> value_vals) {
			if (double.IsNaN(Component1Minimum) || value_cat < Component1Minimum) { Component1Minimum = value_cat; }
			if (double.IsNaN(Component1Maximum) || value_cat > Component1Maximum) { Component1Maximum = value_cat; }
			foreach (var vy in value_vals) {
				if (double.IsNaN(Component2Minimum) || vy < Component2Minimum) { Component2Minimum = vy; }
				if (double.IsNaN(Component2Maximum) || vy > Component2Maximum) { Component2Maximum = vy; }
			}
		}
		protected void UpdateLimits(double value_cat, params double[] value_vals) {
			UpdateLimits(value_cat, (IEnumerable<double>)value_vals);
		}
		/// <summary>
		/// Reset the value and category limits to <see cref="double.NaN"/>.
		/// Sets <see cref="ChartComponent.Dirty"/> = true.
		/// </summary>
		protected void ResetLimits() {
			Component2Minimum = double.NaN; Component2Maximum = double.NaN;
			Component1Minimum = double.NaN; Component1Maximum = double.NaN;
			Dirty = true;
		}
		void UpdateModelTransform() {
			if(CategoryAxis.Orientation == AxisOrientation.Horizontal) {
				Model = MatrixSupport.ModelFor(CategoryAxis.Minimum, CategoryAxis.Maximum + 1, ValueAxis.Minimum, ValueAxis.Maximum);
			}
			else {
				Model = MatrixSupport.ModelFor(ValueAxis.Minimum, ValueAxis.Maximum, CategoryAxis.Minimum, CategoryAxis.Maximum + 1);
			}
			foreach(ColumnSeries_ItemState item in ItemState) {
				// apply new model transform
				if (item.Element != null) {
					item.Element.TransformMatrix = Model;
				}
			}
		}
		void EnsureAxes(IChartComponentContext iccc) {
			IChartErrorInfo icei = iccc as IChartErrorInfo;
			if (!string.IsNullOrEmpty(ValueAxisName)) {
				var axis = iccc.Find(ValueAxisName) as IChartAxis;
				if (axis == null) {
					icei?.Report(new ChartValidationResult(NameOrType(), $"Value axis '{ValueAxisName}' was not found", new[] { nameof(ValueAxisName) }));
				}
				else {
					if(axis.Type != AxisType.Value) {
						icei?.Report(new ChartValidationResult(NameOrType(), $"Value axis '{ValueAxisName}' Type {axis.Type} is not Value", new[] { nameof(ValueAxisName) }));
					}
				}
			}
			else {
				icei?.Report(new ChartValidationResult(NameOrType(), $"Property '{nameof(ValueAxisName)}' was not set", new[] { nameof(ValueAxisName) }));
			}
			if (!string.IsNullOrEmpty(CategoryAxisName)) {
				var axis = iccc.Find(CategoryAxisName) as IChartAxis;
				if (axis == null) {
					icei?.Report(new ChartValidationResult(NameOrType(), $"Category axis '{CategoryAxisName}' was not found", new[] { nameof(CategoryAxisName) }));
				}
				else {
					if (axis.Type != AxisType.Category) {
						icei?.Report(new ChartValidationResult(NameOrType(), $"Category axis '{CategoryAxisName}' Type {axis.Type} is not Category", new[] { nameof(CategoryAxisName) }));
					}
				}
			}
			else {
				icei?.Report(new ChartValidationResult(NameOrType(), $"Property '{nameof(CategoryAxisName)}' was not set", new[] { nameof(CategoryAxisName) }));
			}
		}
		void EnsureValuePath(IChartComponentContext iccc) {
			IChartErrorInfo icei = iccc as IChartErrorInfo;
			if(string.IsNullOrEmpty(ValueMemberName)) {
				icei?.Report(new ChartValidationResult(NameOrType(), $"Property '{nameof(ValueMemberName)}' was not set", new[] { nameof(ValueMemberName) }));
			}
		}
		#endregion
		#region IDataSourceRenderSession<ColumnSeries_RenderState>
		void IDataSourceRenderSession<ColumnSeries_RenderState>.Preamble(ColumnSeries_RenderState state, IChartRenderContext icrc) {
			ResetLimits();
			Model = Matrix3x2.Identity;
		}
		void IDataSourceRenderSession<ColumnSeries_RenderState>.Render(ColumnSeries_RenderState state, int index, object item) {
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
				var element = ElementFactory.CreateElement(state.container.Compositor, xx, yy, CategoryAxis.Orientation, ValueAxis.Orientation);
				element.Comment = $"{Name}[{index}]";
				var istate = new ColumnSeries_ItemState(index, BarOffset, value_val.Value, element);
				var offset = istate.OffsetForColumn(CategoryAxis.Orientation, ValueAxis.Orientation);
				_trace.Verbose($"{Name}[{index}] val:{value_val} dim:{xx:F2},{yy:F2} offset:{offset.X},{offset.Y}");
				element.Offset = offset;
				state.Add(istate, element);
				UpdateLimits(index, value_val.Value, 0);
			}
		}
		void IDataSourceRenderSession<ColumnSeries_RenderState>.RenderComplete(ColumnSeries_RenderState state) {
			// broadcast series extents
			var msgcx = new Series_Extents(Name, DataSourceName, CategoryAxisName, Component1Minimum, Component1Maximum);
			var msgvx = new Series_Extents(Name, DataSourceName, ValueAxisName, Component2Minimum, Component2Maximum);
			state.bus.Consume(msgcx);
			state.bus.Consume(msgvx);
		}
		void IDataSourceRenderSession<ColumnSeries_RenderState>.Postamble(ColumnSeries_RenderState state) {
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
			message.Register(new ColumnSeries_RenderSession(this, new ColumnSeries_RenderState(new List<ItemStateCore>(), message.Bus)));
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
	}
}
