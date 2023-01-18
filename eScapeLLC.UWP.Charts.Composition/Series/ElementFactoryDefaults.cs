using System.Numerics;
using Windows.UI.Composition;

namespace eScapeLLC.UWP.Charts.Composition {
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
			clgb.StartPoint = new Vector2((float)blg.EndPoint.X, (float)blg.EndPoint.Y);
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
			var iecvc = iefc as IElementCategoryValueContext;
			if (FillBrush != null) {
				sprite.FillBrush = FillBrush.CreateBrush(iefc.Compositor);
				if (iecvc.Value < 0 && FillBrush is Brush_LinearGradient blg && FlipGradients && sprite.FillBrush is CompositionLinearGradientBrush clgb) {
					FlipGradient(blg, clgb);
				}
			}
			if (StrokeBrush != null) {
				sprite.StrokeBrush = StrokeBrush.CreateBrush(iefc.Compositor);
				if (iecvc.Value < 0 && StrokeBrush is Brush_LinearGradient blg && FlipGradients && sprite.StrokeBrush is CompositionLinearGradientBrush clgb) {
					FlipGradient(blg, clgb);
				}
			}
			if (!double.IsNaN(CornerRadiusX) && !double.IsNaN(CornerRadiusY)) {
				rectangle.CornerRadius = new Vector2((float)CornerRadiusX, (float)CornerRadiusY);
			}
			if (iefc is IElementRectangleContext ierc) {
				// Offset and Size are Model units (input side of transform)
				rectangle.Size = new Vector2((float)ierc.Width, (float)ierc.Height);
			}
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
