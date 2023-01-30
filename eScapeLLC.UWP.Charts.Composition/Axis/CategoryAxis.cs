using eScape.Core;
using eScape.Host;
using eScapeLLC.UWP.Charts.Composition.Events;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using System.Security.Cryptography;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace eScapeLLC.UWP.Charts.Composition {
	public class CategoryAxis : AxisCommon,
		IRequireEnterLeave, IChartAxis,
		IConsumer<Component_Extents>, IConsumer<Phase_InitializeAxes>, IConsumer<Phase_AxisExtents>, IConsumer<Phase_Layout>,
		IConsumer<Phase_DataSourceOperation>, IConsumer<Phase_RenderTransforms> {
		static readonly LogTools.Flag _trace = LogTools.Add("CategoryAxis", LogTools.Level.Error);
		#region inner
		class Axis_RenderState : RenderStateCore<Axis_ItemState> {
			internal Axis_RenderState(List<ItemStateCore> state) : base(state) {
			}
		}
		class Axis_ItemState : ItemStateCore {
			internal FrameworkElement element;
			internal string label;
			public Axis_ItemState(int index) : base(index) { }
		}
		#endregion
		#region properties
		/// <summary>
		/// Which data source to target.
		/// </summary>
		public string DataSourceName { get; set; }
		/// <summary>
		/// Whether to reverse the direction of the axis.
		/// </summary>
		public bool Reverse { get; set; }
		/// <summary>
		/// Select the label value.
		/// </summary>
		public string LabelMemberPath { get; set; }
		/// <summary>
		/// The layer to manage components.
		/// </summary>
		protected IChartLayer Layer { get; set; }
		/// <summary>
		/// List of active TextBlocks for labels.
		/// </summary>
		protected List<ItemStateCore> AxisLabels { get; set; }
		protected Binding LabelBinding { get; set; }
		#endregion
		#region ctor
		public CategoryAxis() {
			Type = AxisType.Category;
			Side = Side.Bottom;
			AxisLabels = new List<ItemStateCore>();
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
		#region virtual data source handlers
		protected virtual void Reset(DataSource_Reset dsr) {
			var exit = new List<ItemStateCore>();
			var enter = new List<ItemStateCore>();
			if (AxisLabels.Count > 0) {
				// exit
				exit.AddRange(AxisLabels);
			}
			var itemstate = new List<ItemStateCore>();
			for (int ix = 0; ix < dsr.Items.Count; ix++) {
				var state = CreateState(ix, dsr.Items[ix]);
				itemstate.Add(state);
				if (state != null) {
					enter.Add(state);
				}
			}
			foreach (Axis_ItemState item in exit) {
				if (item != null && item.element != null) {
					Layer.Remove(item.element);
				}
			}
			ResetLimits();
			// reset limits
			foreach (Axis_ItemState item in enter) {
				if (item != null && item.element != null) {
					Layer.Add(item.element);
				}
			}
			AxisLabels = itemstate;
		}
		protected virtual void SlidingWindow(DataSource_SlidingWindow slidingWindow) { }
		protected virtual void Add(DataSource_Add add) { }
		#endregion
		#region handlers
		void IConsumer<Phase_Layout>.Consume(Phase_Layout message) {
			var space = AxisMargin + /*AxisLineThickness + */ (Orientation == AxisOrientation.Horizontal ? MinHeight : MinWidth);
			message.Context.ClaimSpace(this, Side, space);
		}
		void IConsumer<Component_Extents>.Consume(Component_Extents message) {
			if (message.AxisName != Name) return;
			Extents(message);
		}
		void IConsumer<Phase_InitializeAxes>.Consume(Phase_InitializeAxes message) {
			ResetLimits();
			var msg = new Axis_Extents(Name, Minimum, Maximum, Side, Type, Reverse);
			message.Reply(msg);
		}
		void IConsumer<Phase_DataSourceOperation>.Consume(Phase_DataSourceOperation message) {
			if (message.Operation.Name != DataSourceName) return;
			if(message.Operation is DataSource_Typed dstt) {
				LabelBinding = Binding.For(dstt.ItemType, LabelMemberPath);
				if (LabelBinding == null) return;
			}
			switch (message.Operation) {
				case DataSource_Add dsa:
					Add(dsa);
					break;
				case DataSource_Reset dsr:
					Reset(dsr);
					break;
				case DataSource_SlidingWindow dst:
					SlidingWindow(dst);
					break;
			}
		}
		void IConsumer<Phase_AxisExtents>.Consume(Phase_AxisExtents message) {
			var msg = new Axis_Extents(Name, Minimum, Maximum, Side, Type, Reverse);
			message.Register(msg);
		}
		float YOffSetFor() {
			if(Side == Side.Top) return 1;
			return 0;
		}
		void IConsumer<Phase_RenderTransforms>.Consume(Phase_RenderTransforms message) {
			if (AxisLabels.Count == 0) return;
			if (double.IsNaN(Minimum) || double.IsNaN(Maximum)) return;
			var rctx = message.ContextFor(this);
			var pmatrix = ProjectionFor(rctx.Area, Reverse);
			var matx = Matrix3x2.Multiply(pmatrix.model, pmatrix.proj);
			double dx = 0, dy = 0;
			foreach (Axis_ItemState state in AxisLabels) {
				if (state.element == null) continue;
				var point = new Vector2(state.Index + (Reverse ? 1 : 0), YOffSetFor());
				var dc = Vector2.Transform(point, matx);
				try {
					state.element.Translation = new Vector3((float)(dc.X + dx), (float)(dc.Y + dy), 0);
				}
				catch (Exception) { //eat it
				}
			}
		}
		#endregion
		#region helpers
		FrameworkElement CreateElement(string text) {
			var tb = new TextBlock() { Text = text, HorizontalAlignment = HorizontalAlignment.Left, HorizontalTextAlignment = TextAlignment.Left };
			return tb;
		}
		Axis_ItemState CreateState(int index, object item) {
			var istate = new Axis_ItemState(index);
			if (LabelBinding.GetString(item, out string label) && !string.IsNullOrEmpty(label)) {
				istate.label = label;
				istate.element = CreateElement(label);
			}
			return istate;
		}
		#endregion
		#region IChartAxis deprecate add to event
		double IChartAxis.For(double value) {
			return value;
		}
		double IChartAxis.ScaleFor(double dimension) {
			throw new NotImplementedException();
		}
		#endregion
	}
}
