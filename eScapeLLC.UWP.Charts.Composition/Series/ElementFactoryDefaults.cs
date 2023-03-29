using eScape.Core;
using System;
using System.Numerics;
using Windows.Foundation.Metadata;
using Windows.UI.Composition;
using Windows.UI.Xaml.Media;

namespace eScapeLLC.UWP.Charts.Composition.Factory {
	#region StrokeGeometry
	/// <summary>
	/// Common base for stroked geometry.
	/// </summary>
	public abstract class StrokeGeometry {
		#region properties
		public Style_Brush StrokeBrush { get; set; }
		public Style_Stroke Stroke { get; set; }
		#endregion
		#region public
		public void ApplyStyle(CompositionSpriteShape sprite) {
			Stroke?.Apply(sprite);
			if (StrokeBrush != null) {
				sprite.StrokeBrush = StrokeBrush.CreateBrush(sprite.Compositor);
				sprite.FillBrush = sprite.StrokeBrush;
			}
		}
		#endregion
	}
	#endregion
	#region FillAndStrokeGeometry
	/// <summary>
	/// Common base for filled and stroked geometry.
	/// </summary>
	public abstract class FillAndStrokeGeometry {
		#region properties
		public Style_Brush FillBrush { get; set; }
		public Style_Brush StrokeBrush { get; set; }
		public Style_Stroke Stroke { get; set; }
		/// <summary>
		/// Whether to flip linear gradients for negative value bars.
		/// Use this when the gradient orients along the bar's axis, and it will "mirror" the gradients around the value axis zero.
		/// </summary>
		public bool FlipGradients { get; set; }
		#endregion
		#region helpers
		/// <summary>
		/// Swap the start and end points of gradient brush.
		/// </summary>
		/// <param name="blg"></param>
		/// <param name="clgb"></param>
		protected void FlipGradient(Brush_LinearGradient blg, CompositionLinearGradientBrush clgb) {
			var flip = clgb.StartPoint;
			clgb.StartPoint = clgb.EndPoint;
			clgb.EndPoint = flip;
		}
		#endregion
		#region public
		public void ApplyStyle(CompositionSpriteShape sprite) {
			Stroke?.Apply(sprite);
			if (FillBrush != null) {
				sprite.FillBrush = FillBrush.CreateBrush(sprite.Compositor);
			}
			if (StrokeBrush != null) {
				sprite.StrokeBrush = StrokeBrush.CreateBrush(sprite.Compositor);
			}
		}
		#endregion
	}
	#endregion
	#region RectangleGeometryFactory
	/// <summary>
	/// Default factory for creating rounded rectangle sprites.
	/// </summary>
	[Deprecated("Do Not Use Compositor.CreateRectangleGeometry", DeprecationType.Remove, 0)]
	public class RectangleGeometryFactory : FillAndStrokeGeometry, IElementFactory {
		public RectangleGeometryFactory() { }
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
			var rectangle = iefc.Compositor.CreateRectangleGeometry();
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
			// Offset and Size are Model units (input side of transform)
			rectangle.Size = new Vector2((float)ierc.Width, (float)ierc.Height);
			// Offset and Transform are managed by the caller
			return sprite;
		}
	}
	#endregion
	#region RoundedRectangleGeometryFactory
	/// <summary>
	/// Default factory for creating rounded rectangle sprites.
	/// </summary>
	public class RoundedRectangleGeometryFactory : FillAndStrokeGeometry, IElementFactory {
		#region properties
		/// <summary>
		/// Corner radius is in NDC units (output side of transform).
		/// </summary>
		public double CornerRadiusX { get; set; } = double.NaN;
		/// <summary>
		/// Corner radius is in NDC units (output side of transform).
		/// </summary>
		public double CornerRadiusY { get; set; } = double.NaN;
		#endregion
		public RoundedRectangleGeometryFactory() { }
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
	#region LineGeometryFactory
	/// <summary>
	/// Use for <see cref="ValueRule"/> etc.
	/// Creates normalized geometry; owner MUST manage the <see cref="CompositionShape.Offset"/> to position correctly.
	/// </summary>
	public class LineGeometryFactory : StrokeGeometry, IElementFactory {
		public CompositionShape CreateElement(IElementFactoryContext iefc) {
			var line = iefc.Compositor.CreateLineGeometry();
			if(iefc is IElementLineContext ielc) {
				line.Start = ielc.Start;
				line.End = ielc.End;
			}
			else {
				line.Start = new Vector2(0, 0);
				line.End = new Vector2(1, 0);
			}
			var sprite = iefc.Compositor.CreateSpriteShape(line);
			Stroke?.Apply(sprite);
			if (StrokeBrush != null) {
				sprite.StrokeBrush = StrokeBrush.CreateBrush(iefc.Compositor);
			}
			return sprite;
		}
	}
	#endregion
	#region PathGeometryFactory
	/// <summary>
	/// Default factor for <see cref="CompositionPath"/> sprites.
	/// </summary>
	public class PathGeometryFactory : StrokeGeometry, IElementFactory {
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
