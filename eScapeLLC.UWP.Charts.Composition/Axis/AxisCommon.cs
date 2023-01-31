using eScape.Core;
using eScapeLLC.UWP.Charts.Composition.Events;
using System.Numerics;
using System;
using Windows.Foundation;

namespace eScapeLLC.UWP.Charts.Composition {
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
		public AxisType Type { get; protected set; }
		public AxisOrientation Orientation { get; protected set; }
		Side _side;
		/// <summary>
		/// Side of the chart; also sets <see cref="Orientation"/>.
		/// </summary>
		public Side Side { get => _side; set { _side = value; AdjustOrientation(_side); } }
		public double Minimum { get; set; } = double.NaN;
		public double Maximum { get; set; } = double.NaN;
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
		public void ResetLimits() {
			Minimum = LimitMinimum;
			Maximum = LimitMaximum;
		}
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
			_trace.Verbose($"{Name} extents did:{did} min:{xmin} max:{xmax} s:{message.SeriesName} smin:{message.Minimum}  smax:{message.Maximum}");
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
