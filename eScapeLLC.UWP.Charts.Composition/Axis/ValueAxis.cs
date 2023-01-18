using eScape.Core;
using eScape.Host;
using eScapeLLC.UWP.Charts.Composition.Events;
using System;

namespace eScapeLLC.UWP.Charts.Composition {
	public class ValueAxis : AxisCommon, IChartAxis, IRequireEnterLeave, IConsumer<Series_Extents>, IConsumer<Phase_InitializeAxes>, IConsumer<Phase_FinalizeAxes>, IConsumer<Phase_Layout> {
		static readonly LogTools.Flag _trace = LogTools.Add("CategoryAxis", LogTools.Level.Error);
		#region properties
		/// <summary>
		/// Whether to reverse the direction of the axis.
		/// </summary>
		public bool Reverse { get; set; }
		/// <summary>
		/// The layer to manage components.
		/// </summary>
		protected IChartLayer Layer { get; set; }
		#endregion
		#region ctor
		public ValueAxis() {
			Type = AxisType.Value;
			Side = Side.Left;
		}
		#endregion
		#region IRequireEnterLeave
		public void Enter(IChartEnterLeaveContext icelc) {
			Layer = icelc.CreateLayer();
		}
		public void Leave(IChartEnterLeaveContext icelc) {
			icelc.DeleteLayer(Layer);
			Layer = null;
		}
		#endregion
		#region handlers
		public void Consume(Series_Extents message) {
			if (message.AxisName != Name) return;
			Extents(message);
		}
		public void Consume(Phase_InitializeAxes message) {
			ResetLimits();
			var msg = new Axis_Extents(Name, Minimum, Maximum, Side, Type, Reverse);
			message.Bus.Consume(msg);
		}
		public void Consume(Phase_FinalizeAxes message) {
			var msg = new Axis_Extents(Name, Minimum, Maximum, Side, Type, Reverse);
			message.Bus.Consume(msg);
		}
		public void Consume(Phase_Layout message) {
			var space = AxisMargin + /*AxisLineThickness + */ MinWidth;
			message.Context.ClaimSpace(this, Side, space);
		}
		#endregion
		#region IChartAxis
		double IChartAxis.For(double value) {
			return value;
		}
		double IChartAxis.ScaleFor(double dimension) {
			throw new NotImplementedException();
		}
		#endregion
	}
}
