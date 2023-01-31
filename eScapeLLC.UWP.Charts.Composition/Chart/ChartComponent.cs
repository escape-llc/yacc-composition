using eScape.Host;
using eScapeLLC.UWP.Charts.Composition.Events;
using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace eScapeLLC.UWP.Charts.Composition {
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
	/// <summary>
	/// Base class of chart components.
	/// </summary>
	public abstract class ChartComponent : FrameworkElement, IConsumer<DataContextChangedEventArgs> {
		#region properties
		/// <summary>
		/// Used for unsolicited messages.
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
		public static void BindTo(object source, String path, DependencyObject target, DependencyProperty dp) {
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
}
