using System;
using System.ComponentModel;
using System.Numerics;
using Windows.UI.Composition;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace eScapeLLC.UWP.Charts.Composition {
	#region AnimationFactory_Default
	public class AnimationFactory_Default : IAnimationFactory {
		Vector2KeyFrameAnimation aoffset;
		ExpressionAnimation xform;
		ImplicitAnimationCollection iac;
		Vector2KeyFrameAnimation enter;
		Vector2KeyFrameAnimation exit;
		public void Prepare(Compositor cc) {
			Vector2KeyFrameAnimation aoffset = cc.CreateVector2KeyFrameAnimation();
			aoffset.InsertExpressionKeyFrame(1f, "Index");
			aoffset.Duration = TimeSpan.FromMilliseconds(2000);
			aoffset.StopBehavior = AnimationStopBehavior.SetToFinalValue;
			aoffset.Target = nameof(CompositionShape.Offset);
			aoffset.Comment = "Shift";
			this.aoffset = aoffset;
			var enter = cc.CreateVector2KeyFrameAnimation();
			enter.InsertExpressionKeyFrame(1f, "this.FinalValue");
			enter.Duration = TimeSpan.FromMilliseconds(1500);
			enter.Target = nameof(CompositionShape.Offset);
			enter.Comment = "Enter";
			this.enter = enter;
			var exit = cc.CreateVector2KeyFrameAnimation();
			exit.InsertExpressionKeyFrame(1f, "Index");
			exit.Duration = TimeSpan.FromMilliseconds(1500);
			exit.Target = nameof(CompositionShape.Offset);
			exit.Comment = "Exit";
			this.exit = exit;
			ExpressionAnimation xxx = cc.CreateExpressionAnimation();
			xxx.Properties.InsertVector3("Component1", new Vector3(1, 0, 0));
			xxx.Properties.InsertVector3("Component2", new Vector3(0, 1, 0));
			xxx.Expression = "Matrix3x2(props.Component1.X,props.Component2.X,props.Component1.Y,props.Component2.Y,props.Component1.Z,props.Component2.Z)";
			xxx.SetExpressionReferenceParameter("props", xxx.Properties);
			xxx.Target = nameof(CompositionShape.TransformMatrix);
			xxx.Comment = "TransformMatrix";
			this.xform = xxx;
			var iac = cc.CreateImplicitAnimationCollection();
			iac[nameof(CompositionShape.Offset)] = aoffset;
			iac[nameof(CompositionShape.TransformMatrix)] = xform;
			iac.Comment = "Implicit";
			this.iac = iac;
		}
		public void Unprepare(Compositor cc) {
			xform?.Dispose(); xform = null;
			aoffset?.Dispose(); aoffset = null;
			exit?.Dispose(); exit = null;
			enter?.Dispose(); enter = null;
			iac?.Dispose(); iac = null;
		}
		public ImplicitAnimationCollection CreateImplcit(IElementFactoryContext iefc) {
			return iac;
		}
		public void StartAnimation(string key, IElementFactoryContext iefc, CompositionObject co, Action<CompositionAnimation> cfg = null) {
			switch (key) {
				case "Enter":
					if (co is CompositionShape cs && iefc is IElementCategoryValueContext ieec) {
						// calculate the spawn point
						var (xx, yy) = MappingSupport.MapComponents(
							ieec.Item.CategoryValue + ieec.Item.CategoryOffset + /*ieec.CategoryAxis.Maximum*/2, ieec.CategoryAxis.Orientation,
							Math.Min(ieec.Item.DataValue, 0), ieec.ValueAxis.Orientation);
						cs.Offset = new Vector2((float)xx, (float)yy);
						// callback MUST add to VT
						cfg?.Invoke(enter);
						//co.StartAnimation(enter.Target, enter);
					}
					break;
				case "Exit":
					if (co is CompositionShape cs2 && iefc is IElementCategoryValueContext ieec2) {
						CompositionScopedBatch ccb = iefc.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
						ccb.Completed += (sender, cbcea) => {
							// callback SHOULD remove from VT
							cfg?.Invoke(exit);
						};
						// calculate the exit point
						var (xx, yy) = MappingSupport.MapComponents(
							-2, ieec2.CategoryAxis.Orientation,
							Math.Min(ieec2.Item.DataValue, 0), ieec2.ValueAxis.Orientation);
						var vxx = new Vector2((float)xx, (float)yy);
						exit.SetVector2Parameter("Index", vxx);
						cs2.StartAnimation(exit.Target, exit);
						ccb.End();
					}
					break;
				case "Transform":
					cfg?.Invoke(xform);
					co.StartAnimation(xform.Target, xform);
					break;
				case "Offset":
					if (iefc is IElementCategoryValueContext ieec3) {
						var (xx, yy) = MappingSupport.MapComponents(
							ieec3.Item.CategoryValue + ieec3.Item.CategoryOffset, ieec3.CategoryAxis.Orientation,
							Math.Min(ieec3.Item.DataValue, 0), ieec3.ValueAxis.Orientation);
						var vxx = new Vector2((float)xx, (float)yy);
						aoffset.SetVector2Parameter("Index", vxx);
						co.StartAnimation(aoffset.Target, aoffset);
					}
					break;
			}
		}
	}
	#endregion
	#region ColumnElementFactory_Default
	/// <summary>
	/// Default factory for creating rounded rectangle sprites.
	/// </summary>
	public class ColumnElementFactory_Default : IElementFactory {
		#region properties
		public Style_Brush FillBrush { get; set; }
		public Style_Brush StrokeBrush { get; set; }
		public Style_Stroke Stroke { get; set; }
		/// <summary>
		/// Corner radius is in NDC units (output side of transform).
		/// </summary>
		public double CornerRadiusX { get; set; } = double.NaN;
		/// <summary>
		/// Corner radius is in NDC units (output side of transform).
		/// </summary>
		public double CornerRadiusY { get; set; } = double.NaN;
		/// <summary>
		/// Whether to flip linear gradients for negative value bars.
		/// Use this when the gradient orients along the bar's axis, and it will "mirror" the gradients around the value axis zero.
		/// </summary>
		public bool FlipGradients { get; set; }
		#endregion
		public ColumnElementFactory_Default() { }
		protected void FlipGradient(Brush_LinearGradient blg, CompositionLinearGradientBrush clgb) {
			var flip = clgb.StartPoint;
			clgb.StartPoint = clgb.EndPoint;
			clgb.EndPoint = flip;
		}
		/// <summary>
		/// <inheritdoc/>
		/// Flips the gradient direction for negative bars.
		/// </summary>
		/// <param name="c"></param>
		/// <param name="width"></param>
		/// <param name="height"></param>
		/// <param name="c1axis"></param>
		/// <param name="c2axis"></param>
		/// <returns></returns>
		public CompositionShape CreateElement(IElementFactoryContext iefc) {
			var rectangle = iefc.Compositor.CreateRoundedRectangleGeometry();
			var sprite = iefc.Compositor.CreateSpriteShape(rectangle);
			Stroke?.Apply(sprite);
			var ierc = iefc as IElementRectangleContext;
			if (FillBrush != null) {
				sprite.FillBrush = FillBrush.CreateBrush(iefc.Compositor);
				if (ierc.DataValue < 0 && FillBrush is Brush_LinearGradient blg && FlipGradients && sprite.FillBrush is CompositionLinearGradientBrush clgb) {
					FlipGradient(blg, clgb);
				}
			}
			if (StrokeBrush != null) {
				sprite.StrokeBrush = StrokeBrush.CreateBrush(iefc.Compositor);
				if (ierc.DataValue < 0 && StrokeBrush is Brush_LinearGradient blg && FlipGradients && sprite.StrokeBrush is CompositionLinearGradientBrush clgb) {
					FlipGradient(blg, clgb);
				}
			}
			if (!double.IsNaN(CornerRadiusX) && !double.IsNaN(CornerRadiusY)) {
				rectangle.CornerRadius = new Vector2((float)CornerRadiusX, (float)CornerRadiusY);
			}
			// Offset and Size are Model units (input side of transform)
			rectangle.Size = new Vector2((float)ierc.Width, (float)ierc.Height);
			// Offset and Transform are managed by the caller
			return sprite;
		}
	}
	#endregion
	#region LineElementFactory_Default
	/// <summary>
	/// Default factor for <see cref="CompositionPath"/> sprites.
	/// </summary>
	public class LineElementFactory_Default : IElementFactory {
		public Style_Brush StrokeBrush { get; set; }
		public Style_Stroke Stroke { get; set; }
		public CompositionShape CreateElement(IElementFactoryContext iefc) {
			var pathgeom = iefc.Compositor.CreatePathGeometry((iefc as IElementCompositionPath).Path);
			var sprite = iefc.Compositor.CreateSpriteShape(pathgeom);
			Stroke?.Apply(sprite);
			if (StrokeBrush != null) {
				sprite.StrokeBrush = StrokeBrush.CreateBrush(iefc.Compositor);
			}
			return sprite;
		}
	}
	#endregion
}
