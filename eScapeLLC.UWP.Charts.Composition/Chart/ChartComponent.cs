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
	#region IListController<S>
	/// <summary>
	/// Manage state operations on a list of state items.
	/// Including but not limited to Visual Tree management, Offset updates, Animation.
	/// </summary>
	/// <typeparam name="S">Item state type.</typeparam>
	public interface IListController<S> where S : ItemStateCore {
		/// <summary>
		/// This item is entering.
		/// </summary>
		/// <param name="index">Current index; MAY be different than the item's index.</param>
		/// <param name="item">Target item.</param>
		void EnteringItem(int index, ItemTransition it, S item);
		/// <summary>
		/// This item is exiting.
		/// </summary>
		/// <param name="index">Current (exiting) index; MAY be different than the item's index.</param>
		/// <param name="item">Target item.</param>
		void ExitingItem(int index, ItemTransition it, S item);
		/// <summary>
		/// This item is "live" meaning it already existed before the operation started.
		/// </summary>
		/// <param name="index">Current index; MAY be different than the item's index.</param>
		/// <param name="item">Target item.</param>
		void LiveItem(int index, ItemTransition it, S item);
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
		public IForwardCommandPort<Component_RefreshRequest> Forward { get; set; }
		/// <summary>
		/// Return the name if set, otherwise the type.
		/// </summary>
		/// <returns>Name or type.</returns>
		public string NameOrType() { return string.IsNullOrEmpty(Name) ? GetType().Name : Name; }
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
		/// <summary>
		/// Generic processing of the exit/live/enter items.
		/// </summary>
		/// <param name="list">Instruction list.</param>
		/// <param name="itemstate">Output list. Accumulates Live and Enter items.</param>
		public static void ProcessItems<S>(IEnumerable<(ItemStatus st, ItemTransition it, S state)> list, IListController<S> ilc, List<ItemStateCore> itemstate) where S : ItemStateCore {
			int index = 0;
			int xindex = 0;
			foreach ((ItemStatus st, ItemTransition it, S state) in list) {
				if (state == null) continue;
				switch (st) {
					case ItemStatus.Exit:
						ilc.ExitingItem(xindex, it, state);
						xindex++;
						break;
					case ItemStatus.Live:
						ilc.LiveItem(index, it, state);
						itemstate.Add(state);
						index++;
						break;
					case ItemStatus.Enter:
						ilc.EnteringItem(index, it, state);
						itemstate.Add(state);
						index++;
						break;
				}
			}
		}
		#endregion
	}
	#endregion
}
