﻿using System;
using System.Numerics;

namespace eScapeLLC.UWP.Charts.Composition {
	public static class MappingSupport {
		/// <summary>
		/// Return the "opposite" orientation.
		/// </summary>
		/// <param name="ao">Candidate orientation.</param>
		/// <returns>Opposite orientation.</returns>
		public static AxisOrientation OppositeOf(AxisOrientation ao) => ao == AxisOrientation.Vertical ? AxisOrientation.Horizontal : AxisOrientation.Vertical;
		/// <summary>
		/// Map components to their respective cartesian axes.
		/// Horizontal -> X, Vertical -> Y.
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
		/// <summary>
		/// Helper to create a <see cref="Vector2"/> which is most common return value.
		/// </summary>
		/// <param name="c1">Component 1.</param>
		/// <param name="c1ori">Component 1 axis orientation.</param>
		/// <param name="c2">Component 2.</param>
		/// <param name="c2ori">Component 2 axis orientation.</param>
		/// <returns></returns>
		public static Vector2 ToVector(double c1, AxisOrientation c1ori, double c2, AxisOrientation c2ori) {
			var (xx, yy) = MapComponents(c1, c1ori, c2, c2ori);
			return new Vector2((float)xx, (float)yy);
		}
		/// <summary>
		/// Calculate offset for Column series sprite.
		/// If the value is negative, adjust the vertical offset by that amount.
		/// </summary>
		/// <param name="c1">Component 1.</param>
		/// <param name="c1ori">C1 axis orientation.</param>
		/// <param name="c2">Component 2.</param>
		/// <param name="c2ori">C2 axis orientation.</param>
		/// <returns>Value to use for the Offset.</returns>
		/// <exception cref="ArgumentException"></exception>
		public static Vector2 OffsetForColumn(double c1, AxisOrientation c1ori, double c2, AxisOrientation c2ori) {
			return MappingSupport.ToVector(c1, c1ori, Math.Min(c2, 0), c2ori);
		}
		/// <summary>
		/// Offsets to the point (C_1,C_2) exactly.
		/// Sprite MUST be able to keep itself centered based on its current size.
		/// </summary>
		/// <param name="c1ori">Category axis orientation.</param>
		/// <param name="c2ori">Value axis orientation.</param>
		/// <returns>Value to use for the Offset.</returns>
		/// <exception cref="ArgumentException"></exception>
		public static Vector2 OffsetFor(double c1, AxisOrientation c1ori, double c2, AxisOrientation c2ori) {
			return MappingSupport.ToVector(c1, c1ori, c2, c2ori);
		}
	}
}
