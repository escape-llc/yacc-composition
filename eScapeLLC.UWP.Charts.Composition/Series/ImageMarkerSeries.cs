using eScape.Core;
using eScape.Host;
using eScapeLLC.UWP.Charts.Composition.Events;
using System;
using System.Linq;
using System.Numerics;
using Windows.ApplicationModel.Background;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace eScapeLLC.UWP.Charts.Composition {
	#region _ItemState
	/// <summary>
	/// Item state.
	/// </summary>
	public class ImageMarkerSeries_ItemState : ItemState_CategoryValue<Visual> {
		CompositionPropertySet PropertySet { get; set; }
		Vector2KeyFrameAnimation Position { get; set; }
		ExpressionAnimation Offset { get; set; }
		ExpressionAnimation Size { get; set; }
		AxisOrientation Component1Axis { get; set; } = AxisOrientation.Horizontal;
		AxisOrientation Component2Axis { get; set; } = AxisOrientation.Vertical;
		public ImageMarkerSeries_ItemState(int index, double categoryOffset, double value) : base(index, categoryOffset, value) { }
		const string PROP_Position = "Position";
		const string PARM_Position = "Position";
		const string PARM_global = "global";
		const string PARM_local = "local";
		readonly static string XAXIS = $"({PARM_local}.Position.X * {PARM_global}.ModelX.X + {PARM_global}.ModelX.Z) * {PARM_global}.ProjX.X + {PARM_global}.ProjX.Z";
		readonly static string YAXIS = $"({PARM_local}.Position.Y * {PARM_global}.ModelY.Y + {PARM_global}.ModelY.Z) * {PARM_global}.ProjY.Y + {PARM_global}.ProjY.Z";
		readonly static string WIDTH = $"{PARM_global}.MarkerWidth*{PARM_global}.ModelX.X*{PARM_global}.ProjX.X";
		readonly static string OFFSET = $"Vector3({XAXIS}, {YAXIS}, 0)";
		readonly static string SIZE = $"Vector2({WIDTH}, ({WIDTH})*{PARM_global}.AspectRatio)";
		public void CreateAnimations(Compositor cx, CompositionPropertySet props, AxisOrientation c1a, AxisOrientation c2a) {
			Component1Axis = c1a;
			Component2Axis = c2a;
			var vx = MappingSupport.ToVector(Component1, Component1Axis, Component2, Component2Axis);
			Position = cx.CreateVector2KeyFrameAnimation();
			Position.Comment = $"Marker[{Index}]_Position";
			Position.StopBehavior = AnimationStopBehavior.SetToFinalValue;
			Position.InsertExpressionKeyFrame(1f, PARM_Position);
			Position.SetVector2Parameter(PARM_Position, vx);
			Position.Target = PROP_Position;
			PropertySet = cx.CreatePropertySet();
			PropertySet.Comment = $"Marker[{Index}]_PropertySet";
			PropertySet.InsertVector2(PROP_Position, vx);
			Offset = cx.CreateExpressionAnimation(OFFSET);
			Offset.Comment = $"Marker[{Index}]_Offset";
			Offset.SetExpressionReferenceParameter(PARM_local, PropertySet);
			Offset.SetExpressionReferenceParameter(PARM_global, props);
			Offset.Target = nameof(Visual.Offset);
			Size = cx.CreateExpressionAnimation(SIZE);
			Size.Comment = $"Marker[{Index}]_Size";
			Size.SetExpressionReferenceParameter(PARM_local, PropertySet);
			Size.SetExpressionReferenceParameter(PARM_global, props);
			Size.Target = nameof(Visual.Size);
		}
		public override Vector2 OffsetFor(AxisOrientation cori, AxisOrientation vori) {
			return MappingSupport.OffsetFor(Component1, cori, Component2, vori);
		}
		public override void SetElement(Visual el) {
			if(Element != null) {
				Element.StopAnimation(Size.Target);
				Element.StopAnimation(Offset.Target);
				PropertySet.StopAnimation(Position.Target);
			}
			base.SetElement(el);
		}
		public override void ResetElement() {
			if(Element != null) {
				Element.StopAnimation(Size.Target);
				Element.StopAnimation(Offset.Target);
				PropertySet.StopAnimation(Position.Target);
			}
			base.ResetElement();
		}
		/// <summary>
		/// Start animating to the current position.
		/// </summary>
		public Vector2 UpdateOffset() {
			var vx = OffsetFor(Component1Axis, Component2Axis);
			Position.SetVector2Parameter(PARM_Position, vx);
			PropertySet.StartAnimation(Position.Target, Position);
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
			PropertySet.InsertVector2(PROP_Position, enter);
			Position.SetVector2Parameter(PARM_Position, enter);
			Element.StartAnimation(Offset.Target, Offset);
			Element.StartAnimation(Size.Target, Size);
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
			Position.SetVector2Parameter(PARM_Position, exit);
			PropertySet.StartAnimation(Position.Target, Position);
			ccb.End();
		}
	}
	#endregion
	#region ImageMarkerSeries
	public class ImageMarkerSeries : CategoryValue_VisualPerItem<ImageMarkerSeries_ItemState>, IRequireEnterLeave, IConsumer<Phase_Transforms> {
		internal static readonly LogTools.Flag _trace = LogTools.Add("ImageMarkerSeries", LogTools.Level.Error);
		#region properties
		public string MarkerImageUrl { get; set; }
		public double MarkerOffset { get; set; }
		public double MarkerWidth { get; set; }
		#endregion
		#region internal
		/// <summary>
		/// Call <see cref="IAnimationController.InitTransform(Matrix3x2)"/> exactly once.
		/// </summary>
		bool didInitTransform = false;
		bool didInitTransform2 = false;
		/// <summary>
		/// Shared reference to the marker brush.
		/// </summary>
		protected CompositionSurfaceBrush Marker { get; set; }
		/// <summary>
		/// This will be (0,0) until surface is loaded.
		/// </summary>
		protected Vector2 MarkerSize { get; set; }
		/// <summary>
		/// Tracks current projection matrix to trigger animations.
		/// </summary>
		protected Matrix3x2 LastProj { get; set; }
		CompositionPropertySet PropertySet { get; set; }
		Vector3KeyFrameAnimation ModelX { get; set; }
		Vector3KeyFrameAnimation ModelY { get; set; }
		Vector3KeyFrameAnimation ProjX { get; set; }
		Vector3KeyFrameAnimation ProjY { get; set; }
		#endregion
		#region helpers
		const string PROP_ModelX = "ModelX";
		const string PROP_ModelY = "ModelY";
		const string PROP_ProjX = "ProjX";
		const string PROP_ProjY = "ProjY";
		const string PROP_MarkerWidth = "MarkerWidth";
		const string PROP_AspectRatio = "AspectRatio";
		const string PARM_Index = "Index";
		const double DURATION = 300;
		protected void CreateAnimations(Compositor cx) {
			#region global property set
			PropertySet = cx.CreatePropertySet();
			PropertySet.Comment = $"{Name}_global";
			PropertySet.InsertVector3(PROP_ModelX, new Vector3(1, 0, 0));
			PropertySet.InsertVector3(PROP_ModelY, new Vector3(0, 1, 0));
			PropertySet.InsertVector3(PROP_ProjX, new Vector3(1, 0, 0));
			PropertySet.InsertVector3(PROP_ProjY, new Vector3(0, 1, 0));
			PropertySet.InsertScalar(PROP_MarkerWidth, (float)MarkerWidth);
			PropertySet.InsertScalar(PROP_AspectRatio, 1f);
			#endregion
			#region model-x
			ModelX = cx.CreateVector3KeyFrameAnimation();
			ModelX.Comment = $"{Name}_modelX";
			ModelX.InsertExpressionKeyFrame(1f, PARM_Index);
			ModelX.SetVector3Parameter(PARM_Index, new Vector3(1, 0, 0));
			ModelX.Duration = TimeSpan.FromMilliseconds(DURATION);
			ModelX.StopBehavior = AnimationStopBehavior.SetToFinalValue;
			ModelX.Target = PROP_ModelX;
			#endregion
			#region model-y
			ModelY = cx.CreateVector3KeyFrameAnimation();
			ModelY.Comment = $"{Name}_modelY";
			ModelY.InsertExpressionKeyFrame(1f, PARM_Index);
			ModelY.SetVector3Parameter(PARM_Index, new Vector3(0, 1, 0));
			ModelY.Duration = TimeSpan.FromMilliseconds(DURATION);
			ModelY.StopBehavior = AnimationStopBehavior.SetToFinalValue;
			ModelY.Target = PROP_ModelY;
			#endregion
			#region proj-x
			ProjX = cx.CreateVector3KeyFrameAnimation();
			ProjX.Comment = $"{Name}_projX";
			ProjX.InsertExpressionKeyFrame(1f, PARM_Index);
			ProjX.SetVector3Parameter(PARM_Index, new Vector3(1, 0, 0));
			ProjX.Duration = TimeSpan.FromMilliseconds(DURATION);
			ProjX.StopBehavior = AnimationStopBehavior.SetToFinalValue;
			ProjX.Target = PROP_ProjX;
			#endregion
			#region proj-y
			ProjY = cx.CreateVector3KeyFrameAnimation();
			ProjY.Comment = $"{Name}_projY";
			ProjY.InsertExpressionKeyFrame(1f, PARM_Index);
			ProjY.SetVector3Parameter(PARM_Index, new Vector3(0, 1, 0));
			ProjY.Duration = TimeSpan.FromMilliseconds(DURATION);
			ProjY.StopBehavior = AnimationStopBehavior.SetToFinalValue;
			ProjY.Target = PROP_ProjY;
			#endregion
		}
		protected void DisposeAnimations() {
			PropertySet?.Dispose(); PropertySet = null;
			ModelX?.Dispose(); ModelX = null;
			ModelY?.Dispose(); ModelY = null;
			ProjX?.Dispose(); ProjX = null;
			ProjY?.Dispose(); ProjY = null;
		}
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
			Model = Matrix3x2.Identity;
			int index = 0;
			foreach (var op in Pending.Where(xx => xx is ItemsWithOffset<ImageMarkerSeries_ItemState>)) {
				foreach (var item in (op as ItemsWithOffset<ImageMarkerSeries_ItemState>).Items) {
					UpdateLimits(index, item.DataValue, 0);
					index++;
				}
			}
			UpdateLimits(index);
		}
		protected override IElementFactoryContext CreateAnimateContext(ImageMarkerSeries_ItemState item, ItemTransition it) => new CategoryValueContext(Container.Compositor, item, CategoryAxis, ValueAxis, it, CategoryValueMode.Marker);
		protected override Visual CreateLegendVisual(Compositor cx) {
			var vis = cx.CreateSpriteVisual();
			EnsureMarker(cx);
			vis.Brush = Marker;
			vis.Size = new Vector2(24, 24);
			return vis;
		}
		protected void EnsureMarker(Compositor cx) {
			if (Marker != null) return;
			var isurf = LoadedImageSurface.StartLoadFromUri(new Uri(MarkerImageUrl));
			isurf.LoadCompleted += (sender, args) => {
				_trace.Verbose($"{Name}.image.LoadCompleted {args.Status} ds:{sender.DecodedSize} ns:{sender.NaturalSize}");
				if (args.Status == LoadedImageSourceLoadStatus.Success) {
					Size decodedSize = sender.DecodedSize;
					MarkerSize = new Vector2((float)decodedSize.Width, (float)decodedSize.Height);
					PropertySet.InsertScalar(PROP_AspectRatio, (float)decodedSize.Height / (float)decodedSize.Width);
				}
			};
			var brush = cx.CreateSurfaceBrush();
			brush.Surface = isurf;
			brush.Stretch = CompositionStretch.Uniform;
			Marker = brush;
		}
		protected override Visual CreateVisual(Compositor cx, ImageMarkerSeries_ItemState state) {
			EnsureMarker(cx);
			state.CreateAnimations(cx, PropertySet, CategoryAxis.Orientation, ValueAxis.Orientation);
			var (xx, yy) = MappingSupport.MapComponents(state.Component1, CategoryAxis.Orientation, state.Component2, ValueAxis.Orientation);
			var element = cx.CreateSpriteVisual();
			element.Brush = Marker;
			element.Comment = $"{Name}[{state.Index}]";
			element.AnchorPoint = new Vector2(0, .5f);
			_trace.Verbose($"{Name}[{state.Index}] create-shape val:{state.DataValue} pt:{xx:F2},{yy:F2}");
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
			if (model == Model) return;
			if (!didInitTransform) {
				//Animate?.InitTransform(model);
				PropertySet.InsertVector3(PROP_ModelX, new Vector3(model.M11, model.M21, model.M31));
				PropertySet.InsertVector3(PROP_ModelY, new Vector3(model.M12, model.M22, model.M32));
				didInitTransform = true;
			}
			Model = model;
			ModelX.SetVector3Parameter(PARM_Index, new Vector3(model.M11, model.M21, model.M31));
			ModelY.SetVector3Parameter(PARM_Index, new Vector3(model.M12, model.M22, model.M32));
			PropertySet.StartAnimation(ModelX.Target, ModelX);
			PropertySet.StartAnimation(ModelY.Target, ModelY);
			/*
			if (AnimationFactory != null) {
				var ctx = new DefaultContext(Container.Compositor, CategoryAxis, ValueAxis);
				Animate.Transform(ctx, Model);
			}
			else {
				foreach (ImageMarkerSeries_ItemState item in ItemState.Cast<ImageMarkerSeries_ItemState>().Where(xx => xx != null && xx.Element != null)) {
					// apply new model transform
					item.Element.TransformMatrix = new Matrix4x4(Model);
				}
			}*/
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
			if(!didInitTransform2) {
				PropertySet.InsertVector3(PROP_ProjX, new Vector3(proj.M11, proj.M21, proj.M31));
				PropertySet.InsertVector3(PROP_ProjY, new Vector3(proj.M12, proj.M22, proj.M32));
				didInitTransform2 = true;
			}
			if (proj == LastProj) return;
			LastProj = proj;
			ProjX.SetVector3Parameter(PARM_Index, new Vector3(proj.M11, proj.M21, proj.M31));
			ProjY.SetVector3Parameter(PARM_Index, new Vector3(proj.M12, proj.M22, proj.M32));
			PropertySet.StartAnimation(ProjX.Target, ProjX);
			PropertySet.StartAnimation(ProjY.Target, ProjY);
		}
		#endregion
		#region IRequireEnterLeave
		public void Enter(IChartEnterLeaveContext icelc) {
			EnsureAxes(icelc as IChartComponentContext);
			EnsureValuePath(icelc as IChartComponentContext);
			var icei = icelc as IChartErrorInfo;
			if (ElementFactory == null) {
				icei?.Report(new ChartValidationResult(NameOrType(), $"Property '{nameof(ElementFactory)}' was not set", new[] { nameof(ElementFactory) }));
			}
			Compositor compositor = Window.Current.Compositor;
			Container = compositor.CreateContainerVisual();
			Container.Comment = $"container_{Name}";
			Layer = icelc.CreateLayer(Container);
			Animate = AnimationFactory?.CreateAnimationController(compositor);
			CreateAnimations(compositor);
			_trace.Verbose($"{Name} enter v:{ValueAxisName} {ValueAxis} c:{CategoryAxisName} {CategoryAxis} d:{DataSourceName}");
		}
		public void Leave(IChartEnterLeaveContext icelc) {
			_trace.Verbose($"{Name} leave");
			DisposeAnimations();
			Animate?.Dispose();
			Animate = null;
			Container = null;
			icelc.DeleteLayer(Layer);
			Layer = null;
		}
		#endregion
	}
	#endregion
}
