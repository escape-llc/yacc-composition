﻿using eScape.Core;
using eScape.Host;
using eScapeLLC.UWP.Charts.Composition.Events;
using System;
using System.Linq;
using System.Numerics;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace eScapeLLC.UWP.Charts.Composition {
	/// <summary>
	/// Item state.
	/// </summary>
	public class ImageMarkerSeries_ItemState : ItemState_CategoryValue<Visual> {
		CompositionPropertySet PropertySet { get; set; }
		Vector2KeyFrameAnimation Position { get; set; }
		ExpressionAnimation Offset { get; set; }
		ExpressionAnimation Size { get; set; }
		public ImageMarkerSeries_ItemState(int index, double categoryOffset, double value) : base(index, categoryOffset, value) { }
		public void CreateAnimations(Compositor cx, CompositionPropertySet props) {
			var vx = MappingSupport.ToVector(Component1, AxisOrientation.Horizontal, Component2, AxisOrientation.Vertical);
			Position = cx.CreateVector2KeyFrameAnimation();
			Position.Comment = $"Marker[{Index}]_Position";
			Position.InsertExpressionKeyFrame(1f, "Position");
			Position.SetVector2Parameter("Position", vx);
			Position.Target = "Position";
			PropertySet = cx.CreatePropertySet();
			PropertySet.Comment = $"Marker[{Index}]_PropertySet";
			PropertySet.InsertVector2("Position", vx);
			const string XAXIS = "(local.Position.X * global.ModelX.X + global.ModelX.Z) * global.ProjX.X + global.ProjX.Z";
			const string YAXIS = "(local.Position.Y * global.ModelY.Y + global.ModelY.Z) * global.ProjY.Y + global.ProjY.Z";
			Offset = cx.CreateExpressionAnimation($"Vector3({XAXIS}, {YAXIS}, 0)");
			Offset.Comment = $"Marker[{Index}]_Offset";
			Offset.SetReferenceParameter("local", PropertySet);
			Offset.SetReferenceParameter("global", props);
			Offset.Target = nameof(Visual.Offset);
			const string WIDTH = "global.MarkerWidth*global.ModelX.X*global.ProjX.X";
			Size = cx.CreateExpressionAnimation($"Vector2({WIDTH}, ({WIDTH})*global.AspectRatio)");
			Size.Comment = $"Marker[{Index}]_Size";
			Size.SetReferenceParameter("local", PropertySet);
			Size.SetReferenceParameter("global", props);
			Size.Target = nameof(Visual.Size);
		}
		public override Vector2 OffsetFor(AxisOrientation cori, AxisOrientation vori) {
			return MappingSupport.OffsetFor(Component1, cori, Component2, vori);
		}
		public override void SetElement(Visual el) {
			if(Element != null) {
				Element.StopAnimation(Size.Target);
				Element.StopAnimation(Offset.Target);
			}
			base.SetElement(el);
			if(Element != null) {
				Element.StartAnimation(Offset.Target, Offset);
				Element.StartAnimation(Size.Target, Size);
			}
		}
		public override void ResetElement() {
			if(Element != null) {
				Element.StopAnimation(Size.Target);
				Element.StopAnimation(Offset.Target);
			}
			base.ResetElement();
		}
		/// <summary>
		/// Start animating to the current position.
		/// </summary>
		public Vector2 AnimatePosition() {
			var vx = MappingSupport.ToVector(Component1, AxisOrientation.Horizontal, Component2, AxisOrientation.Vertical);
			Position.SetVector2Parameter("Position", vx);
			PropertySet.StartAnimation(Position.Target, Position);
			return vx;
		}
		/// <summary>
		/// Animate from given location to current position.
		/// </summary>
		/// <param name="enter">Initial position.</param>
		public void AnimateFrom(Vector2 enter) {
			// start at entry point
			PropertySet.InsertVector2("Position", enter);
			// target is current position
			var vx = MappingSupport.ToVector(Component1, AxisOrientation.Horizontal, Component2, AxisOrientation.Vertical);
			Position.SetVector2Parameter("Position", vx);
			PropertySet.StartAnimation(Position.Target, Position);
		}
		/// <summary>
		/// Animate from current position to given position.
		/// </summary>
		/// <param name="exit">Exit position.</param>
		public void AnimateTo(Vector2 exit) {
			Position.SetVector2Parameter("Position", exit);
			PropertySet.StartAnimation(Position.Target, Position);
		}
	}
	public class ImageMarkerSeries : CategoryValue_VisualPerItem<ImageMarkerSeries_ItemState>, IRequireEnterLeave, IConsumer<Phase_Transforms> {
		static readonly LogTools.Flag _trace = LogTools.Add("ImageMarkerSeries", LogTools.Level.Error);
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
		/// <summary>
		/// Shared reference to the marker brush.
		/// </summary>
		protected CompositionSurfaceBrush Marker { get; set; }
		/// <summary>
		/// This will be (0,0) until surface is loaded.
		/// </summary>
		protected Vector2 MarkerSize { get; set; }
		#endregion
		#region helpers
		CompositionPropertySet PropertySet { get; set; }
		Vector3KeyFrameAnimation ModelX { get; set; }
		Vector3KeyFrameAnimation ModelY { get; set; }
		Vector3KeyFrameAnimation ProjX { get; set; }
		Vector3KeyFrameAnimation ProjY { get; set; }
		protected void CreateAnimations(Compositor cx) {
			PropertySet = cx.CreatePropertySet();
			PropertySet.Comment = $"{Name}_global";
			PropertySet.InsertVector3("ModelX", new Vector3(1, 0, 0));
			PropertySet.InsertVector3("ModelY", new Vector3(0, 1, 0));
			PropertySet.InsertVector3("ProjX", new Vector3(1, 0, 0));
			PropertySet.InsertVector3("ProjY", new Vector3(0, 1, 0));
			PropertySet.InsertScalar("MarkerWidth", (float)MarkerWidth);
			PropertySet.InsertScalar("AspectRatio", 1f);
			ModelX = cx.CreateVector3KeyFrameAnimation();
			ModelX.Comment = $"{Name}_modelX";
			ModelX.InsertExpressionKeyFrame(1f, "Index");
			ModelX.SetVector3Parameter("Index", new Vector3(1, 0, 0));
			ModelX.Duration = TimeSpan.FromMilliseconds(300);
			ModelX.Target = "ModelX";
			ModelY = cx.CreateVector3KeyFrameAnimation();
			ModelY.Comment = $"{Name}_modelY";
			ModelY.InsertExpressionKeyFrame(1f, "Index");
			ModelY.SetVector3Parameter("Index", new Vector3(0, 1, 0));
			ModelY.Duration = TimeSpan.FromMilliseconds(300);
			ModelY.Target = "ModelY";
			ProjX = cx.CreateVector3KeyFrameAnimation();
			ProjX.Comment = $"{Name}_projX";
			ProjX.InsertExpressionKeyFrame(1f, "Index");
			ProjX.SetVector3Parameter("Index", new Vector3(1, 0, 0));
			ProjX.Duration = TimeSpan.FromMilliseconds(300);
			ProjX.Target = "ProjX";
			ProjY = cx.CreateVector3KeyFrameAnimation();
			ProjY.Comment = $"{Name}_projY";
			ProjY.InsertExpressionKeyFrame(1f, "Index");
			ProjY.SetVector3Parameter("Index", new Vector3(0, 1, 0));
			ProjY.Duration = TimeSpan.FromMilliseconds(300);
			ProjY.Target = "ProjY";
		}
		protected void DisposeAnimations() {
			PropertySet.Dispose();
			PropertySet = null;
			ModelX.Dispose();
			ModelX = null;
			ModelY.Dispose();
			ModelY = null;
			ProjX.Dispose();
			ProjX = null;
			ProjY.Dispose();
			ProjY = null;
		}
		#endregion
		#region extensions
		protected override void Entering(ImageMarkerSeries_ItemState item, ItemTransition it) {
			if (item == null || item.Element == null) return;
			if (it == ItemTransition.Head) {
				Container.Children.InsertAtTop(item.Element);
			}
			else {
				Container.Children.InsertAtBottom(item.Element);
			}
			item.AnimateFrom(Spawn(item, it));
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
		protected override void Exiting(ImageMarkerSeries_ItemState item, ItemTransition it) {
			if (item == null || item.Element == null) return;
			//item.AnimateTo(Spawn(item, it));
			Container.Children.Remove(item.Element);
			item.ResetElement();
		}
		protected override void UpdateOffset(ImageMarkerSeries_ItemState item) {
			if (item.Element == null) return;
			var offset = item.AnimatePosition();
			_trace.Verbose($"{Name}[{item.Index}] update-offset val:{item.DataValue} from:{item.Element.Offset.X},{item.Element.Offset.Y} to:{offset.X},{offset.Y}");
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
					// TODO need this to be the calculated size
					MarkerSize = new Vector2((float)decodedSize.Width, (float)decodedSize.Height);
					PropertySet.InsertScalar("AspectRatio", (float)decodedSize.Height / (float)decodedSize.Width);
					// ensure all the current items get the size
					foreach (ImageMarkerSeries_ItemState state in ItemState) {
						if(state != null && state.Element != null) {
							//state.SetMarkerSize((float)decodedSize.Width, (float)decodedSize.Height);
						}
					}
				}
			};
			var brush = cx.CreateSurfaceBrush();
			brush.Surface = isurf;
			brush.Stretch = CompositionStretch.Uniform;
			Marker = brush;
		}
		protected override Visual CreateVisual(Compositor cx, ImageMarkerSeries_ItemState state) {
			EnsureMarker(cx);
			state.CreateAnimations(cx, PropertySet);
			var (xx, yy) = MappingSupport.MapComponents(state.Component1, CategoryAxis.Orientation, state.Component2, ValueAxis.Orientation);
			var element = cx.CreateSpriteVisual();
			element.Brush = Marker;
			element.Comment = $"{Name}[{state.Index}]";
			//element.Size = MarkerSize;
			//element.Offset = new Vector3((float)xx, (float)yy, 0);
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
				didInitTransform = true;
			}
			Model = model;
			ModelX.SetVector3Parameter("Index", new Vector3(model.M11, model.M21, model.M31));
			ModelY.SetVector3Parameter("Index", new Vector3(model.M12, model.M22, model.M32));
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
			// detect change before animating
			if (proj == LastProj) return;
			LastProj = proj;
			ProjX.SetVector3Parameter("Index", new Vector3(proj.M11, proj.M21, proj.M31));
			ProjY.SetVector3Parameter("Index", new Vector3(proj.M12, proj.M22, proj.M32));
			PropertySet.StartAnimation(ProjX.Target, ProjX);
			PropertySet.StartAnimation(ProjY.Target, ProjY);
		}
		Matrix3x2 LastProj { get; set; }
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
}
