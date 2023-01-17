using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eScapeLLC.UWP.Composition.Charts {
	public static class MappingSupport {
		/// <summary>
		/// Map components to their respective cartesian axes.
		/// Horizontal axis receives the X, Vertical axis receives the Y.
		/// </summary>
		/// <param name="c1">Component 1.</param>
		/// <param name="c2">Component 2.</param>
		/// <param name="c1ori">Component 1 axis orientation.</param>
		/// <param name="c2ori">Component 2 axis orientation.</param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		public static (double x, double y) MapComponents(double c1, double c2, AxisOrientation c1ori, AxisOrientation c2ori) {
			if (c1ori == c2ori) throw new ArgumentException($"Orientations are equal {c1ori}");
			// only 2 combinations remain
			if (c1ori == AxisOrientation.Horizontal && c2ori == AxisOrientation.Vertical) {
				return (c1, c2);
			}
			else if (c1ori == AxisOrientation.Vertical && c2ori == AxisOrientation.Horizontal) {
				return (c2, c1);
			}
			else {
				// won't get here...
				throw new InvalidOperationException($"Cannot map components c1:{c1ori} c2{c2ori}");
			}
		}
	}
}
