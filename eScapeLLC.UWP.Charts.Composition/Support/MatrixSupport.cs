using System;
using System.Numerics;
using Windows.Foundation;
using Windows.UI.Xaml.Media;

namespace eScapeLLC.UWP.Composition.Charts {
	public static class MatrixSupport {
		/// <summary>
		/// Return the Quadrant for drawing the data area.
		/// </summary>
		/// <param name="l2r">true: left-to-right.</param>
		/// <param name="b2t">true: bottom-to-top.</param>
		/// <returns></returns>
		public static int QuadrantFor(bool l2r, bool b2t) {
			if (l2r) {
				return b2t ? 1 : 4;
			}
			else {
				return b2t ? 2 : 3;
			}
		}
		public static Matrix3x2 From(Matrix mx) {
			return new Matrix3x2((float)mx.M11, (float)mx.M12, (float)mx.M21, (float)mx.M22, (float)mx.OffsetX, (float)mx.OffsetY);
		}
		/// <summary>
		/// Create matrix in(NDC) out(DC).
		/// </summary>
		/// <param name="xorigin">x-origin.</param>
		/// <param name="yorigin">y-origin.</param>
		/// <param name="width">x-range. negate to reverse direction.</param>
		/// <param name="height">y-range. negate to reverse direction.</param>
		/// <returns></returns>
		public static Matrix3x2 ProjectionFor(double xorigin, double yorigin, double width, double height) {
			return new Matrix3x2((float)width, 0, 0, (float)height, (float)xorigin, (float)yorigin);
		}
		/// <summary>
		/// Project up-and-right from Bottom Left corner (Quadrant I).
		/// in(NDC) out(+DC,-DC)
		/// </summary>
		/// <param name="rect">Source rectangle.</param>
		/// <returns>New instance.</returns>
		public static Matrix3x2 ProjectQuadrant1(Rect rect) {
			return ProjectionFor(rect.Left, rect.Bottom, rect.Width, -rect.Height);
		}
		/// <summary>
		/// Project up-and-left from Bottom Right corner (Quadrant II).
		/// in(NDC) out(-DC,-DC)
		/// </summary>
		/// <param name="rect">Source rectangle.</param>
		/// <returns>New instance.</returns>
		public static Matrix3x2 ProjectQuadrant2(Rect rect) {
			return ProjectionFor(rect.Right, rect.Bottom, -rect.Width, -rect.Height);
		}
		/// <summary>
		/// Project down-and-left from Top Right corner (Quadrant III).
		/// in(NDC) out(-DC,+DC)
		/// </summary>
		/// <param name="rect">Source rectangle.</param>
		/// <returns>New instance.</returns>
		public static Matrix3x2 ProjectQuadrant3(Rect rect) {
			return ProjectionFor(rect.Right, rect.Top, -rect.Width, rect.Height);
		}
		/// <summary>
		/// Project down-and-right from Top Left corner (Quadrant IV).
		/// in(NDC) out(+DC,+DC)
		/// </summary>
		/// <param name="rect">Source rectangle.</param>
		/// <returns>New instance.</returns>
		public static Matrix3x2 ProjectQuadrant4(Rect rect) {
			return ProjectionFor(rect.Left, rect.Top, rect.Width, rect.Height);
		}
		/// <summary>
		/// Map integer quadrant to matching ProjectQuadrantX call.
		/// </summary>
		/// <param name="quad">Quadrant: 1..4</param>
		/// <param name="rect">Area to map.</param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		public static Matrix3x2 ProjectForQuadrant(int quad, Rect rect) {
			switch(quad) {
				case 1:
					return ProjectQuadrant1(rect);
				case 2:
					return ProjectQuadrant2(rect);
				case 3:
					return ProjectQuadrant3(rect);
				case 4:
					return ProjectQuadrant4(rect);
				default:
					throw new InvalidOperationException($"Invalid quadrant: {quad}");
			}
		}
		/// <summary>
		/// Project world coordinates to NDC.
		/// in(WC) out(NDC)
		/// </summary>
		/// <param name="a1min">Axis-1 Minimum.</param>
		/// <param name="a1max">Axis-1 Maximum.</param>
		/// <param name="a2min">Axis-2 Minimum.</param>
		/// <param name="a2max">Axis-2 Maximum.</param>
		/// <returns>New instance.</returns>
		public static Matrix3x2 ModelFor(double a1min, double a1max, double a2min, double a2max) {
			var a1r = a1max - a1min;
			var a2r = a2max - a2min;
			return new Matrix3x2((float)(1.0 / a1r), 0, 0, (float)(1.0 / a2r), (float)(-a1min / a1r), (float)(-a2min / a2r));
		}
		/// <summary>
		/// Horizontal axis mapping: Bottom.
		/// x(WC) y(NDC)
		/// </summary>
		/// <param name="axisrect"></param>
		/// <param name="min"></param>
		/// <param name="max"></param>
		/// <param name="ltr"></param>
		/// <returns></returns>
		public static (Matrix3x2 model, Matrix3x2 proj) AxisBottom(Rect axisrect, double min, double max, bool ltr = true) {
			return (ModelFor(min, max, 0, 1), ProjectForQuadrant(ltr ? 4 : 3, axisrect));
		}
		/// <summary>
		/// Horizontal axis mapping: Top.
		/// x(WC) y(NDC)
		/// </summary>
		/// <param name="axisrect"></param>
		/// <param name="min"></param>
		/// <param name="max"></param>
		/// <param name="ltr"></param>
		/// <returns></returns>
		public static (Matrix3x2 model, Matrix3x2 proj) AxisTop(Rect axisrect, double min, double max, bool ltr = true) {
			return (ModelFor(min, max, 0, 1), ProjectForQuadrant(ltr ? 1 : 2, axisrect));
		}
		/// <summary>
		/// Vertical axis mapping: Right.
		/// x(NDC) y(WC)
		/// </summary>
		/// <param name="axisrect"></param>
		/// <param name="min"></param>
		/// <param name="max"></param>
		/// <param name="b2t"></param>
		/// <returns></returns>
		public static (Matrix3x2 model, Matrix3x2 proj) AxisRight(Rect axisrect, double min, double max, bool b2t = true) {
			return (ModelFor(0, 1, min, max), ProjectForQuadrant(b2t ? 1 : 4, axisrect));
		}
		/// <summary>
		/// Vertical axis mapping: Left.
		/// x(NDC) y(WC)
		/// </summary>
		/// <param name="axisrect"></param>
		/// <param name="min"></param>
		/// <param name="max"></param>
		/// <param name="b2t"></param>
		/// <returns></returns>
		public static (Matrix3x2 model, Matrix3x2 proj) AxisLeft(Rect axisrect, double min, double max, bool b2t = true) {
			return (ModelFor(0, 1, min, max), ProjectForQuadrant(b2t ? 2 : 3, axisrect));
		}
	}
}
