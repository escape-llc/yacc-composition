using System.Collections.Generic;
using Windows.Foundation;
using Windows.UI.Composition;

namespace eScapeLLC.UWP.Charts.Composition {
	public abstract class Style_Brush {
		public abstract CompositionBrush CreateBrush(Compositor c);
	}
	public class Style_Stroke {
		public double StrokeThickness { get; set; } = double.NaN;
		public double StrokeMiterLimit { get; set; } = double.NaN;
		public CompositionStrokeCap StrokeStartCap { get; set; }
		public CompositionStrokeCap StrokeEndCap { get; set; }
		public CompositionStrokeLineJoin StrokeLineJoin { get; set; }
		//public double StrokeDashOffset { get; set; } = double.NaN;
		//public CompositionStrokeCap StrokeDashCap { get; set; }
		//public CompositionStrokeDashArray StrokeDashArray { get; set; }
		/// <summary>
		/// Force Stroke to be PX units regardless.
		/// </summary>
		public bool IsStrokeNonScaling { get; set; } = true;
		public virtual void Apply(CompositionSpriteShape sprite) {
			sprite.IsStrokeNonScaling = IsStrokeNonScaling;
			if (!double.IsNaN(StrokeThickness)) {
				sprite.StrokeThickness = (float)StrokeThickness;
			}
			if (!double.IsNaN(StrokeMiterLimit)) {
				sprite.StrokeMiterLimit = (float)StrokeMiterLimit;
			}
			sprite.StrokeLineJoin = StrokeLineJoin;
			sprite.StrokeStartCap = StrokeStartCap;
			sprite.StrokeEndCap = StrokeEndCap;
		}
	}
	public class Brush_Color : Style_Brush {
		public Windows.UI.Color Color { get; set; }
		public override CompositionBrush CreateBrush(Compositor c) => c.CreateColorBrush(Color);
	}
	public class Brush_GradientColorStop {
		public double Offset { get; set; }
		public Windows.UI.Color Color { get; set; }
	}
	public class ColorStopCollection : List<Brush_GradientColorStop> { }
	public abstract class Brush_Gradient : Style_Brush {
		public ColorStopCollection ColorStops { get; private set; } = new ColorStopCollection();
	}
	public class Brush_LinearGradient : Brush_Gradient {
		public Point StartPoint { get; set; } = new Point(-1, -1);
		public Point EndPoint { get; set; } = new Point(-1, -1);
		public double RotationAngleInDegrees { get; set; } = double.NaN;
		public override CompositionBrush CreateBrush(Compositor c) {
			var brush = c.CreateLinearGradientBrush();
			if(StartPoint.X >= 0 && StartPoint.Y >= 0) {
				brush.StartPoint = new System.Numerics.Vector2((float)StartPoint.X, (float)StartPoint.Y);
			}
			if (EndPoint.X >= 0 && EndPoint.Y >= 0) {
				brush.EndPoint = new System.Numerics.Vector2((float)EndPoint.X, (float)EndPoint.Y);
			}
			if(!double.IsNaN(RotationAngleInDegrees)) {
				brush.RotationAngleInDegrees = (float)RotationAngleInDegrees;
			}
			foreach (var cs in ColorStops) {
				var stop = c.CreateColorGradientStop();
				stop.Offset = (float)cs.Offset;
				stop.Color = cs.Color;
				brush.ColorStops.Add(stop);
			}
			return brush;
		}
	}
	public class Brush_RadialGradient : Brush_Gradient {
		public override CompositionBrush CreateBrush(Compositor c) {
			var brush = c.CreateRadialGradientBrush();
			foreach(var cs in ColorStops) {
				var stop = c.CreateColorGradientStop();
				stop.Offset = (float)cs.Offset;
				stop.Color = cs.Color;
				brush.ColorStops.Add(stop);
			}
			return brush;
		}
	}
}
