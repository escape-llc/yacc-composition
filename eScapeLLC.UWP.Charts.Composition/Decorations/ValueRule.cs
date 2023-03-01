using eScape.Core;
using eScape.Host;
using eScapeLLC.UWP.Charts.Composition.Events;
using eScapeLLC.UWP.Charts.Composition.Factory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;

namespace eScapeLLC.UWP.Charts.Composition {
	/// <summary>
	/// Tracks a value against a Value Axis via a line segment spanning the alternate axis e.g. Category Axis.
	/// </summary>
	public class ValueRule : ChartComponent, IRequireEnterLeave, IProvideSeriesItemValues, IProvideSeriesItemLayout,
		IConsumer<Phase_ComponentExtents>, IConsumer<Phase_ModelComplete>, IConsumer<Phase_Transforms> {
		static readonly LogTools.Flag _trace = LogTools.Add("ValueRule", LogTools.Level.Error);
		#region inner
		class Value_LayoutSession : LayoutSession {
			readonly AxisOrientation c1axis;
			readonly AxisOrientation c2axis;
			internal Value_LayoutSession(Matrix3x2 model, Matrix3x2 projection, AxisOrientation c1axis, AxisOrientation c2axis) : base(model, projection) {
				this.c1axis = c1axis;
				this.c2axis = c2axis;
			}
			public override (Vector2 center, Point direction)? Layout(ISeriesItem isi, Point offset) {
				if (isi is ItemStateC1 sis) {
					var invert = sis.Component1 < 0 ? -1 : 1;
					var center = MappingSupport.ToVector(offset.X, c1axis, sis.Component1 + offset.Y * invert, c2axis);
					var (dx, dy) = MappingSupport.MapComponents(1, c1axis, sis.Component1 < 0 ? 1 : -1, c2axis);
					return (Project(center), new Point(dx, dy));
				}
				return null;
			}
		}
		#endregion
		#region properties
		/// <summary>
		/// Component name of value axis.
		/// Referenced component MUST implement IChartAxis.
		/// </summary>
		public string ValueAxisName { get; set; }
		/// <summary>
		/// Binding path to the value axis value.
		/// </summary>
		public double Value { get { return (double)GetValue(ValueProperty); } set { SetValue(ValueProperty, value); } }
		/// <summary>
		/// How to create the elements for this series.
		/// </summary>
		public IElementFactory ElementFactory { get; set; }
		/// <summary>
		/// How to create animations for series and its elements.
		/// </summary>
		public IAnimationFactory AnimationFactory { get; set; }
		/// <summary>
		/// Provide a wrapper so labels can generate.
		/// </summary>
		IEnumerable<ISeriesItem> IProvideSeriesItemValues.SeriesItemValues {
			get {
				var sivc = new ItemStateC1(0, Value);
				return new[] { sivc };
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
		#endregion
		#region DPs
		/// <summary>
		/// Value DP.
		/// </summary>
		public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
			nameof(Value), typeof(double), typeof(ValueRule), new PropertyMetadata(null, new PropertyChangedCallback(ComponentPropertyChanged))
		);
		/// <summary>
		/// Generic DP property change handler.
		/// Calls DataSeries.ProcessData().
		/// </summary>
		/// <param name="d"></param>
		/// <param name="dpcea"></param>
		private static void ComponentPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs dpcea) {
			ValueRule hr = d as ValueRule;
			if (dpcea.OldValue != dpcea.NewValue) {
				if (hr.ValueAxis == null) return;
				var aus = AxisUpdateState.None;
				if (hr.ValueAxis != null) {
					if (hr.Value > hr.ValueAxis.Maximum || hr.Value < hr.ValueAxis.Minimum) {
						_trace.Verbose($"{hr.Name} axis-update-required");
						aus = AxisUpdateState.Value;
					}
				}
				else {
					aus = AxisUpdateState.Value;
				}
				var cop = new Component_Operation(hr, RefreshRequestType.ValueDirty, aus);
				hr.Forward?.Forward(new Component_RefreshRequest(cop));
			}
		}
		#endregion
		#region helpers
		/// <summary>
		/// Locate required components and generate errors if they are not found.
		/// </summary>
		/// <param name="iccc">Use to locate components and report errors.</param>
		protected void EnsureAxis(IChartComponentContext iccc) {
			IChartErrorInfo icei = iccc as IChartErrorInfo;
			if (!string.IsNullOrEmpty(ValueAxisName)) {
				if (!(iccc.Find(ValueAxisName) is IChartAxis axis)) {
					icei?.Report(new ChartValidationResult(NameOrType(), $"Value axis '{ValueAxisName}' was not found", new[] { nameof(ValueAxisName) }));
				}
				else {
					if (axis.Type != AxisType.Value) {
						icei?.Report(new ChartValidationResult(NameOrType(), $"Value axis '{ValueAxisName}' Type {axis.Type} is not Value", new[] { nameof(ValueAxisName) }));
					}
				}
			}
			else {
				icei?.Report(new ChartValidationResult(NameOrType(), $"Property '{nameof(ValueAxisName)}' was not set", new[] { nameof(ValueAxisName) }));
			}
		}
		#endregion
		#region handlers
		void IConsumer<Phase_ComponentExtents>.Consume(Phase_ComponentExtents message) {
			message.Register(new Component_Extents(Name, null, ValueAxisName, Value, Value));
		}
		protected CompositionLineGeometry Line { get; set; }
		protected CompositionShape Sprite { get; set; }
		protected Matrix3x2 Model { get; set; }
		Side FlipSide(Side vx) {
			if (vx == Side.Left || vx == Side.Right) return Side.Bottom;
			else if (vx == Side.Top || vx == Side.Bottom) return Side.Left;
			throw new ArgumentException(nameof(vx));
		}
		void IConsumer<Phase_ModelComplete>.Consume(Phase_ModelComplete message) {
			ValueAxis = message.AxisExtents.SingleOrDefault(xx => xx.AxisName == ValueAxisName);
			if (ValueAxis == null) return;
			if (double.IsNaN(ValueAxis.Minimum) || double.IsNaN(ValueAxis.Maximum)) return;
			if (double.IsNaN(Value)) return;
			if (Container == null) return;
			if (ElementFactory == null) return;
			var model = MatrixSupport.ModelFor(0, 1, ValueAxis.Minimum, ValueAxis.Maximum);
			// C_1(ndc), C_2(M)
			// Offset(0, Value)
			var c1ori = MappingSupport.OppositeOf(ValueAxis.Orientation);
			var offset = MappingSupport.ToVector(0, c1ori, Value, ValueAxis.Orientation);
			var fakec = new Axis_Extents(null, 0, 1, FlipSide(ValueAxis.AxisSide), AxisType.Category, false);
			if (Sprite == null) {
				// (0,0)...(1,0)
				var v1 = MappingSupport.ToVector(0, c1ori, 0, ValueAxis.Orientation);
				var v2 = MappingSupport.ToVector(1, c1ori, 0, ValueAxis.Orientation);
				var gctx = new LineGeometryContext(Container.Compositor, v1, v2);
				var sprite = ElementFactory.CreateElement(gctx);
				sprite.TransformMatrix = model;
				sprite.Offset = offset;
				Sprite = sprite;
				if (AnimationFactory != null) {
					var ectx = new ValueContext(Container.Compositor, Value, fakec, ValueAxis, ItemTransition.None);
					if(!AnimationFactory.StartAnimation(AnimationKeys.ENTER, ectx, Container.Shapes, Sprite)) {
						Container.Shapes.Add(Sprite);
					}
				}
				else {
					Container.Shapes.Add(Sprite);
				}
			}
			else {
				if (AnimationFactory != null) {
					// animate offset change
					if (offset != Sprite.Offset) {
						var octx = new ValueContext(Container.Compositor, Value, fakec, ValueAxis, ItemTransition.None);
						if(!AnimationFactory.StartAnimation(AnimationKeys.OFFSET, octx, Sprite)) {
							if (offset != Sprite.Offset) {
								Sprite.Offset = offset;
							}
						}
					}
				}
				else {
					if (model != Model) {
						Sprite.TransformMatrix = model;
					}
					if (offset != Sprite.Offset) {
						Sprite.Offset = offset;
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
			EnsureAxis(icelc as IChartComponentContext);
			var icei = icelc as IChartErrorInfo;
			if (ElementFactory == null) {
				icei?.Report(new ChartValidationResult(NameOrType(), $"Property '{nameof(ElementFactory)}' was not set", new[] { nameof(ElementFactory) }));
			}
			Compositor wcc = Window.Current.Compositor;
			Container = wcc.CreateContainerShape();
			Container.Comment = $"container_{Name}";
			Layer = icelc.CreateLayer(Container);
			AnimationFactory?.Prepare(wcc);
			_trace.Verbose($"{Name} enter v:{ValueAxisName}");
		}
		void IRequireEnterLeave.Leave(IChartEnterLeaveContext icelc) {
			_trace.Verbose($"{Name} leave");
			AnimationFactory?.Unprepare(Window.Current.Compositor);
			Container = null;
			icelc.DeleteLayer(Layer);
			Layer = null;
		}
		#endregion
		#region IProvideSeriesItemLayout
		public ILayoutSession Create(Rect area) {
			var xaxis = ValueAxis.Orientation == AxisOrientation.Horizontal ? ValueAxis.Reversed : false;
			var yaxis = ValueAxis.Orientation == AxisOrientation.Vertical ? ValueAxis.Reversed : false;
			var q = MatrixSupport.QuadrantFor(!xaxis, !yaxis);
			var proj = MatrixSupport.ProjectForQuadrant(q, area);
			return new Value_LayoutSession(Model, proj, MappingSupport.OppositeOf(ValueAxis.Orientation), ValueAxis.Orientation);
		}
		#endregion
	}
}
