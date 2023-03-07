using eScape.Host;
using eScapeLLC.UWP.Charts.Composition.Events;
using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace eScapeLLC.UWP.Charts.Composition {
	#region Component_Extents
	/// <summary>
	/// Register during <see cref="Phase_ComponentExtents"/>.
	/// </summary>
	public sealed class Component_Extents {
		/// <summary>
		/// MUST match receiver's name.
		/// </summary>
		public readonly string ComponentName;
		/// <summary>
		/// MUST match receiver's data source.
		/// MAY be NULL for components not tracking a <see cref="DataSource"/>.
		/// </summary>
		public readonly string DataSourceName;
		/// <summary>
		/// SHOULD receive one event for each axis.
		/// </summary>
		public readonly string AxisName;
		public readonly double Minimum;
		public readonly double Maximum;
		public Component_Extents(string componentName, string dataSourceName, string axisName, double minimum, double maximum) {
			ComponentName = componentName;
			DataSourceName = dataSourceName;
			AxisName = axisName;
			Minimum = minimum;
			Maximum = maximum;
		}
	}
	#endregion
	#region Component_Operation
	/// <summary>
	/// Send on the command port when an update occurs.
	/// </summary>
	public class Component_Operation : CommandPort_Operation {
		public readonly ChartComponent Component;
		public readonly RefreshRequestType Type;
		public readonly AxisUpdateState Axis;
		public Component_Operation(ChartComponent component, RefreshRequestType type, AxisUpdateState axis) {
			Component = component;
			Type = type;
			Axis = axis;
		}
	}
	#endregion
	#region IOperationController<S>
	/// <summary>
	/// Manage state operations on a sequence of state items.
	/// Including but not limited to Visual Tree management, Offset updates, Animation.
	/// </summary>
	/// <typeparam name="S">Item state type.</typeparam>
	public interface IOperationController<S> where S : ItemStateCore {
		/// <summary>
		/// This item is entering.
		/// </summary>
		/// <param name="index">Assigned index; MAY be different than the item's index.</param>
		/// <param name="it">Target end of list.</param>
		/// <param name="item">Target item.</param>
		void EnteringItem(int index, ItemTransition it, S item);
		/// <summary>
		/// This item is exiting.
		/// </summary>
		/// <param name="index">Exiting index; MAY be different than the item's index.</param>
		/// <param name="it">Target end of list.</param>
		/// <param name="item">Target item.</param>
		void ExitingItem(int index, ItemTransition it, S item);
		/// <summary>
		/// This item is "live" meaning it already existed before the operation started.
		/// MAY get reindexed.
		/// </summary>
		/// <param name="index">Assigned index; MAY be different than the item's index.</param>
		/// <param name="it">Target end of list.</param>
		/// <param name="item">Target item.</param>
		void LiveItem(int index, ItemTransition it, S item);
	}
	#endregion
	#region ItemStateOperation<S>
	/// <summary>
	/// Represents an operation on the item state list.
	/// </summary>
	/// <typeparam name="S">Type of the item state.</typeparam>
	public abstract class ItemStateOperation<S> where S : ItemStateCore {
		/// <summary>
		/// Target end of the list.
		/// </summary>
		public ItemTransition Transition { get; private set; }
		protected ItemStateOperation(ItemTransition it) {
			Transition = it;
		}
		/// <summary>
		/// Perform the operation.
		/// </summary>
		/// <param name="ilc">List controller callback.</param>
		/// <param name="itemstate">Accumulator.</param>
		public abstract void Execute(IOperationController<S> ilc, List<ItemStateCore> itemstate);
	}
	/// <summary>
	/// Successive operations MUST use continuous indices for entering and/or live items [0..i][i+1..n-1].
	/// Exiting items the index is not relevant and start at zero.
	/// </summary>
	/// <typeparam name="S">Type of the item state.</typeparam>
	public abstract class ItemsWithOffset<S> : ItemStateOperation<S> where S : ItemStateCore {
		/// <summary>
		/// The items affected (action indicated by subclasses).
		/// </summary>
		public IReadOnlyList<S> Items { get; private set; }
		/// <summary>
		/// Live and Entering share the same set of indices [0..i][i+1..n-1].
		/// Use this to offset the indices accordingly.
		/// </summary>
		public int Offset { get; private set; }
		protected ItemsWithOffset(ItemTransition it, IReadOnlyList<S> items, int offset = 0) : base(it) {
			Items = items;
			Offset = offset;
		}
	}
	/// <summary>
	/// Items are entering.
	/// </summary>
	/// <typeparam name="S">Type of the item state.</typeparam>
	public class ItemsEntering<S> : ItemsWithOffset<S> where S : ItemStateCore {
		public ItemsEntering(ItemTransition it, IReadOnlyList<S> items, int offset = 0) : base(it, items, offset) { }
		/// <summary>
		/// Make callback for entering item and accumulate.
		/// </summary>
		/// <param name="ilc">Use for callback.</param>
		/// <param name="itemstate">Use to accumulate.</param>
		public override void Execute(IOperationController<S> ilc, List<ItemStateCore> itemstate) {
			for (int ix = 0; ix < Items.Count; ix++) {
				ilc.EnteringItem(Offset + ix, Transition, Items[ix]);
				itemstate.Add(Items[ix]);
			}
		}
	}
	/// <summary>
	/// Items are live, and MAY move to a different start index.
	/// </summary>
	/// <typeparam name="S">Type of the item state.</typeparam>
	public class ItemsLive<S> : ItemsWithOffset<S> where S : ItemStateCore {
		public ItemsLive(ItemTransition it, IReadOnlyList<S> items, int offset = 0) : base(it, items, offset) { }
		/// <summary>
		/// Make callback for live item and accumulate.
		/// </summary>
		/// <param name="ilc">Use for callback.</param>
		/// <param name="itemstate">Use to accumulate.</param>
		public override void Execute(IOperationController<S> ilc, List<ItemStateCore> itemstate) {
			for (int ix = 0; ix < Items.Count; ix++) {
				ilc.LiveItem(Offset + ix, Transition, Items[ix]);
				itemstate.Add(Items[ix]);
			}
		}
	}
	/// <summary>
	/// Items are exiting.
	/// </summary>
	/// <typeparam name="S">Type of the item state.</typeparam>
	public class ItemsExiting<S> : ItemStateOperation<S> where S : ItemStateCore {
		/// <summary>
		/// The item(s) exiting.
		/// </summary>
		public IReadOnlyList<S> Items { get; private set; }
		public ItemsExiting(ItemTransition it, IReadOnlyList<S> items) : base(it) {
			Items = items;
		}
		/// <summary>
		/// Make callback for live item and DO NOT accumulate.
		/// </summary>
		/// <param name="ilc">Use for callback.</param>
		/// <param name="itemstate">Not used.</param>
		public override void Execute(IOperationController<S> ilc, List<ItemStateCore> itemstate) {
			for (int ix = 0; ix < Items.Count; ix++) {
				ilc.ExitingItem(ix, Transition, Items[ix]);
			}
		}
	}
	#endregion
	#region ChartComponent
	/// <summary>
	/// Base class of chart components.
	/// </summary>
	public abstract class ChartComponent : FrameworkElement, IConsumer<DataContextChangedEventArgs> {
		#region properties
		/// <summary>
		/// Use to enqueue unit-of-work to the render pipeline.
		/// </summary>
		public IForwardCommandPort<Component_Request, Component_Operation> Forward { get; set; }
		/// <summary>
		/// Return the name if set, otherwise the type.
		/// </summary>
		/// <returns>Name or type.</returns>
		public string NameOrType() { return string.IsNullOrEmpty(Name) ? GetType().Name : Name; }
		#endregion
		#region protected
		/// <summary>
		/// Locate required components and generate errors if they are not found.
		/// </summary>
		/// <param name="iccc">Use to locate components and report errors.</param>
		protected void EnsureAxis(IChartComponentContext iccc, string name) {
			IChartErrorInfo icei = iccc as IChartErrorInfo;
			if (!string.IsNullOrEmpty(name)) {
				if (!(iccc.Find(name) is IChartAxis axis)) {
					icei?.Report(new ChartValidationResult(NameOrType(), $"Value axis '{name}' was not found", new[] { nameof(name) }));
				}
				else {
					if (axis.Type != AxisType.Value) {
						icei?.Report(new ChartValidationResult(NameOrType(), $"Value axis '{name}' Type {axis.Type} is not Value", new[] { nameof(name) }));
					}
				}
			}
			else {
				icei?.Report(new ChartValidationResult(NameOrType(), $"Property '{nameof(name)}' was not set", new[] { nameof(name) }));
			}
		}
		#endregion
		#region handlers
		void IConsumer<DataContextChangedEventArgs>.Consume(DataContextChangedEventArgs args) {
			if (DataContext != args.NewValue) {
				DataContext = args.NewValue;
			}
		}
		#endregion
		#region static
		/// <summary>
		/// Bind source.Path to the target.DP.
		/// </summary>
		/// <param name="source">Source instance.</param>
		/// <param name="path">Component's (source) property path.</param>
		/// <param name="target">Target DO.</param>
		/// <param name="dp">FE's (target) DP.</param>
		public static void BindTo(object source, string path, DependencyObject target, DependencyProperty dp) {
			Windows.UI.Xaml.Data.Binding bx = new Windows.UI.Xaml.Data.Binding() {
				Path = new PropertyPath(path),
				Source = source,
				Mode = BindingMode.OneWay
			};
			target.ClearValue(dp);
			BindingOperations.SetBinding(target, dp, bx);
		}
		#endregion
	}
	#endregion
}
