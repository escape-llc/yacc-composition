using System;
using System.Collections.Generic;

namespace eScapeLLC.UWP.Charts {
	#region TickState
	/// <summary>
	/// Data holder for <see cref="TickCalculator.GetTicks"/> or similar API.
	/// To get ticks in increasing numeric order, sort by <see cref="Index"/> ascending.
	/// </summary>
	public class TickState {
		/// <summary>
		/// Index of the value.  MAY be negative.
		/// 0: "center" tick, -N: "below" center (by N ticks), +N: "above" center (by N ticks).
		/// </summary>
		public int Index { get; private set; }
		/// <summary>
		/// Tick value.
		/// </summary>
		public double Value { get; private set; }
		/// <summary>
		/// Ctor.
		/// </summary>
		/// <param name="idx"></param>
		/// <param name="val"></param>
		public TickState(int idx, double val) { Index = idx; Value = val; }
	}
	#endregion
	#region TickCalculator
	/// <summary>
	/// Helper to calculate grid lines/tick marks.
	/// The "sweet spot" for grids is 10, so calculations use base-10 logarithms.
	/// The grid "origin" is selected as follows:
	///		If the extents have opposing signs, ZERO is the "center" point (regardless of where it falls),
	///		Otherwise it's the arithmetic midpoint (Minimum + Range/2) rounded to the nearest TickInterval.
	/// </summary>
	public class TickCalculator {
		#region properties
		/// <summary>
		/// The low extent.
		/// </summary>
		public double Minimum { get; private set; }
		/// <summary>
		/// The high extent.
		/// </summary>
		public double Maximum { get; private set; }
		/// <summary>
		/// Range of the interval.
		/// Initialized in ctor.
		/// </summary>
		public double Range { get; private set; }
		/// <summary>
		/// Computed tick interval, based on range.
		/// Initialized in ctor.
		/// SHOULD be a Power of Ten.
		/// </summary>
		public double TickInterval { get; private set; }
		/// <summary>
		/// Power of 10 to use for ticks.
		/// Based on Log10(Range).
		/// </summary>
		public int DecimalPlaces { get; private set; }
		#endregion
		#region ctor
		/// <summary>
		/// Ctor.
		/// Initialize Range, TickInterval.
		/// </summary>
		/// <param name="minimum">The minimum value.</param>
		/// <param name="maximum">The maximum value.</param>
		public TickCalculator(double minimum, double maximum) {
			Minimum = minimum;
			Maximum = maximum;
			Range = Math.Abs(maximum - minimum);
			DecimalPlaces = (int)Math.Round(Math.Log10(Range) - 1.0);
			TickInterval = Math.Pow(10, DecimalPlaces);
		}
		#endregion
		#region public
		/// <summary>
		/// Round the number to nearest multiple.
		/// </summary>
		/// <param name="val">Source value.</param>
		/// <param name="multiple">Multiple for rounding.</param>
		/// <returns>Rounded value.</returns>
		public static double RoundTo(double val, double multiple) {
			return Math.Round(val / multiple) * multiple;
		}
		/// <summary>
		/// Compare the values, using the given threshold.
		/// As is common in floating-point-land, the bit pattern produced by a numeric "string" may not match exactly.
		/// Underlying cause is the power-of-two/power-of-ten mismatch.
		/// There is also possibility of accumulated error, depending on source calculations.
		/// </summary>
		/// <param name="v1">Value 1.</param>
		/// <param name="v2">Value 2.</param>
		/// <param name="epsilon">Threshold for comparison.</param>
		/// <returns>True: under threshold; False: over or equal threshold.</returns>
		public static bool Equals(double v1, double v2, double epsilon) {
			return Math.Abs(v1 - v2) < epsilon;
		}
		/// <summary>
		/// Enumerate the ticks.
		/// Starts at the "center" and works "outward" toward each extent, alternating positive/negative direction.
		/// Once an extent is "filled", only values of the opposite extent SHALL appear.
		/// If extents "cross" zero, start at zero, otherwise the "center" of the range, to nearest tick.
		/// Generator: [0] the "center", [ix] "above-center" tick, [-ix] "below-center" tick
		/// Generator pattern: 0, 1, -1, 2, -2,...
		/// </summary>
		/// <returns>Series of values, as described above.</returns>
		public IEnumerable<TickState> GetTicks() {
			var center = Math.Sign(Minimum) != Math.Sign(Maximum) ? 0 : RoundTo(Minimum + Range / 2, TickInterval);
			yield return new TickState(0, center);
			var inside = true;
			for (int ix = 1; inside; ix++) {
				bool didu = false, didl = false;
				// do it this way to avoid accumulated error
				var upper = center + ix * TickInterval;
				var lower = center - ix * TickInterval;
				if (upper <= Maximum) {
					yield return new TickState(ix, upper);
					didu = true;
				}
				if (lower >= Minimum) {
					yield return new TickState(-ix, lower);
					didl = true;
				}
				inside = didu || didl;
			}
		}
		#endregion
	}
	#endregion
}
