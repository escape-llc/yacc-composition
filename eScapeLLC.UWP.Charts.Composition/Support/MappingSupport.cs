using System;

namespace eScapeLLC.UWP.Charts.Composition {
	public static class MappingSupport {
		/// <summary>
		/// Map components to their respective cartesian axes.
		/// Horizontal axis receives the X, Vertical axis receives the Y.
		/// </summary>
		/// <param name="c1">Component 1.</param>
		/// <param name="c1ori">Component 1 axis orientation.</param>
		/// <param name="c2">Component 2.</param>
		/// <param name="c2ori">Component 2 axis orientation.</param>
		/// <returns></returns>
		/// <exception cref="ArgumentException">Orientations are equal</exception>
		/// <exception cref="InvalidOperationException">Cannot map components</exception>
		public static (double xx, double yy) MapComponents(double c1, AxisOrientation c1ori, double c2, AxisOrientation c2ori) {
			if (c1ori == c2ori) throw new ArgumentException($"Orientations are equal {c1ori}");
			// only 2 combinations remain
			if (c1ori == AxisOrientation.Horizontal && c2ori == AxisOrientation.Vertical) {
				return (c1, c2);
			}
			else if (c2ori == AxisOrientation.Horizontal && c1ori == AxisOrientation.Vertical) {
				return (c2, c1);
			}
			else {
				// won't get here...
				throw new InvalidOperationException($"Cannot map components c1:{c1ori} c2{c2ori}");
			}
		}
	}
}
