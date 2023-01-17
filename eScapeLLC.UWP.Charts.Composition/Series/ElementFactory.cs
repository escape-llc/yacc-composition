using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Composition;

namespace eScapeLLC.UWP.Composition.Charts {
	/// <summary>
	/// Ability to create elements for column series types.
	/// </summary>
	public interface IColumnElementFactory {
		/// <summary>
		/// Create the sprite.  Treats as a rectangle.
		/// </summary>
		/// <param name="c">Use to create elements.</param>
		/// <param name="width">Column width.  Always refers to the x-axis.</param>
		/// <param name="height">Column height.  Always refers to the y-axis.</param>
		/// <param name="caxis">Orientation of category axis.</param>
		/// <param name="vaxis">Orientation of value axis.</param>
		/// <returns></returns>
		CompositionSpriteShape CreateElement(Compositor c, double width, double height, AxisOrientation caxis, AxisOrientation vaxis);
	}
	/// <summary>
	/// Default implementation for creating rounded rectangle sprites.
	/// </summary>
	public class ColumnElementFactory_Default : IColumnElementFactory {
		#region properties
		public Style_Brush FillBrush { get; set; }
		public Style_Brush StrokeBrush { get; set; }
		public double StrokeThickness { get; set; } = double.NaN;
		public double StrokeMiterLimit { get; set; } = double.NaN;
		public CompositionStrokeCap StrokeStartCap { get; set; }
		public CompositionStrokeCap StrokeEndCap { get; set; }
		public CompositionStrokeLineJoin StrokeLineJoin { get; set; }
		public double StrokeDashOffset { get; set; } = double.NaN;
		public CompositionStrokeCap StrokeDashCap { get; set; }
		//public CompositionStrokeDashArray StrokeDashArray { get; set; }
		/// <summary>
		/// Force Stroke to be PX units regardless.
		/// </summary>
		public bool IsStrokeNonScaling { get; set; } = true;
		/// <summary>
		/// Corner radius is in NDC units (output side of transform).
		/// </summary>
		public double CornerRadiusX { get; set; } = double.NaN;
		/// <summary>
		/// Corner radius is in NDC units (output side of transform).
		/// </summary>
		public double CornerRadiusY { get; set; } = double.NaN;
		#endregion
		public ColumnElementFactory_Default() { }
		/// <summary>
		/// <inheritdoc/>
		/// </summary>
		/// <param name="c"></param>
		/// <param name="width"></param>
		/// <param name="height"></param>
		/// <param name="c1axis"></param>
		/// <param name="c2axis"></param>
		/// <returns></returns>
		public CompositionSpriteShape CreateElement(Compositor c, double width, double height, AxisOrientation c1axis, AxisOrientation c2axis) {
			var rectangle = c.CreateRoundedRectangleGeometry();
			var sprite = c.CreateSpriteShape(rectangle);
			sprite.IsStrokeNonScaling = IsStrokeNonScaling;
			if(!double.IsNaN(CornerRadiusX) && !double.IsNaN(CornerRadiusY))	{
				rectangle.CornerRadius = new Vector2((float)CornerRadiusX, (float)CornerRadiusY);
			}
			if (!double.IsNaN(StrokeThickness)) {
				sprite.StrokeThickness = (float)StrokeThickness;
			}
			if (!double.IsNaN(StrokeMiterLimit)) {
				sprite.StrokeMiterLimit = (float)StrokeMiterLimit;
			}
			sprite.StrokeStartCap = StrokeStartCap;
			sprite.StrokeEndCap = StrokeEndCap;
			if (FillBrush != null) {
				sprite.FillBrush = FillBrush.CreateBrush(c);
			}
			if (StrokeBrush != null) {
				sprite.StrokeBrush = StrokeBrush.CreateBrush(c);
			}
			// Offset and Size are Model units (input side of transform)
			rectangle.Size = new Vector2((float)Math.Abs(width), (float)Math.Abs(height));
			// Offset and Transform are managed by the caller
			return sprite;
		}
	}
}
