#define ANIMATE
using MatrixStuff;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Charts.UWP.Composition {
	public static class PathBuilderExtensions {
		public static CanvasPathBuilder BuildPathWithLines(
				this CanvasPathBuilder builder,
				IEnumerable<Vector2> vectors,
				CanvasFigureLoop canvasFigureLoop) {
			var first = true;

			foreach (var vector2 in vectors) {
				if (first) {
					builder.BeginFigure(vector2);
					first = false;
				}
				else {
					builder.AddLine(vector2);
				}
			}

			builder.EndFigure(canvasFigureLoop);
			return builder;
		}

		public static CanvasPathBuilder BuildPathWithLines(
				this CanvasPathBuilder builder,
				IEnumerable<(float x, float y)> nodes,
				CanvasFigureLoop canvasFigureLoop) {
			var vectors = nodes.Select(n => new Vector2(n.x, n.y));
			return BuildPathWithLines(builder, vectors, canvasFigureLoop);
		}
	}

	public class PathNode {
		private Vector2 _vector2;

		public PathNode(Vector2 vector2) {
			_vector2 = vector2;
		}
	}

	public enum NodeType {
		Line,
		Arc,
		CubicBezier,
		Geometry,
		QuadraticBezier
	}
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class MainPage : Page {
		#region axis
		public double A1_Min { get; set; } = 0;
		public double A1_Max { get; set; } = 10;
		public double A2_Min { get; set; } = -10;
		public double A2_Max { get; set; } = 10;
		public bool A1_LeftToRight { get; set; }
		public bool A2_BottomToTop { get; set; }
		#endregion
		#region proj
		public Rect ProjRectangle { get { return new Rect(50, 50, canvas.ActualWidth - 100, canvas.ActualHeight - 100); } }
		#endregion
		#region ctor
		public MainPage() {
			this.InitializeComponent();
			canvas.SizeChanged += Evh_SizeChanged;
			canvas.Loaded += Canvas_Loaded;
			A1_LeftToRight = true;
			A2_BottomToTop = true;
		}
		#endregion
		#region axes
		static void GenerateLabels<VT>(Canvas canvas, List<Tuple<Vector2, TextBlock>> pool, IEnumerable<VT> values,
		Func<VT, Vector2> mkpoint, Func<VT, TextBlock> mktb, (Matrix3x2 model, Matrix3x2 proj) pmatrix, Action<Vector2, TextBlock> configure) {
			foreach (var tx in pool) canvas.Children.Remove(tx.Item2);
			pool.Clear();
			var matx = Matrix3x2.Multiply(pmatrix.model, pmatrix.proj);
			foreach (var axv in values) {
				var point = mkpoint(axv);
				var dc = Vector2.Transform(point, matx);
				var tx = mktb(axv);
				configure(dc, tx);
				canvas.Children.Add(tx);
				pool.Add(new Tuple<Vector2, TextBlock>(point, tx));
			}
		}
		static void RepositionLabels(List<Tuple<Vector2, TextBlock>> pool, (Matrix3x2 model, Matrix3x2 proj) pmatrix, double dx, double dy) {
			var matx = Matrix3x2.Multiply(pmatrix.model, pmatrix.proj);
			foreach (var tpx in pool) {
				var dc = Vector2.Transform(tpx.Item1, matx);
				try {
					tpx.Item2.Translation = new Vector3((float)(dc.X + dx), (float)(dc.Y + dy), 0);
				}
				catch (Exception) { //eat it
				}
			}
		}
		static IEnumerable<double> AxisValues(double min, double max, double axv) {
			for (var xv = min; xv <= max; xv += axv) {
				yield return xv;
			}
		}
		static TextBlock CreateLabel(double axv, Brush fg) {
			var tx = new TextBlock() {
				Text = axv.ToString("G3"),
				Foreground = fg,
			};
			return tx;
		}
		static void Configure(double dx, double dy, Vector2 dc, UIElement tx) {
			try {
				tx.Translation = new Vector3((float)(dc.X + dx), (float)(dc.Y + dy), 0);
				//tx.TranslationTransition = new Vector3Transition() { Duration = TimeSpan.FromMilliseconds(50), Components = Vector3TransitionComponents.X | Vector3TransitionComponents.Y };
			}
			catch (Exception) { //eat it
			}
		}
		#region A1
		List<Tuple<Vector2, TextBlock>> poola1 = new List<Tuple<Vector2, TextBlock>>();
		(Matrix3x2 model, Matrix3x2 proj) A1Matrix() {
			var mainrect = ProjRectangle;
			var axisrect = new Rect(mainrect.Left, mainrect.Bottom + 2, mainrect.Width, 20);
			return MatrixSupport2.AxisBottom(axisrect, A1_Min, A1_Max, A1_LeftToRight);
		}
		void GenerateAxis1() {
			if (poola1.Count != 0) return;
			var pmatrix = A1Matrix();
			GenerateLabels(canvas, poola1,
				AxisValues(A1_Min, A1_Max, 1),
				vx => new Vector2((float)vx, 0),
				vx => CreateLabel(vx, new SolidColorBrush(Windows.UI.Color.FromArgb(255, 144, 244, 44))),
				pmatrix, (dc, tx) => { Configure(-8, 0, dc, tx); }
			);
		}
		void RepositionAxis1() {
			var pmatrix = A1Matrix();
			RepositionLabels(poola1, pmatrix, -8, 0);
		}
		#endregion
		#region A3
		List<Tuple<Vector2, TextBlock>> poola3 = new List<Tuple<Vector2, TextBlock>>();
		(Matrix3x2 model, Matrix3x2 proj) A3Matrix() {
			var mainrect = ProjRectangle;
			var axisrect = new Rect(mainrect.Left, mainrect.Top - 22, mainrect.Width, 20);
			return MatrixSupport2.AxisTop(axisrect, A1_Min, A1_Max, A1_LeftToRight);
		}
		void GenerateAxis3() {
			if (poola3.Count != 0) return;
			var pmatrix = A3Matrix();
			GenerateLabels(canvas, poola3,
				AxisValues(A1_Min, A1_Max, 1),
				vx => new Vector2((float)vx, 1), vx => CreateLabel(vx, new SolidColorBrush(Windows.UI.Color.FromArgb(255, 244, 244, 44))),
				pmatrix, (dc, tx) => { Configure(-8, 0, dc, tx); }
			);
		}
		void RepositionAxis3() {
			var pmatrix = A3Matrix();
			RepositionLabels(poola3, pmatrix, -8, 0);
		}
		#endregion
		#region A2
		List<Tuple<Vector2, TextBlock>> poola2 = new List<Tuple<Vector2, TextBlock>>();
		(Matrix3x2 model, Matrix3x2 proj) A2Matrix() {
			var mainrect = ProjRectangle;
			var axisrect = new Rect(mainrect.Right + 6, mainrect.Top, 20, mainrect.Height);
			return MatrixSupport2.AxisRight(axisrect, A2_Min, A2_Max, A2_BottomToTop);
		}
		void GenerateAxis2() {
			if (poola2.Count != 0) return;
			var pmatrix = A2Matrix();
			GenerateLabels(canvas, poola2,
				AxisValues(A2_Min, A2_Max, 1),
				vx => new Vector2(0, (float)vx), vx => CreateLabel(vx, new SolidColorBrush(Windows.UI.Color.FromArgb(255, 44, 244, 244))),
				pmatrix,
				(dc, tx) => { Configure(0, -8, dc, tx); }
			);
		}
		void RepositionAxis2() {
			var pmatrix = A2Matrix();
			RepositionLabels(poola2, pmatrix, 0, -8);
		}
		#endregion
		#region A4
		List<Tuple<Vector2, TextBlock>> poola4 = new List<Tuple<Vector2, TextBlock>>();
		(Matrix3x2 model, Matrix3x2 proj) A4Matrix() {
			var mainrect = ProjRectangle;
			var axisrect = new Rect(mainrect.Left - 26, mainrect.Top, 20, mainrect.Height);
			return MatrixSupport2.AxisLeft(axisrect, A2_Min, A2_Max, A2_BottomToTop);
		}
		void GenerateAxis4() {
			if (poola4.Count != 0) return;
			var pmatrix = A4Matrix();
			GenerateLabels(canvas, poola4,
				AxisValues(A2_Min, A2_Max, 1),
				(vx) => new Vector2(1, (float)vx), vx => CreateLabel(vx, new SolidColorBrush(Windows.UI.Color.FromArgb(255, 44, 144, 244))), pmatrix,
				(dc, tx) => { Configure(0, -8, dc, tx); }
			);
		}
		void RepositionAxis4() {
			var pmatrix = A4Matrix();
			RepositionLabels(poola4, pmatrix, 0, -8);
		}
		#endregion
		#endregion
		#region configure
		void Configure(CompositionSpriteShape css, Windows.UI.Color fill, Windows.UI.Color stroke, float st) {
			css.FillBrush = css.Compositor.CreateColorBrush(fill);
			css.StrokeBrush = css.Compositor.CreateColorBrush(stroke);
			css.StrokeThickness = st;
			// forces Stroke to be PX units regardless
			css.IsStrokeNonScaling = true;
		}
		CompositionSpriteShape CreateSeriesRectangle2(Compositor c, float wid, float hgt, Windows.UI.Color color) {
			var seriesRectangle = c.CreateRoundedRectangleGeometry();
			// IST CornerRadius is NDC units (output side of transform)!
			seriesRectangle.CornerRadius = new Vector2(0.05f, 0.05f);
			var seriesSprite = c.CreateSpriteShape(seriesRectangle);
			Configure(seriesSprite, color, Colors.White, 2f);
			// Offset and Size are Model units (input side of transform)
			seriesRectangle.Size = new Vector2(Math.Abs(wid), Math.Abs(hgt));
			return seriesSprite;
		}
		void ConfigureElement(CompositionSpriteShape css, float xx, float yy) {
			css.Comment = $"({xx},{yy})";
			var yo = yy < 0 ? yy : 0;
#if ANIMATE
			// slides in from the left side (0 --> xx)
			css.Offset = new Vector2((float)A1_Min, yo);
			// right side
			//css.Offset = new Vector2((float)A1_Max, yo);
			// top
			//css.Offset = new Vector2(xx, (float)A2_Max);
			// bottom
			//css.Offset = new Vector2(xx, (float)A2_Min);
			// Scale etc. work ON TOP OF the TransformMatrix!
			// this scales from "sliver" to full size
			// y-sliver (uniform scale all bars same width)
			css.Scale = new Vector2(0.05f, 1);
			// x-sliver (scales non-uniformly because it's Model coords)
			// would have to back-calculate scale factor to get constant PX
			//css.Scale = new Vector2(1, 0.05f);
			Vector2KeyFrameAnimation aoffset = css.Compositor.CreateVector2KeyFrameAnimation();
			aoffset.InsertKeyFrame(1f, new Vector2(xx, yo));
			aoffset.Duration = TimeSpan.FromSeconds(1.25);
			aoffset.StopBehavior = AnimationStopBehavior.SetToFinalValue;
			Vector2KeyFrameAnimation ascale = css.Compositor.CreateVector2KeyFrameAnimation();
			ascale.InsertKeyFrame(1f, new Vector2(1, 1));
			ascale.Duration = TimeSpan.FromSeconds(1.25);
			ascale.StopBehavior = AnimationStopBehavior.SetToFinalValue;
			// start after first one completes
			ascale.DelayTime = aoffset.Duration;
			css.StartAnimation("Offset", aoffset);
			css.StartAnimation("Scale", ascale);
#else
			css.Offset = new Vector2(xx, yo);
#endif
		}
		#endregion
		#region evhs
		private void CheckBox_A1(object sender, RoutedEventArgs e) {
			A1_LeftToRight = true;
			if (ActualWidth == 0 || ActualHeight == 0) return;
			RepositionAxis1();
			RepositionAxis3();
			RepositionSeries();
		}
		private void CheckBox_Uncheck_A1(object sender, RoutedEventArgs e) {
			A1_LeftToRight = false;
			if (ActualWidth == 0 || ActualHeight == 0) return;
			RepositionAxis1();
			RepositionAxis3();
			RepositionSeries();
		}

		private void CheckBox_A2(object sender, RoutedEventArgs e) {
			A2_BottomToTop = true;
			if (ActualWidth == 0 || ActualHeight == 0) return;
			RepositionAxis2();
			RepositionAxis4();
			RepositionSeries();
		}
		private void CheckBox_Uncheck_A2(object sender, RoutedEventArgs e) {
			A2_BottomToTop = false;
			if (ActualWidth == 0 || ActualHeight == 0) return;
			RepositionAxis2();
			RepositionAxis4();
			RepositionSeries();
		}
		private void Canvas_Loaded(object sender, RoutedEventArgs e) {
			var c = Window.Current.Compositor;
			var shapeVisual = c.CreateShapeVisual();
			var seriesContainer3 = ExamplePath(c);
			var seriesContainer2 = ExampleBars(c);
			shapeVisual.Shapes.Add(seriesContainer2);
			shapeVisual.Shapes.Add(seriesContainer3);
			shapeVisual.Size = new Vector2((float)canvas.RenderSize.Width, (float)canvas.RenderSize.Height);
			ElementCompositionPreview.SetElementChildVisual(canvas, shapeVisual);
		}
		private void Evh_SizeChanged(object sender, SizeChangedEventArgs e) {
			if (e.NewSize.Width == 0 || e.NewSize.Height == 0) return;
			GenerateAxis1();
			GenerateAxis2();
			GenerateAxis3();
			GenerateAxis4();
			RepositionAxis1();
			RepositionAxis2();
			RepositionAxis3();
			RepositionAxis4();
			RepositionSeries();
		}
		#endregion
		#region rectangle series
		int QuadrantFor(bool l2r, bool b2t) {
			if(l2r) {
				return b2t ? 1 : 4;
			}
			else {
				return b2t ? 2 : 3;
			}
		}
		void RepositionSeries() {
			var shape = ElementCompositionPreview.GetElementChildVisual(canvas);
			if (shape == null) return;
			if(shape is ShapeVisual sv) {
				var q = QuadrantFor(A1_LeftToRight, A2_BottomToTop);
				var proj = MatrixSupport2.ProjectForQuadrant(q, ProjRectangle);
				foreach (var shx in sv.Shapes) {
					shx.TransformMatrix = proj;
				}
				shape.Size = new Vector2((float)RenderSize.Width, (float)RenderSize.Height);
			}
		}
		#endregion
		CompositionShape ExamplePath(Compositor c) {
			var proj = MatrixSupport2.ProjectForQuadrant(A2_BottomToTop ? 1 : 4, ProjRectangle);
			var container = c.CreateContainerShape();
			container.TransformMatrix = proj;
			var model = MatrixSupport2.ModelFor(A1_Min, A1_Max, A2_Min, A2_Max);
			var xoffset = 0.5f;
			var cpb = new CanvasPathBuilder(new CanvasDevice());
			using (cpb) {
				cpb.BuildPathWithLines(new (float x, float y)[] {
					(0 + xoffset, 6), (1 + xoffset, 8), (2 + xoffset, -4), (3 + xoffset, 1.5f), (4 + xoffset, -9.1f),
					(5 + xoffset, 0.5f), (6 + xoffset, 2.3f), (7 + xoffset, -10f), (8 + xoffset, 10), (9 + xoffset, 5.778f)
					},
					CanvasFigureLoop.Open);
				var geom = CanvasGeometry.CreatePath(cpb);
				var path = new CompositionPath(geom);
				var pathgeom = c.CreatePathGeometry(path);
				var shape = c.CreateSpriteShape(pathgeom);
				shape.TransformMatrix = model;
				shape.StrokeBrush = c.CreateColorBrush(Colors.GreenYellow);
				shape.StrokeThickness = 5;
				shape.IsStrokeNonScaling = true;
				shape.StrokeLineJoin = CompositionStrokeLineJoin.Round;
				shape.StrokeStartCap = CompositionStrokeCap.Round;
				shape.StrokeEndCap = CompositionStrokeCap.Round;
				ConfigureElement(shape, 0, 0);
				container.Shapes.Add(shape);
			}
			return container;
		}
		CompositionShape ExampleBars(Compositor c) {
			// the "outer" container uses the P matrix
			var proj = MatrixSupport2.ProjectForQuadrant(A2_BottomToTop ? 1 : 4, ProjRectangle);
			var container = c.CreateContainerShape();
			container.TransformMatrix = proj;
			var data = new Tuple<float, float, Windows.UI.Color>[] {
				new Tuple<float, float, Windows.UI.Color>(0, 6, Colors.Goldenrod),
				new Tuple<float, float, Windows.UI.Color>(1, 8, Colors.Blue),
				new Tuple<float, float, Windows.UI.Color>(2, -4, Colors.Red),
				new Tuple<float, float, Windows.UI.Color>(3, 1.5f, Colors.Goldenrod),
				new Tuple<float, float, Windows.UI.Color>(4, -9.1f, Colors.Blue),
				new Tuple<float, float, Windows.UI.Color>(5, 0.5f, Colors.Red),
				new Tuple<float, float, Windows.UI.Color>(6, 2.3f, Colors.Goldenrod),
				new Tuple<float, float, Windows.UI.Color>(7, -10f, Colors.Blue),
				new Tuple<float, float, Windows.UI.Color>(8, 10f, Colors.Red),
				new Tuple<float, float, Windows.UI.Color>(9, 5.778f, Colors.Goldenrod),
			};
			// each child element uses the M matrix
			var model = MatrixSupport2.ModelFor(A1_Min, A1_Max, A2_Min, A2_Max);
			var xoffset = 0.25f;
			var xbarwid = 0.5f;
			foreach(var itx in data) {
				var item = CreateSeriesRectangle2(c, xbarwid, itx.Item2, itx.Item3);
				item.TransformMatrix = model;
				ConfigureElement(item, itx.Item1 + xoffset, itx.Item2);
				container.Shapes.Add(item);
			}
			return container;
		}
		#region not used
		ShapeVisual Example1(Compositor c) {
			// Need this so we can add multiple shapes to a sprite
			var shapeContainer = c.CreateContainerShape();

			// Rounded Rectangle - just the rounded rect properties
			var roundedRectangle = c.CreateRoundedRectangleGeometry();
			roundedRectangle.CornerRadius = new Vector2(20);
			roundedRectangle.Size = new Vector2(400, 300);

			// Need to create a sprite shape from the rounded rect
			var roundedRectSpriteShape = c.CreateSpriteShape(roundedRectangle);
			Configure(roundedRectSpriteShape, Colors.Red, Colors.Green, 5);
			roundedRectSpriteShape.Offset = new Vector2(100, 50);

			// Now we must add that share to the container
			shapeContainer.Shapes.Add(roundedRectSpriteShape);

			// Let's create another shape
			var roundedRectSpriteShape2 = c.CreateSpriteShape(roundedRectangle);
			Configure(roundedRectSpriteShape2, Colors.Purple, Colors.Yellow, 3);
			roundedRectSpriteShape2.Offset = new Vector2(200, 50);
			roundedRectSpriteShape2.CenterPoint = new Vector2(roundedRectangle.Size.X / 2, roundedRectangle.Size.Y / 2);
			roundedRectSpriteShape2.RotationAngleInDegrees = 5;

			// Add it to the container - as it is added after the previous shape, it will appear on top
			shapeContainer.Shapes.Add(roundedRectSpriteShape2);

			// Create paths and animate them
			//SetupPathAndAnimation(c, shapeContainer);

			// Now we need to create a ShapeVisual and add the ShapeContainer to it.
			var shapeVisual = c.CreateShapeVisual();
			shapeVisual.Shapes.Add(shapeContainer);
			return shapeVisual;
		}
		private static void SetupPathAndAnimation(Compositor c, CompositionContainerShape shapeContainer) {
			var startPathBuilder = new CanvasPathBuilder(new CanvasDevice());

			// Use my helper to create a W shaped path
			startPathBuilder.BuildPathWithLines(new (float x, float y)[]
					{
						(10, 10), (30, 80), (50, 30), (70, 80), (90, 10)
					},
					CanvasFigureLoop.Open);

			// Add another path
			startPathBuilder.BuildPathWithLines(new (float x, float y)[]
					{
						(105, 30), (105, 80)
					},
					CanvasFigureLoop.Open);

			// Create geometry and path that represents the start position of an animation
			var startGeometry = CanvasGeometry.CreatePath(startPathBuilder);
			var startPath = new CompositionPath(startGeometry);

			// Now create the end state paths
			var endPathBuilder = new CanvasPathBuilder(new CanvasDevice());
			endPathBuilder.BuildPathWithLines(new (float x, float y)[]
					{
						(10, 10), (30, 10), (50, 10), (70, 10), (90, 10)
					},
					CanvasFigureLoop.Open);

			endPathBuilder.BuildPathWithLines(new (float x, float y)[]
					{
						(105, 30), (105, 80)
					},
					CanvasFigureLoop.Open);

			var endGeometry = CanvasGeometry.CreatePath(endPathBuilder);
			var endPath = new CompositionPath(endGeometry);

			// Create a CompositionPathGeometery from the Win2D GeometeryPath
			var pathGeometry = c.CreatePathGeometry(startPath);

			// Create a CompositionSpriteShape from the path
			var pathShape = c.CreateSpriteShape(pathGeometry);
			pathShape.StrokeBrush = c.CreateColorBrush(Colors.Purple);
			pathShape.StrokeThickness = 5;
			pathShape.Offset = new Vector2(50);

			// Add the pathShape to the ShapeContainer that we used elsewhere
			// This will ensure it is rendered
			shapeContainer.Shapes.Add(pathShape);

			// Create an animation using the start and endpaths
			var animation = c.CreatePathKeyFrameAnimation();
			animation.Target = "Geometry.Path";
			animation.Duration = TimeSpan.FromSeconds(1);
			animation.InsertKeyFrame(0, startPath);
			animation.InsertKeyFrame(1, endPath);
			animation.IterationBehavior = AnimationIterationBehavior.Forever;
			animation.Direction = Windows.UI.Composition.AnimationDirection.AlternateReverse;
			pathGeometry.StartAnimation(nameof(pathGeometry.Path), animation);
		}
		#endregion
	}
}
