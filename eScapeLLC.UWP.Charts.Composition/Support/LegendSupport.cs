using System.Numerics;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace eScapeLLC.UWP.Charts.Composition {
	/// <summary>
	/// Static helpers for legend UI "swatches".
	/// </summary>
	public static class LegendSupport {
		/// <summary>
		/// This is the desired size for swatches.
		/// </summary>
		public static readonly Vector2 DesiredSize = new Vector2(24, 24);
		/// <summary>
		/// Create a "container" element we can insert a <see cref="Visual"/> into for the swatch.
		/// </summary>
		/// <returns></returns>
		public static FrameworkElement Create() {
			var fe = new Canvas() {
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Stretch,
				MinWidth = 16,
				MinHeight = 16,
				Width = DesiredSize.X,
				Height = DesiredSize.Y,
			};
			return fe;
		}
		/// <summary>
		/// Insert the visual and attach a handler to keep the size synchronized.
		/// </summary>
		/// <param name="fe">Receives new <see cref="Visual"/>.</param>
		/// <param name="vis">Target.</param>
		public static void SetVisual(FrameworkElement fe, Visual vis) {
			vis.Size = new Vector2((float)fe.ActualWidth, (float)fe.ActualHeight);
			ElementCompositionPreview.SetElementChildVisual(fe, vis);
			fe.SizeChanged += (sender, e) => {
				var vis2 = ElementCompositionPreview.GetElementChildVisual(sender as UIElement);
				if (vis2 == null) return;
				vis2.Size = new Vector2((float)fe.ActualWidth, (float)fe.ActualHeight);
			};
		}
	}
}
