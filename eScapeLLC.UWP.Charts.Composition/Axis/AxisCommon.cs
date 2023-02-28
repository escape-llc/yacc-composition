using eScape.Core;
using eScapeLLC.UWP.Charts.Composition.Events;
using System.Numerics;
using System;
using Windows.Foundation;
using System.Collections.Immutable;

namespace eScapeLLC.UWP.Charts.Composition {
	#region Axis_Extents
	/// <summary>
	/// Register during <see cref="Phase_AxisExtents"/>.
	/// </summary>
	public class Axis_Extents {
		public readonly string AxisName;
		/// <summary>
		/// MAY be <see cref="double.NaN"/>.
		/// </summary>
		public readonly double Minimum;
		/// <summary>
		/// MAY be <see cref="double.NaN"/>.
		/// </summary>
		public readonly double Maximum;
		public readonly Side AxisSide;
		public readonly AxisType Type;
		public readonly bool Reversed;
		public readonly double Range;
		public Axis_Extents(string axisName, double minimum, double maximum, Side axisSide, AxisType axisType, bool reversed) {
			AxisName = axisName;
			Minimum = minimum;
			Maximum = maximum;
			AxisSide = axisSide;
			Type = axisType;
			Reversed = reversed;
			Range = double.IsNaN(minimum) || double.IsNaN(maximum) ? double.NaN : maximum - minimum;
		}
		public AxisOrientation Orientation => AxisSide == Side.Left || AxisSide == Side.Right ? AxisOrientation.Vertical : AxisOrientation.Horizontal;
	}
	#endregion
	#region Axis_Extents_TickValues
	/// <summary>
	/// Axis with tick values, e.g. a value axis.
	/// </summary>
	public sealed class Axis_Extents_TickValues : Axis_Extents {
		public readonly ImmutableArray<TickState> TickValues;
		public Axis_Extents_TickValues(string axisName, double minimum, double maximum, Side axisSide, AxisType axisType, bool reversed, ImmutableArray<TickState> tvs) : base(axisName, minimum, maximum, axisSide, axisType, reversed) {
			TickValues = tvs;
		}
	}
	#endregion
	#region IAxisLabelSelectorContext
	/// <summary>
	/// Base context for axis label selector/formatter.
	/// </summary>
	public interface IAxisLabelSelectorContext {
		/// <summary>
		/// Current axis label index.
		/// </summary>
		int Index { get; }
		/// <summary>
		/// The axis presenting labels.
		/// </summary>
		IChartAxis Axis { get; }
		/// <summary>
		/// Axis rendering area in DC.
		/// </summary>
		Rect Area { get; }
	}
	#endregion
	#region AxisCommon
	public abstract class AxisCommon : ChartComponent {
		static readonly LogTools.Flag _trace = LogTools.Add("AxisCommon", LogTools.Level.Error);
		#region properties
		/// <summary>
		/// Set by the axis instance.
		/// </summary>
		public AxisType Type { get; protected set; }
		/// <summary>
		/// Set in response to setting <see cref="Side"/>.
		/// </summary>
		public AxisOrientation Orientation { get; protected set; }
		Side _side;
		/// <summary>
		/// Side of the chart; also sets <see cref="Orientation"/>.
		/// </summary>
		public Side Side { get => _side; set { _side = value; AdjustOrientation(_side); } }
		/// <summary>
		/// Reflects extent of <see cref="LimitMinimum"/> or data values.
		/// </summary>
		public double Minimum { get; protected set; } = double.NaN;
		/// <summary>
		/// Reflects extent of <see cref="LimitMaximum"/> or data values.
		/// </summary>
		public double Maximum { get; protected set; } = double.NaN;
		/// <summary>
		/// Set this to override auto-scaling behavior on Minimum.
		/// Default value is NaN.
		/// </summary>
		public double LimitMinimum { get; set; } = double.NaN;
		/// <summary>
		/// Set this to override auto-scaling behavior on Maximum.
		/// Default value is NaN.
		/// </summary>
		public double LimitMaximum { get; set; } = double.NaN;
		/// <summary>
		/// The axis range or NaN if limits were not initialized.
		/// </summary>
		public double Range { get { return double.IsNaN(Minimum) || double.IsNaN(Maximum) ? double.NaN : Maximum - Minimum; } }
		/// <summary>
		/// The PX margin of the axis "line" from the edge of its bounds facing the data area.
		/// Default value is 2.
		/// </summary>
		public double AxisMargin { get; set; } = 2;
		#endregion
		/// <summary>
		/// Reset extents to <see cref="LimitMinimum"/> and <see cref="LimitMaximum"/>.
		/// </summary>
		public void ResetLimits() {
			Minimum = LimitMinimum;
			Maximum = LimitMaximum;
		}
		/// <summary>
		/// Update auto-scaled extent(s).
		/// </summary>
		/// <param name="value">Candidate value.</param>
		public void UpdateLimits(double value) {
			if (double.IsNaN(LimitMinimum) && (double.IsNaN(Minimum) || value < Minimum)) {
				Minimum = value;
			}
			if (double.IsNaN(LimitMaximum) && (double.IsNaN(Maximum) || value > Maximum)) {
				Maximum = value;
			}
		}
		/// <summary>
		/// Update the <see cref="Minimum"/> and <see cref="Maximum"/> based on incoming values.
		/// </summary>
		/// <param name="message"></param>
		protected void Extents(Component_Extents message) {
			bool did = false;
			double xmin = Minimum, xmax = Maximum;
			if (!double.IsNaN(message.Minimum)) {
				if (double.IsNaN(LimitMinimum) && (double.IsNaN(Minimum) || message.Minimum < Minimum)) {
					Minimum = message.Minimum;
					did = true;
				}
			}
			if (!double.IsNaN(message.Maximum)) {
				if (double.IsNaN(LimitMaximum) && (double.IsNaN(Maximum) || message.Maximum > Maximum)) {
					Maximum = message.Maximum;
					did = true;
				}
			}
			_trace.Verbose($"{Name} extents did:{did} min:{xmin} max:{xmax} s:{message.ComponentName} smin:{message.Minimum}  smax:{message.Maximum}");
			if (did) {
				//Dirty = true;
			}
		}
		private void AdjustOrientation(Side side) {
			switch (side) {
				case Side.Left:
				case Side.Right:
					Orientation = AxisOrientation.Vertical;
					break;
				case Side.Top:
				case Side.Bottom:
					Orientation = AxisOrientation.Horizontal;
					break;
			}
		}
		/// <summary>
		/// Compute matrices for the given projection rectangle and direction (ltr/rtl).
		/// </summary>
		/// <param name="area">Projection area in DC.</param>
		/// <param name="reverse">true: reverse direction.</param>
		/// <returns>new instance.</returns>
		/// <exception cref="InvalidOperationException">Cannot determine projection.</exception>
		protected (Matrix3x2 model, Matrix3x2 proj) ProjectionFor(Rect area, bool reverse) {
			switch (Side) {
				case Side.Bottom:
					return MatrixSupport.AxisBottom(area, Minimum, Maximum, !reverse);
				case Side.Left:
					return MatrixSupport.AxisLeft(area, Minimum, Maximum, !reverse);
				case Side.Right:
					return MatrixSupport.AxisRight(area, Minimum, Maximum, !reverse);
				case Side.Top:
					return MatrixSupport.AxisTop(area, Minimum, Maximum, !reverse);
			}
			throw new InvalidOperationException($"cannot determine projection for {Side}");
		}
	}
	#endregion
}
