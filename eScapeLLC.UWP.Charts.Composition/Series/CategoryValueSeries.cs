using eScape.Host;
using eScapeLLC.UWP.Charts.Composition.Events;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace eScapeLLC.UWP.Charts.Composition {
	public abstract class CategoryValueSeries : ChartComponent,
		IConsumer<Phase_ComponentExtents>, IConsumer<Axis_Extents>, IConsumer<Phase_DataSourceOperation> {
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
		/// How to create the elements for this series.
		/// </summary>
		public IElementFactory ElementFactory { get; set; }
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
		/// <summary>
		/// Required to obtain value from DAO.
		/// </summary>
		protected Binding ValueBinding { get; set; }
		/// <summary>
		/// Obtain alternate label if not NULL.
		/// </summary>
		protected Binding LabelBinding { get; set; }
		#endregion
		#region helpers
		/// <summary>
		/// Update C1 and C2 limits.
		/// If a value is <see cref="double.NaN"/>, it is effectively ignored because NaN is NOT GT/LT ANY number, even itself.
		/// </summary>
		/// <param name="value_c1">Component 1 (Category). MAY be NaN.</param>
		/// <param name="value_c2s">Component 2 (Values).  MAY contain NaN.</param>
		protected void UpdateLimits(double value_c1, IEnumerable<double> value_c2s) {
			if (double.IsNaN(Component1Minimum) || value_c1 < Component1Minimum) { Component1Minimum = value_c1; }
			if (double.IsNaN(Component1Maximum) || value_c1 > Component1Maximum) { Component1Maximum = value_c1; }
			foreach (var vv in value_c2s) {
				if (double.IsNaN(Component2Minimum) || vv < Component2Minimum) { Component2Minimum = vv; }
				if (double.IsNaN(Component2Maximum) || vv > Component2Maximum) { Component2Maximum = vv; }
			}
		}
		protected void UpdateLimits(double value_c1, params double[] value_c2s) {
			UpdateLimits(value_c1, (IEnumerable<double>)value_c2s);
		}
		/// <summary>
		/// Reset the value and category limits to <see cref="double.NaN"/>.
		/// Sets <see cref="ChartComponent.Dirty"/> = true.
		/// </summary>
		protected void ResetLimits() {
			Component2Minimum = double.NaN; Component2Maximum = double.NaN;
			Component1Minimum = double.NaN; Component1Maximum = double.NaN;
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
		#region virtual data source handlers
		/// <summary>
		/// Reset exits any existing elements and enters all the elements given.
		/// </summary>
		/// <param name="dsr"></param>
		protected virtual void Reset(DataSource_Reset dsr) { }
		/// <summary>
		/// Sliding window exits and enters same number of elements on front/read respectively.
		/// </summary>
		/// <param name="slidingWindow"></param>
		protected virtual void SlidingWindow(DataSource_SlidingWindow slidingWindow) { }
		/// <summary>
		/// Add enters the given element at the given position.
		/// </summary>
		/// <param name="add"></param>
		protected virtual void Add(DataSource_Add add) { }
		/// <summary>
		/// Respond to axis information update in <see cref="CategoryAxis"/> and <see cref="ValueAxis"/>.
		/// </summary>
		protected abstract void UpdateModelTransform();
		#endregion
		#region handlers
		/// <summary>
		/// Respond with current series extents so axes can update.
		/// </summary>
		/// <param name="message"></param>
		void IConsumer<Phase_ComponentExtents>.Consume(Phase_ComponentExtents message) {
			message.Register(new Component_Extents(Name, DataSourceName, CategoryAxisName, Component1Minimum, Component1Maximum));
			message.Register(new Component_Extents(Name, DataSourceName, ValueAxisName, Component2Minimum, Component2Maximum));
		}
		/// <summary>
		/// Axis extents participate in the Model transform.
		/// </summary>
		/// <param name="message"></param>
		void IConsumer<Axis_Extents>.Consume(Axis_Extents message) {
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
		/// Dispatch to virtual handlers.
		/// </summary>
		/// <param name="message"></param>
		void IConsumer<Phase_DataSourceOperation>.Consume(Phase_DataSourceOperation message) {
			if (ElementFactory == null) return;
			if (message.Operation.Name != DataSourceName) return;
			if(message.Operation is DataSource_Typed dstt) {
				ValueBinding = Binding.For(dstt.ItemType, ValueMemberName);
				if (!string.IsNullOrEmpty(LabelMemberName)) {
					LabelBinding = Binding.For(dstt.ItemType, LabelMemberName);
				}
				else {
					LabelBinding = ValueBinding;
				}
				if (ValueBinding == null) return;
			}
			switch (message.Operation) {
				case DataSource_Add dsa:
					Add(dsa);
					break;
				case DataSource_Reset dsr:
					Reset(dsr);
					break;
				case DataSource_SlidingWindow dst:
					SlidingWindow(dst);
					break;
			}
		}
		#endregion
	}
}
