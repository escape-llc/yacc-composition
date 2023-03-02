using eScape.Core;
using eScape.Host;
using eScapeLLC.UWP.Charts.Composition.Events;
using System.Collections.Generic;
using System.Linq;

namespace eScapeLLC.UWP.Charts.Composition {
	#region DataSourceSeries
	/// <summary>
	/// Commits to a <see cref="DataSource"/> and defines virtual dispatch handlers for the operations.
	/// </summary>
	public abstract class DataSourceSeries : ChartComponent, IConsumer<Phase_DataSourceOperation> {
		static readonly LogTools.Flag _trace = LogTools.Add("DataSourceSeries", LogTools.Level.Error);
		#region properties
		/// <summary>
		/// MUST match the name of a data source.
		/// </summary>
		public string DataSourceName { get; set; }
		#endregion
		#region abstract operation handlers
		/// <summary>
		/// Reset exits all existing elements and enters all the elements given.
		/// </summary>
		/// <param name="reset"></param>
		protected abstract void Reset(DataSource_Reset reset);
		/// <summary>
		/// Sliding window exits and enters same number of elements on head/tail respectively.
		/// </summary>
		/// <param name="slidingWindow"></param>
		protected abstract void SlidingWindow(DataSource_SlidingWindow slidingWindow);
		/// <summary>
		/// Add enters the given element(s) at the given end.
		/// </summary>
		/// <param name="add"></param>
		protected abstract void Add(DataSource_Add add);
		/// <summary>
		/// Remove exits the given number of elements from indicated end.
		/// </summary>
		/// <param name="remove"></param>
		protected abstract void Remove(DataSource_Remove remove);
		/// <summary>
		/// Return TRUE if successfully configured.
		/// </summary>
		/// <param name="message">Use for configuration.</param>
		/// <returns>true: proceed; false: return.</returns>
		protected abstract bool TryConfigure(DataSource_Operation message);
		#endregion
		#region handlers
		/// <summary>
		/// Dispatch to virtual handlers.
		/// </summary>
		/// <param name="message"></param>
		void IConsumer<Phase_DataSourceOperation>.Consume(Phase_DataSourceOperation message) {
			if (message.Operation.Name != DataSourceName) return;
			if(!TryConfigure(message.Operation)) return;
			switch (message.Operation) {
				case DataSource_Add add:
					_trace.Verbose($"{Name} dso-add ds:{add.Name} front:{add.AtFront} ct:{add.NewItems.Count}");
					Add(add);
					break;
				case DataSource_Reset reset:
					_trace.Verbose($"{Name} dso-reset ds:{reset.Name} ct:{reset.Items.Count}");
					Reset(reset);
					break;
				case DataSource_SlidingWindow sw:
					_trace.Verbose($"{Name} dso-sw ds:{sw.Name} ct:{sw.NewItems.Count}");
					SlidingWindow(sw);
					break;
				case DataSource_Remove remove:
					_trace.Verbose($"{Name} dso-remove ds:{remove.Name} front:{remove.AtFront} ct:{remove.Count}");
					Remove(remove);
					break;
			}
		}
		#endregion
	}
	#endregion
	/// <summary>
	/// Base class for <see cref="DataSourceSeries"/> based on 2 components (category, value).
	/// </summary>
	/// <typeparam name="S">Item type. MUST NOT be an Inner Class!</typeparam>
	public abstract class CategoryValueSeries<S> : DataSourceSeries,
		IConsumer<Phase_ComponentExtents>, IConsumer<Phase_ModelComplete> where S: ItemStateCore {
		static readonly LogTools.Flag _trace = LogTools.Add("CategoryValueSeries", LogTools.Level.Error);
		#region properties
		/// <summary>
		/// MUST match the name of an axis.  Mapped to Component_1.
		/// </summary>
		public string CategoryAxisName { get; set; }
		/// <summary>
		/// MUST match the name of an axis.  Mapped to Component_2.
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
		/// How to create animations for series and its elements.
		/// </summary>
		public IAnimationFactory AnimationFactory { get; set; }
		/// <summary>
		/// The minimum category (value) seen.
		/// </summary>
		public double Component1Minimum { get; protected set; } = double.NaN;
		/// <summary>
		/// The maximum category (value) seen.
		/// </summary>
		public double Component1Maximum { get; protected set; } = double.NaN;
		/// <summary>
		/// The minimum value seen.
		/// </summary>
		public double Component2Minimum { get; protected set; } = double.NaN;
		/// <summary>
		/// The maximum value seen.
		/// </summary>
		public double Component2Maximum { get; protected set; } = double.NaN;
		/// <summary>
		/// Range of the values or <see cref="double.NaN"/> if <see cref="UpdateLimits(double, double)"/>or <see cref="UpdateLimits(double, double[])"/> was never called.
		/// </summary>
		public double Component1Range { get { return double.IsNaN(Component1Minimum) || double.IsNaN(Component1Maximum) ? double.NaN : Component1Maximum - Component1Minimum; } }
		/// <summary>
		/// Range of the values or <see cref="double.NaN"/> if <see cref="UpdateLimits(double, double)"/>or <see cref="UpdateLimits(double, double[])"/> was never called.
		/// </summary>
		public double Component2Range { get { return double.IsNaN(Component2Minimum) || double.IsNaN(Component2Maximum) ? double.NaN : Component2Maximum - Component2Minimum; } }
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
		/// <summary>
		/// Data needed for current state.
		/// </summary>
		protected List<ItemStateCore> ItemState { get; set; }
		/// <summary>
		/// MUST be NULL after processing.
		/// This is because the "end" phases execute regardless of the <see cref="DataSource"/> that caused it.
		/// </summary>
		protected IEnumerable<ItemStateOperation<S>> Pending { get; set; }
		#endregion
		#region ctor
		public CategoryValueSeries() {
			ItemState = new List<ItemStateCore>();
		}
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
		/// </summary>
		protected void ResetLimits() {
			Component2Minimum = double.NaN; Component2Maximum = double.NaN;
			Component1Minimum = double.NaN; Component1Maximum = double.NaN;
		}
		/// <summary>
		/// Locate required components and generate errors if they are not found.
		/// </summary>
		/// <param name="iccc">Use to locate components and report errors.</param>
		protected void EnsureAxes(IChartComponentContext iccc) {
			IChartErrorInfo icei = iccc as IChartErrorInfo;
			if (!string.IsNullOrEmpty(ValueAxisName)) {
				if (!(iccc.Find(ValueAxisName) is IChartAxis axis)) {
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
				if (!(iccc.Find(CategoryAxisName) is IChartAxis axis)) {
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
		/// <summary>
		/// Check that the required binding values are set and generate errors if not.
		/// </summary>
		/// <param name="iccc">Use to locate components and report errors.</param>
		protected void EnsureValuePath(IChartComponentContext iccc) {
			IChartErrorInfo icei = iccc as IChartErrorInfo;
			if (string.IsNullOrEmpty(ValueMemberName)) {
				icei?.Report(new ChartValidationResult(NameOrType(), $"Property '{nameof(ValueMemberName)}' was not set", new[] { nameof(ValueMemberName) }));
			}
		}
		#endregion
		#region virtual data source handlers
		/// <summary>
		/// Fabricate state used for entering items.
		/// </summary>
		/// <param name="items">Item source.</param>
		/// <returns>New instance.</returns>
		protected virtual IEnumerable<S> Entering(System.Collections.IList items) {
			for (int ix = 0; ix < items.Count; ix++) {
				var state = CreateState(ix, items[ix]);
				yield return state;
			}
		}
		/// <summary>
		/// Create the state needed for this series.
		/// </summary>
		/// <param name="index">Item index.</param>
		/// <param name="item">Data item.</param>
		/// <returns>New instance.</returns>
		protected abstract S CreateState(int index, object item);
		/// <summary>
		/// Reset exits all existing elements and enters all the elements given.
		/// </summary>
		/// <param name="reset"></param>
		protected override void Reset(DataSource_Reset reset) {
			Pending = reset.CreateOperations(ItemState, Entering);
		}
		/// <summary>
		/// Sliding window exits and enters same number of elements on head/tail respectively.
		/// </summary>
		/// <param name="slidingWindow"></param>
		protected override void SlidingWindow(DataSource_SlidingWindow slidingWindow) {
			Pending = slidingWindow.CreateOperations(ItemState, Entering);
		}
		/// <summary>
		/// Add enters the given element(s) at the given end.
		/// </summary>
		/// <param name="add"></param>
		protected override void Add(DataSource_Add add) {
			Pending = add.CreateOperations(ItemState, Entering);
		}
		/// <summary>
		/// Remove exits the given number of elements from indicated end.
		/// </summary>
		/// <param name="remove"></param>
		protected override void Remove(DataSource_Remove remove) {
			Pending = remove.CreateOperations<S>(ItemState);
		}
		/// <summary>
		/// Called during <see cref="Phase_ModelComplete"/>.
		/// Apply axis information from <see cref="CategoryAxis"/> and <see cref="ValueAxis"/> to the Model transform.
		/// </summary>
		protected abstract void UpdateModelTransform();
		/// <summary>
		/// Called during <see cref="Phase_ModelComplete"/>.
		/// Called after UpdateModelTransform().
		/// Perform final layout of geometry; all axis extents are finalized.
		/// </summary>
		protected abstract void ModelComplete();
		/// <summary>
		/// Called during <see cref="Phase_ComponentExtents"/>.
		/// Calculate component extents into XXXMinimum/XXXMaximum.
		/// </summary>
		protected abstract void ComponentExtents();
		/// <summary>
		/// Core part of the update cycle.
		/// </summary>
		/// <param name="items">Sequence of item operations.</param>
		protected virtual void UpdateCore(IOperationController<S> ioc, IEnumerable<ItemStateOperation<S>> items) {
			var itemstate = new List<ItemStateCore>();
			foreach (var item in items) {
				item.Execute(ioc, itemstate);
			}
			ItemState = itemstate;
		}
		#endregion
		#region handlers
		/// <summary>
		/// Calculate and respond with current series extents so axes can update.
		/// </summary>
		/// <param name="message"></param>
		void IConsumer<Phase_ComponentExtents>.Consume(Phase_ComponentExtents message) {
			ComponentExtents();
			message.Register(new Component_Extents(Name, DataSourceName, CategoryAxisName, Component1Minimum, Component1Maximum));
			message.Register(new Component_Extents(Name, DataSourceName, ValueAxisName, Component2Minimum, Component2Maximum));
		}
		/// <summary>
		/// Update model transform.  Signal model complete.
		/// </summary>
		/// <param name="message"></param>
		void IConsumer<Phase_ModelComplete>.Consume(Phase_ModelComplete message) {
			CategoryAxis = message.AxisExtents.SingleOrDefault(ax => ax.AxisName == CategoryAxisName);
			if (CategoryAxis == null) return;
			if (double.IsNaN(CategoryAxis.Minimum) || double.IsNaN(CategoryAxis.Maximum)) return;
			ValueAxis = message.AxisExtents.SingleOrDefault(ax => ax.AxisName == ValueAxisName);
			if (ValueAxis == null) return;
			if (double.IsNaN(ValueAxis.Minimum) || double.IsNaN(ValueAxis.Maximum)) return;
			UpdateModelTransform();
			ModelComplete();
		}
		protected override bool TryConfigure(DataSource_Operation operation) {
			if (ElementFactory == null) return false;
			if (operation is DataSource_Typed dstt) {
				ValueBinding = Binding.For(dstt.ItemType, ValueMemberName);
				if (!string.IsNullOrEmpty(LabelMemberName)) {
					LabelBinding = Binding.For(dstt.ItemType, LabelMemberName);
				}
				else {
					LabelBinding = ValueBinding;
				}
				if (ValueBinding == null) return false;
			}
			return true;
		}
		#endregion
	}
}
