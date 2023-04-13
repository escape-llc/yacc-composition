using eScape.Core;
using eScape.Host;
using eScapeLLC.UWP.Charts.Composition.Events;
using System;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Xml.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace eScapeLLC.UWP.Charts.Composition {
	#region _ItemState
	/// <summary>
	/// Item state.
	/// </summary>
	public class ImageMarkerSeries_ItemState : ItemState_CategoryValue<Visual> {
		Animation.MarkerBrush Local { get; set; }
		AxisOrientation Component1Axis { get; set; } = AxisOrientation.Horizontal;
		AxisOrientation Component2Axis { get; set; } = AxisOrientation.Vertical;
		public ImageMarkerSeries_ItemState(int index, double categoryOffset, double value) : base(index, categoryOffset, value) { }
		public void CreateAnimations(Compositor cx, CompositionPropertySet props, AxisOrientation c1a, AxisOrientation c2a) {
			Component1Axis = c1a;
			Component2Axis = c2a;
			var vx = MappingSupport.ToVector(Component1, Component1Axis, Component2, Component2Axis);
			Local = new Animation.MarkerBrush(cx, props);
			Local.Initial(vx);
		}
		public override Vector2 OffsetFor(AxisOrientation cori, AxisOrientation vori) {
			return MappingSupport.OffsetFor(Component1, cori, Component2, vori);
		}
		public override void SetElement(Visual el) {
			Local.Stop(Element);
			base.SetElement(el);
		}
		public override void ResetElement() {
			Local.Stop(Element);
			base.ResetElement();
		}
		/// <summary>
		/// Start animating to the current position.
		/// </summary>
		public Vector2 UpdateOffset() {
			var vx = OffsetFor(Component1Axis, Component2Axis);
			Local.To(vx);
			return vx;
		}
		/// <summary>
		/// Initailize animation position to given position.
		/// Expects subsequent call to <see cref="UpdateOffset"/> with "final" position.
		/// </summary>
		/// <param name="enter">Initial position.</param>
		/// <param name="it">Use for list operations.</param>
		/// <param name="vsc">Target collection.</param>
		public void Enter(Vector2 enter, ItemTransition it, VisualCollection vsc) {
			Local.Enter(Element, enter);
			if (it == ItemTransition.Head) {
				vsc.InsertAtTop(Element);
			}
			else {
				vsc.InsertAtBottom(Element);
			}
		}
		/// <summary>
		/// Animate from current position to given position, then remove from Visual Tree.
		/// </summary>
		/// <param name="exit">Exit position.</param>
		/// <param name="vsc">Target collection.</param>
		public void Exit(Vector2 exit, VisualCollection ssc) {
			CompositionScopedBatch ccb = Element.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
			ccb.Comment = $"ScopedBatch_{Element.Comment}";
			ccb.Completed += (sender, cbcea) => {
				try {
					ssc.Remove(Element);
					ResetElement();
				}
				catch(Exception ex) {
					ImageMarkerSeries._trace.Error($"ccb[{Index}].Completed: {ex}");
				}
				finally {
					ccb.Dispose();
				}
			};
			Local.To(exit);
			ccb.End();
		}
	}
	#endregion
	#region ImageMarkerSeries
	public class ImageMarkerSeries : CategoryValue_VisualPerItem<ImageMarkerSeries_ItemState>, IRequireEnterLeave, IConsumer<Phase_Transforms> {
		internal static readonly LogTools.Flag _trace = LogTools.Add("ImageMarkerSeries", LogTools.Level.Error);
		#region properties
		/// <summary>
		/// Generates brush for sprites.
		/// </summary>
		public IBrushFactory MarkerFactory { get; set; }
		/// <summary>
		/// Marker offset in category units.
		/// </summary>
		public double MarkerOffset { get; set; }
		/// <summary>
		/// Marker width in category units.
		/// Aspect ratio is preserved based on this measurement.
		/// </summary>
		public double MarkerWidth { get; set; }
		#endregion
		#region internal
		/// <summary>
		/// Shared reference to the marker brush.
		/// </summary>
		protected CompositionBrush Marker { get; set; }
		/// <summary>
		/// This will be (0,0) until surface is loaded.
		/// </summary>
		protected Vector2 MarkerSize { get; set; }
		/// <summary>
		/// Manage animations.
		/// </summary>
		Animation.Global Global { get; set; }
		#endregion
		#region helpers
		Vector2 Spawn(ImageMarkerSeries_ItemState item, ItemTransition it) {
			double c1 = it == ItemTransition.Head
				? CategoryAxis.Minimum - 2 + item.CategoryOffset
				: CategoryAxis.Maximum + 2 + item.CategoryOffset;
			var spawn = MappingSupport.OffsetFor(
					c1, CategoryAxis.Orientation,
					item.DataValue, ValueAxis.Orientation);
			return spawn;
		}
		#endregion
		#region extensions
		protected override void Entering(ImageMarkerSeries_ItemState item, ItemTransition it) {
			if (item == null || item.Element == null) return;
			item.Enter(Spawn(item, it), it, Container.Children);
		}
		protected override void Exiting(ImageMarkerSeries_ItemState item, ItemTransition it) {
			if (item == null || item.Element == null) return;
			item.Exit(Spawn(item, it), Container.Children);
		}
		protected override void UpdateOffset(ImageMarkerSeries_ItemState item) {
			if (item == null || item.Element == null) return;
			var to = item.UpdateOffset();
			_trace.Verbose($"{Name}[{item.Index}] update-offset val:{item.DataValue} to:{to.X},{to.Y}");
		}
		protected override void ComponentExtents() {
			if (Pending == null) return;
			_trace.Verbose($"{Name} component-extents");
			ResetLimits();
			int index = 0;
			foreach (var op in Pending.Where(xx => xx is ItemsWithOffset<ImageMarkerSeries_ItemState>)) {
				foreach (var item in (op as ItemsWithOffset<ImageMarkerSeries_ItemState>).Items) {
					UpdateLimits(index, item.DataValue, 0);
					index++;
				}
			}
			UpdateLimits(index);
		}
		protected override Visual CreateLegendVisual(Compositor cx) {
			var vis = cx.CreateSpriteVisual();
			EnsureMarker(cx);
			vis.Brush = Marker;
			vis.Size = new Vector2(24, 24);
			return vis;
		}
		protected void EnsureMarker(Compositor cx) {
			if (MarkerFactory == null) return;
			if (Marker != null) return;
			var iefc = new DefaultBrushFactoryContext(cx, (sender, args) => {
				_trace.Verbose($"{Name}.image.LoadCompleted {args.Status} ds:{sender.DecodedSize} ns:{sender.NaturalSize}");
				if (args.Status == LoadedImageSourceLoadStatus.Success) {
					Size decodedSize = sender.DecodedSize;
					MarkerSize = new Vector2((float)decodedSize.Width, (float)decodedSize.Height);
					Global.PropertySet.InsertScalar(Animation.MarkerBrush.PROP_AspectRatio, (float)decodedSize.Height / (float)decodedSize.Width);
				}
			});
			Marker = MarkerFactory.CreateBrush(iefc);
		}
		protected override Visual CreateVisual(Compositor cx, ImageMarkerSeries_ItemState state) {
			EnsureMarker(cx);
			state.CreateAnimations(cx, Global.PropertySet, CategoryAxis.Orientation, ValueAxis.Orientation);
			var element = cx.CreateSpriteVisual();
			element.Brush = Marker;
			element.Comment = $"{Name}[{state.GetHashCode()}]";
			element.AnchorPoint = new Vector2(0, .5f);
			_trace.Verbose($"{Name}[{state.Index}] create-shape val:{state.DataValue} cx:{state.Component1:F2},{state.Component2:F2}");
			return element;
		}
		protected override ImageMarkerSeries_ItemState CreateState(int index, object item) {
			if (ValueBinding.GetDouble(item, out double? value_val)) {
				// short-circuit if it's NaN or NULL
				if (!value_val.HasValue || double.IsNaN(value_val.Value)) {
					return null;
				}
				var istate = new ImageMarkerSeries_ItemState(index, MarkerOffset, value_val.Value);
				_trace.Verbose($"{Name}[{index}] create-state val:{istate.DataValue}");
				return istate;
			}
			return null;
		}
		protected override void ModelComplete() {
			if (Pending == null) return;
			if (Container == null) return;
			_trace.Verbose($"{Name} model-complete");
			try {
				UpdateCore(this, Pending);
			}
			finally {
				Pending = null;
			}
		}
		protected override void UpdateModelTransform() {
			Matrix3x2 model = CategoryAxis.Orientation == AxisOrientation.Horizontal
			? MatrixSupport.ModelFor(CategoryAxis.Minimum, CategoryAxis.Maximum, ValueAxis.Minimum, ValueAxis.Maximum)
			: MatrixSupport.ModelFor(ValueAxis.Minimum, ValueAxis.Maximum, CategoryAxis.Minimum, CategoryAxis.Maximum);
			Global.Model(model);
		}
		#endregion
		#region handlers
		/// <summary>
		/// Render area participates in the Projection transform.
		/// </summary>
		/// <param name="message"></param>
		void IConsumer<Phase_Transforms>.Consume(Phase_Transforms message) {
			if (CategoryAxis == null || ValueAxis == null) return;
			if (ItemState.Count == 0) return;
			if (Container == null) return;
			var rctx = message.ContextFor(this);
			var xaxis = CategoryAxis.Orientation == AxisOrientation.Horizontal ? CategoryAxis.Reversed : ValueAxis.Reversed;
			var yaxis = CategoryAxis.Orientation == AxisOrientation.Vertical ? CategoryAxis.Reversed : ValueAxis.Reversed;
			var q = MatrixSupport.QuadrantFor(!xaxis, !yaxis);
			var proj = MatrixSupport.ProjectForQuadrant(q, rctx.SeriesArea);
			Global.Projection(proj);
		}
		#endregion
		#region IRequireEnterLeave
		public void Enter(IChartEnterLeaveContext icelc) {
			EnsureAxes(icelc as IChartComponentContext);
			EnsureValuePath(icelc as IChartComponentContext);
			var icei = icelc as IChartErrorInfo;
			if (MarkerFactory == null) {
				icei?.Report(new ChartValidationResult(NameOrType(), $"Property '{nameof(MarkerFactory)}' was not set", new[] { nameof(MarkerFactory) }));
			}
			Compositor compositor = Window.Current.Compositor;
			Container = compositor.CreateContainerVisual();
			Container.Comment = $"container_{Name}";
			Layer = icelc.CreateLayer(Container);
			Global = new Animation.Global(compositor, Name);
			Global.PropertySet.InsertScalar(Animation.MarkerBrush.PROP_MarkerWidth, (float)MarkerWidth);
			Global.PropertySet.InsertScalar(Animation.MarkerBrush.PROP_AspectRatio, 1f);
			_trace.Verbose($"{Name} enter v:{ValueAxisName} {ValueAxis} c:{CategoryAxisName} {CategoryAxis} d:{DataSourceName}");
		}
		public void Leave(IChartEnterLeaveContext icelc) {
			_trace.Verbose($"{Name} leave");
			Global?.Dispose();
			Global = null;
			Container = null;
			icelc.DeleteLayer(Layer);
			Layer = null;
		}
		#endregion
	}
	#endregion
}
