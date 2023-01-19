using eScape.Host;
using eScapeLLC.UWP.Charts.Composition.Events;
using System.Collections.Generic;
using System.Numerics;

namespace eScapeLLC.UWP.Charts.Composition {
	public abstract class CategoryValueSeries : ChartComponent, IConsumer<Component_RenderExtents> {
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
		#endregion
		#region internal
		protected Axis_Extents CategoryAxis { get; set; }
		protected Axis_Extents ValueAxis { get; set; }
		protected Binding ValueBinding { get; set; }
		/// <summary>
		/// Obtain alternate label if not NULL.
		/// </summary>
		protected Binding LabelBinding { get; set; }
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
		protected void EnsureAxes(IChartComponentContext iccc) {
			IChartErrorInfo icei = iccc as IChartErrorInfo;
			if (!string.IsNullOrEmpty(ValueAxisName)) {
				var axis = iccc.Find(ValueAxisName) as IChartAxis;
				if (axis == null) {
					icei?.Report(new ChartValidationResult(NameOrType(), $"Value axis '{ValueAxisName}' was not found", new[] { nameof(ValueAxisName) }));
				}
				else {
					if (axis.Type != AxisType.Value) {
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
		protected void EnsureValuePath(IChartComponentContext iccc) {
			IChartErrorInfo icei = iccc as IChartErrorInfo;
			if (string.IsNullOrEmpty(ValueMemberName)) {
				icei?.Report(new ChartValidationResult(NameOrType(), $"Property '{nameof(ValueMemberName)}' was not set", new[] { nameof(ValueMemberName) }));
			}
		}
		#endregion
		#region handlers
		/// <summary>
		/// Respond with current series extents so axes can update.
		/// </summary>
		/// <param name="message"></param>
		public void Consume(Component_RenderExtents message) {
			if (message.Target != typeof(DataSource)) return;
			message.Bus.Consume(new Series_Extents(Name, DataSourceName, CategoryAxisName, Component1Minimum, Component1Maximum));
			message.Bus.Consume(new Series_Extents(Name, DataSourceName, ValueAxisName, Component2Minimum, Component2Maximum));
		}
		#endregion
	}
}
