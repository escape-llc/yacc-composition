using eScape.Core;
using eScape.Host;
using eScapeLLC.UWP.Charts.Composition.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Numerics;
using System.Reflection;
using Windows.Media.Core;
using Windows.UI.Composition;
using Windows.UI.Xaml;

namespace eScapeLLC.UWP.Charts.Composition {
	public class ValueBand : ChartComponent, IRequireEnterLeave, IProvideSeriesItemValues, /*IProvideSeriesItemLayout,*/
		IConsumer<Phase_ComponentExtents>, IConsumer<Phase_ModelComplete>, IConsumer<Phase_Transforms> {
		static readonly LogTools.Flag _trace = LogTools.Add("ValueBand", LogTools.Level.Error);
		#region properties
		/// <summary>
		/// Component name of value axis.
		/// Referenced component MUST implement IChartAxis.
		/// </summary>
		public string ValueAxisName { get; set; }
		/// <summary>
		/// Binding path to the value axis value.
		/// </summary>
		public double Value1 { get { return (double)GetValue(Value1Property); } set { SetValue(Value1Property, value); } }
		public double Value2 { get { return (double)GetValue(Value2Property); } set { SetValue(Value2Property, value); } }
		/// <summary>
		/// How to create the elements for this series.
		/// </summary>
		public IElementFactory ElementFactory { get; set; }
		public IElementFactory FillElementFactory { get; set; }
		/// <summary>
		/// How to create animations for series and its elements.
		/// </summary>
		public IAnimationFactory AnimationFactory { get; set; }
		/// <summary>
		/// Provide a wrapper so labels can generate.
		/// </summary>
		IEnumerable<ISeriesItem> IProvideSeriesItemValues.SeriesItemValues {
			get {
				return new[] { new ItemStateC1(0, Value1), new ItemStateC1(1, Value2) };
			}
		}
		/// <summary>
		/// Dereferenced value axis.
		/// </summary>
		protected Axis_Extents ValueAxis { get; set; }
		/// <summary>
		/// The layer for components.
		/// </summary>
		protected IChartCompositionLayer Layer { get; set; }
		/// <summary>
		/// Holds all the shapes for this series.
		/// </summary>
		protected CompositionContainerShape Container { get; set; }
		protected IAnimationController Animate { get; set; }
		protected CompositionShape Line1 { get; set; }
		protected CompositionShape Line2 { get; set; }
		protected CompositionShape Fill { get; set; }
		protected Matrix3x2 Model { get; set; }
		#endregion
		#region DPs
		/// <summary>
		/// Value DP.
		/// </summary>
		public static readonly DependencyProperty Value1Property = DependencyProperty.Register(
			nameof(Value1), typeof(double), typeof(ValueBand), new PropertyMetadata(null, new PropertyChangedCallback(ComponentPropertyChanged))
		);
		public static readonly DependencyProperty Value2Property = DependencyProperty.Register(
			nameof(Value2), typeof(double), typeof(ValueBand), new PropertyMetadata(null, new PropertyChangedCallback(ComponentPropertyChanged))
		);
		/// <summary>
		/// Generic DP property change handler.
		/// Calls DataSeries.ProcessData().
		/// </summary>
		/// <param name="d"></param>
		/// <param name="dpcea"></param>
		private static void ComponentPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs dpcea) {
			ValueBand hr = d as ValueBand;
			if (dpcea.OldValue != dpcea.NewValue) {
				if (hr.ValueAxis == null) return;
				var aus = AxisUpdateState.None;
				if (hr.ValueAxis != null) {
					if (dpcea.Property == Value1Property && (hr.Value1 > hr.ValueAxis.Maximum || hr.Value1 < hr.ValueAxis.Minimum)) {
						_trace.Verbose($"{hr.Name} axis-update-required.value1");
						aus = AxisUpdateState.Value;
					}
					else if (dpcea.Property == Value2Property && (hr.Value2 > hr.ValueAxis.Maximum || hr.Value2 < hr.ValueAxis.Minimum)) {
						_trace.Verbose($"{hr.Name} axis-update-required.value2");
						aus = AxisUpdateState.Value;
					}
				}
				else {
					aus = AxisUpdateState.Value;
				}
				var cop = new Component_Operation(hr, RefreshRequestType.ValueDirty, aus);
				hr.Forward?.Forward(new Component_Request(cop));
			}
		}
		#endregion
		#region handlers
		void IConsumer<Phase_ComponentExtents>.Consume(Phase_ComponentExtents message) {
			message.Register(new Component_Extents(Name, null, ValueAxisName, Value1, Value2));
		}
		void EnsureSprite(Axis_Extents cat, AxisOrientation c1ori, Matrix3x2 model, Vector2 offset1, Vector2 offset2) {
			// (0,0)...(1,0)
			var v1 = MappingSupport.ToVector(0, c1ori, 0, ValueAxis.Orientation);
			var v2 = MappingSupport.ToVector(1, c1ori, 0, ValueAxis.Orientation);
			var gctx = new LineGeometryContext(Container.Compositor, v1, v2);
			var sprite = ElementFactory.CreateElement(gctx);
			sprite.TransformMatrix = model;
			sprite.Offset = offset1;
			sprite.Comment = $"{Name}.Value1";
			Line1 = sprite;
			sprite = ElementFactory.CreateElement(gctx);
			sprite.TransformMatrix = model;
			sprite.Offset = offset2;
			sprite.Comment = $"{Name}.Value2";
			Line2 = sprite;
			// area sprite
			CompositionShape element = null;
			if (FillElementFactory != null) {
				// area.Offset = Vector2(0, Math.Min(Line1.Offset.Y, Line2.Offset.Y))
				// area.Size = Vector2(1, Math.Abs(Line2.Offset.Y - Line2.Offset.Y))
				var vs = MappingSupport.ToVector(1, cat.Orientation, Math.Abs(Value2 - Value1), ValueAxis.Orientation);
				// TODO need to re-evaluate the box style on each value change
				var ctx = new ColumnElementContext(Container.Compositor, 0, 0, Value2 - Value1, vs.X, vs.Y, cat, ValueAxis);
				element = FillElementFactory.CreateElement(ctx);
				element.TransformMatrix = model;
				element.Offset = Value1 < Value2 ? offset1 : offset2;
				element.Comment = $"{Name}.Area";
				// TODO expressions depend on orientation
				var exprsz = Container.Compositor.CreateExpressionAnimation("Vector2(1,Abs(Line2.Offset.Y - Line1.Offset.Y))");
				exprsz.SetReferenceParameter("Line1", Line1);
				exprsz.SetReferenceParameter("Line2", Line2);
				// TODO expressions depend on orientation
				var exprof = Container.Compositor.CreateExpressionAnimation("Vector2(0,Min(Line1.Offset.Y, Line2.Offset.Y))");
				exprof.SetReferenceParameter("Line1", Line1);
				exprof.SetReferenceParameter("Line2", Line2);
				(element as CompositionSpriteShape).Geometry.StartAnimation("Offset", exprof);
				(element as CompositionSpriteShape).Geometry.StartAnimation("Size", exprsz);
			}
			Fill = element;
			if (Animate != null) {
				var ectx2 = new ValueContext(Container.Compositor, Value2, cat, ValueAxis, ItemTransition.None);
				if (Fill != null) {
					if (!Animate.Enter(ectx2, Fill, Container.Shapes)) {
						Container.Shapes.Add(Fill);
					}
				}
				var ectx1 = new ValueContext(Container.Compositor, Value1, cat, ValueAxis, ItemTransition.None);
				if (!Animate.Enter(ectx1, Line1, Container.Shapes)) {
					Container.Shapes.Add(Line1);
				}
				if (!Animate.Enter(ectx2, Line2, Container.Shapes)) {
					Container.Shapes.Add(Line2);
				}
			}
			else {
				if (Fill != null) Container.Shapes.Add(Fill);
				Container.Shapes.Add(Line1);
				Container.Shapes.Add(Line2);
			}
		}
		void IConsumer<Phase_ModelComplete>.Consume(Phase_ModelComplete message) {
			ValueAxis = message.AxisExtents.SingleOrDefault(xx => xx.AxisName == ValueAxisName);
			if (ValueAxis == null) return;
			if (double.IsNaN(ValueAxis.Minimum) || double.IsNaN(ValueAxis.Maximum)) return;
			if (double.IsNaN(Value1) || double.IsNaN(Value2)) return;
			if (Container == null) return;
			if (ElementFactory == null) return;
			var cat = new Axis_Extents("$category01", 0, 1, ValueRule.FlipSide(ValueAxis.AxisSide), AxisType.Category, false);
			var model = MatrixSupport.ModelFor(cat.Minimum, cat.Maximum, ValueAxis.Minimum, ValueAxis.Maximum);
			// C_1(ndc), C_2(M), C_3(M)
			// Offset(0, Value1), Offset(0, Value2)
			var c1ori = MappingSupport.OppositeOf(ValueAxis.Orientation);
			var offset1 = MappingSupport.ToVector(0, c1ori, Value1, ValueAxis.Orientation);
			var offset2 = MappingSupport.ToVector(0, c1ori, Value2, ValueAxis.Orientation);
			if (Line1 == null) {
				EnsureSprite(cat, c1ori, model, offset1, offset2);
			}
			else {
				if (Animate != null) {
					if (model != Model) {
						Animate.Transform(null, model);
					}
					if (offset1 != Line1.Offset) {
						var octx = new ValueContext(Container.Compositor, Value1, cat, ValueAxis, ItemTransition.None);
						if (!Animate.Offset(octx, Line1)) {
							Line1.Offset = offset1;
						}
					}
					if(offset2 != Line2.Offset) {
						var octx = new ValueContext(Container.Compositor, Value2, cat, ValueAxis, ItemTransition.None);
						if (!Animate.Offset(octx, Line2)) {
							Line2.Offset = offset2;
						}
					}
				}
				else {
					if (model != Model) {
						Line1.TransformMatrix = model;
						Line2.TransformMatrix = model;
					}
					if (offset1 != Line1.Offset) {
						Line1.Offset = offset1;
					}
					if (offset2 != Line2.Offset) {
						Line2.Offset = offset2;
					}
				}
			}
			Model = model;
		}
		void IConsumer<Phase_Transforms>.Consume(Phase_Transforms message) {
			if (ValueAxis == null) return;
			//if (ItemState.Count == 0) return;
			if (Container == null) return;
			var rctx = message.ContextFor(this);
			var xaxis = ValueAxis.Orientation == AxisOrientation.Horizontal ? ValueAxis.Reversed : false;
			var yaxis = ValueAxis.Orientation == AxisOrientation.Vertical ? ValueAxis.Reversed : false;
			var q = MatrixSupport.QuadrantFor(!xaxis, !yaxis);
			var proj = MatrixSupport.ProjectForQuadrant(q, rctx.SeriesArea);
			Container.TransformMatrix = proj;
		}
		#endregion
		#region IRequireEnterLeave
		void IRequireEnterLeave.Enter(IChartEnterLeaveContext icelc) {
			EnsureAxis(icelc as IChartComponentContext, ValueAxisName);
			var icei = icelc as IChartErrorInfo;
			if (ElementFactory == null) {
				icei?.Report(new ChartValidationResult(NameOrType(), $"Property '{nameof(ElementFactory)}' was not set", new[] { nameof(ElementFactory) }));
			}
			Compositor wcc = Window.Current.Compositor;
			Container = wcc.CreateContainerShape();
			Container.Comment = $"container_{Name}";
			Layer = icelc.CreateLayer(Container);
			Animate = AnimationFactory?.CreateAnimationController(wcc);
			_trace.Verbose($"{Name} enter v:{ValueAxisName}");
		}
		void IRequireEnterLeave.Leave(IChartEnterLeaveContext icelc) {
			_trace.Verbose($"{Name} leave");
			Animate?.Dispose();
			Animate = null;
			Container = null;
			icelc.DeleteLayer(Layer);
			Layer = null;
		}
		#endregion
	}
}
