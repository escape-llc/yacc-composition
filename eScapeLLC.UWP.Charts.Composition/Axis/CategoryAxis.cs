using eScape.Core;
using eScape.Host;
using eScapeLLC.UWP.Composition.Charts.Events;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using System.ServiceModel.Channels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using static eScapeLLC.UWP.Composition.Charts.ColumnSeries;

namespace eScapeLLC.UWP.Composition.Charts {
	public class CategoryAxis : AxisCommon, IRequireEnterLeave, IChartAxis, IDataSourceRenderSession<CategoryAxis.CategoryAxis_RenderState>,
		IConsumer<Series_Extents>,
		IConsumer<Phase_InitializeAxes>, IConsumer<Phase_FinalizeAxes>, IConsumer<Phase_Layout>, IConsumer<DataSource_RenderStart>, IConsumer<Phase_RenderTransforms> {
		static readonly LogTools.Flag _trace = LogTools.Add("CategoryAxis", LogTools.Level.Error);
		#region inner
		class CategoryAxis_RenderState : RenderStateCore<CategoryAxis_ItemState> {
			internal CategoryAxis_RenderState(List<ItemStateCore> state) : base(state) {
			}
		}
		class CategoryAxis_ItemState : ItemStateCore {
			internal FrameworkElement element;
			internal string label;
			public CategoryAxis_ItemState(int index) : base(index) { }
		}
		class CategoryAxis_RenderSession : RenderSession<CategoryAxis_RenderState> {
			internal CategoryAxis_RenderSession(IDataSourceRenderSession<CategoryAxis_RenderState> series, CategoryAxis_RenderState state) : base(series, state) { }
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
		#region handlers
		public void Consume(Phase_Layout message) {
			var space = AxisMargin + /*AxisLineThickness + */ (Orientation == AxisOrientation.Horizontal ? MinHeight : MinWidth);
			message.Context.ClaimSpace(this, Side, space);
		}
		public void Consume(Series_Extents message) {
			if (message.AxisName != Name) return;
			Extents(message);
		}
		public void Consume(Phase_InitializeAxes message) {
			ResetLimits();
			var msg = new Axis_Extents(Name, Minimum, Maximum, Side, Type, Reverse);
			message.Bus.Consume(msg);
		}
		public void Consume(DataSource_RenderStart message) {
			if (message.Name != DataSourceName) return;
			if (message.ExpectedItemType == null) return;
			LabelBinding = Binding.For(message.ExpectedItemType, LabelMemberPath);
			if (LabelBinding == null) return;
			message.Register(new CategoryAxis_RenderSession(this, new CategoryAxis_RenderState(new List<ItemStateCore>())));
		}
		public void Consume(Phase_FinalizeAxes message) {
			var msg = new Axis_Extents(Name, Minimum, Maximum, Side, Type, Reverse);
			message.Bus.Consume(msg);
		}
		float YOffSetFor() {
			if(Side == Side.Top) return 1;
			return 0;
		}
		public void Consume(Phase_RenderTransforms message) {
			if (AxisLabels.Count == 0) return;
			if (double.IsNaN(Minimum) || double.IsNaN(Maximum)) return;
			var rctx = message.ContextFor(this);
			var pmatrix = ProjectionFor(rctx.Area, Reverse);
			var matx = Matrix3x2.Multiply(pmatrix.model, pmatrix.proj);
			double dx = 0, dy = 0;
			foreach (CategoryAxis_ItemState state in AxisLabels) {
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
		protected (Matrix3x2 model, Matrix3x2 proj) ProjectionFor(Rect area, bool reverse) {
			switch (Side) {
				case Side.Bottom:
					return MatrixSupport.AxisBottom(area, Minimum, Maximum + 1, !reverse);
				case Side.Left:
					return MatrixSupport.AxisLeft(area, Minimum, Maximum + 1, !reverse);
				case Side.Right:
					return MatrixSupport.AxisRight(area, Minimum, Maximum + 1, !reverse);
				case Side.Top:
					return MatrixSupport.AxisTop(area, Minimum, Maximum + 1, !reverse);
			}
			throw new InvalidOperationException($"cannot determine projection for {Side}");
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
		#region IDataSourceRenderSession<CategoryAxis_RenderState>
		void IDataSourceRenderSession<CategoryAxis_RenderState>.Preamble(CategoryAxis_RenderState state, IChartRenderContext icrc) {
			ResetLimits();
		}
		void IDataSourceRenderSession<CategoryAxis_RenderState>.Render(CategoryAxis_RenderState state, int index, object item) {
			if (LabelBinding == null) return;
			state.ix = index;
			var istate = new CategoryAxis_ItemState(index);
			state.itemstate.Add(istate);
			if (LabelBinding.GetString(item, out string label) && !string.IsNullOrEmpty(label)) {
				istate.label = label;
				istate.element = CreateElement(label);
				Layer.Add(istate.element);
			}
		}
		void IDataSourceRenderSession<CategoryAxis_RenderState>.RenderComplete(CategoryAxis_RenderState state) {
		}
		void IDataSourceRenderSession<CategoryAxis_RenderState>.Postamble(CategoryAxis_RenderState state) {
			AxisLabels = state.itemstate;
			//Layer.Remove(state.recycler.Unused);
			//Layer.Add(state.recycler.Created);
			//RebuildAxisGeometry();
			Dirty = false;
		}
		#endregion
	}
}
