using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media.Animation;

namespace eScapeLLC.UWP.Charts.Composition {
	#region layer implementations
	#region CanvasLayerCore
	/// <summary>
	/// Base implementation for <see cref="IChartLayer"/> that uses a <see cref="Canvas"/>.
	/// </summary>
	public abstract class CanvasLayerCore : IChartLayer/*, IChartLayerAnimation */{
		#region data
		/// <summary>
		/// Access to the <see cref="Canvas"/>.
		/// </summary>
		protected readonly Canvas canvas;
		#endregion
		#region ctor
		/// <summary>
		/// Ctor.
		/// </summary>
		/// <param name="canvas">Target canvas.</param>
		public CanvasLayerCore(Canvas canvas) {
			this.canvas = canvas;
		}
		#endregion
		#region helpers
		#endregion
		#region extension points
		/// <summary>
		/// Add element with assign z-index.
		/// </summary>
		/// <param name="fe"></param>
		protected virtual void InternalAdd(FrameworkElement fe) {
			var sb = default(Storyboard);
			//if(this is IChartLayerAnimation icla) {
			//	sb = icla.Enter.Clone(fe);
			//}
			canvas.Children.Add(fe);
			sb?.Begin();
		}
		/// <summary>
		/// Add elements with assign z-index.
		/// </summary>
		/// <param name="fes"></param>
		protected virtual void InternalAdd(IEnumerable<FrameworkElement> fes) { foreach (var fe in fes) { (this as IChartLayer).Add(fe); } }
		/// <summary>
		/// Set Canvas layout properties on the source canvas.
		/// </summary>
		/// <param name="target">Location in PX.</param>
		protected abstract void InternalLayout(Rect target);
		/// <summary>
		/// Remove element and call <see cref="PostRemove"/>.
		/// If there's a <see cref="Storyboard"/> in effect, waits until that is done playing before remove.
		/// </summary>
		/// <param name="fe"></param>
		protected virtual void InternalRemove(FrameworkElement fe) {
			var sb = default(Storyboard);
			/*
			if (this is IChartLayerAnimation icla) {
				sb = icla.Leave.Clone(fe);
				if (sb != null) {
					sb.Completed += (sender, e) => {
						canvas.Children.Remove(fe);
						PostRemove(fe);
					};
					sb.Begin();
				}
			}*/
			if(sb == null) {
				canvas.Children.Remove(fe);
				PostRemove(fe);
			}
		}
		/// <summary>
		/// Any actions to perform AFTER element has left VT.
		/// Called from <see cref="InternalClear"/> and <see cref="InternalRemove(FrameworkElement)"/>.
		/// </summary>
		/// <param name="fe"></param>
		protected virtual void PostRemove(FrameworkElement fe) { }
		/// <summary>
		/// Remove elements.
		/// </summary>
		/// <param name="fes"></param>
		protected virtual void InternalRemove(IEnumerable<FrameworkElement> fes) { foreach (var fe in fes) (this as IChartLayer).Remove(fe); }
		/// <summary>
		/// Remove all children.
		/// Invokes <see cref="Storyboard"/> if present.
		/// </summary>
		protected virtual void InternalClear() {
			var array = new UIElement[canvas.Children.Count];
			try {
				canvas.Children.CopyTo(array, 0);
				canvas.Children.Clear();
			} finally {
				foreach (var fe in array) {
					if (fe is FrameworkElement fe2) {
						InternalRemove(fe2);
					}
				}
			}
		}
		#endregion
		#region IChartLayerAnimation
		//Storyboard IChartLayerAnimation.Enter { get; set; }
		//Storyboard IChartLayerAnimation.Leave { get; set; }
		#endregion
		#region IChartLayer
		void IChartLayer.Add(FrameworkElement fe) { InternalAdd(fe); }
		void IChartLayer.Add(IEnumerable<FrameworkElement> fes) { InternalAdd(fes); }
		void IChartLayerCore.Layout(Rect target) { InternalLayout(target); }
		void IChartLayer.Remove(FrameworkElement fe) { InternalRemove(fe); }
		void IChartLayer.Remove(IEnumerable<FrameworkElement> fes) { InternalRemove(fes); }
		void IChartLayerCore.Clear() { InternalClear(); }
		#endregion
	}
	#endregion
	#region CommonCanvasLayer
	/// <summary>
	/// Layer where all layers share a common <see cref="Canvas"/>.
	/// Because of the sharing, this implementation tracks its "own" elements separately from <see cref="Panel.Children"/>.
	/// </summary>
	public class CommonCanvasLayer : CanvasLayerCore {
		#region data
		readonly int zindex;
		readonly List<FrameworkElement> elements;
		#endregion
		#region ctor
		/// <summary>
		/// Ctor.
		/// </summary>
		/// <param name="canvas">Target canvas (shared).</param>
		/// <param name="zindex">Z-index to assign to elements.</param>
		public CommonCanvasLayer(Canvas canvas, int zindex) :base(canvas) {
			this.zindex = zindex;
			this.elements = new List<FrameworkElement>();
		}
		#endregion
		#region IChartLayer
		/// <summary>
		/// Add element with assigned z-index.
		/// </summary>
		/// <param name="fe"></param>
		protected override void InternalAdd(FrameworkElement fe) {
			fe.SetValue(Canvas.ZIndexProperty, zindex);
			base.InternalAdd(fe);
			elements.Add(fe);
		}
		/// <summary>
		/// Do Not Respond, because one canvas owns everything.
		/// </summary>
		/// <param name="target"></param>
		protected override void InternalLayout(Rect target) { }
		/// <summary>
		/// Do reverse bookkeeping.
		/// </summary>
		/// <param name="fe"></param>
		protected override void PostRemove(FrameworkElement fe) {
			elements.Remove(fe);
			base.PostRemove(fe);
		}
		#endregion
	}
	#endregion
	#region CanvasLayer
	/// <summary>
	/// Layer where each layer is bound to a different <see cref="Canvas"/> (COULD be <see cref="IPanel"/>).
	/// This implementation relies on <see cref="Panel.Children"/> to track the elements.
	/// </summary>
	public class CanvasLayer : CanvasLayerCore {
		#region data
		readonly int zindex;
		#endregion
		#region ctor
		/// <summary>
		/// Ctor.
		/// </summary>
		/// <param name="canvas">Target canvas.</param>
		/// <param name="zindex">Z-index to assign to this canvas.</param>
		public CanvasLayer(Canvas canvas, int zindex) : base(canvas) {
			this.zindex = zindex;
			canvas.SetValue(Canvas.ZIndexProperty, zindex);
		}
		#endregion
		#region extensions
		/// <summary>
		/// Set Canvas layout properties on the source canvas.
		/// </summary>
		/// <param name="target">Location in PX.</param>
		protected override void InternalLayout(Rect target) {
			canvas.SetValue(Canvas.TopProperty, target.Top);
			canvas.SetValue(Canvas.LeftProperty, target.Left);
			canvas.SetValue(FrameworkElement.WidthProperty, target.Width);
			canvas.SetValue(FrameworkElement.HeightProperty, target.Height);
		}
		#endregion
	}
	#endregion
	#region CompositionLayer
	public class CompositionLayer : IChartLayerCore, IChartCompositionLayer {
		#region data
		/// <summary>
		/// Access to the <see cref="Canvas"/>.
		/// </summary>
		internal readonly Canvas canvas;
		#endregion
		#region ctor
		/// <summary>
		/// Ctor.
		/// </summary>
		/// <param name="canvas">Target canvas.</param>
		public CompositionLayer(Canvas canvas, int zindex) {
			this.canvas = canvas;
			canvas.SetValue(Canvas.ZIndexProperty, zindex);
			var cc = Window.Current.Compositor;
			var shape = cc.CreateShapeVisual();
			shape.Size = new Vector2((float)canvas.ActualWidth, (float)canvas.ActualHeight);
			ElementCompositionPreview.SetElementChildVisual(canvas, shape);
		}
		public void SizeChanged(SizeChangedEventArgs e) {
			var shape = ElementCompositionPreview.GetElementChildVisual(canvas) as ShapeVisual;
			if (shape == null) return;
			shape.Size = new Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
		}
		private void Local_SizeChanged(object sender, SizeChangedEventArgs e) {
			var shape = ElementCompositionPreview.GetElementChildVisual(canvas) as ShapeVisual;
			if (shape == null) return;
			shape.Size = new Vector2((float)canvas.ActualWidth, (float)canvas.ActualHeight);
		}
		void IChartCompositionLayer.Use(Action<ShapeVisual> useit) {
			var shape = ElementCompositionPreview.GetElementChildVisual(canvas) as ShapeVisual;
			if (shape == null) return;
			useit(shape);
		}
		void IChartLayerCore.Clear() {
			var shape = ElementCompositionPreview.GetElementChildVisual(canvas) as ShapeVisual;
			if (shape == null) return;
			shape.Shapes.Clear();
		}
		void IChartLayerCore.Layout(Rect target) {
			canvas.SetValue(Canvas.TopProperty, target.Top);
			canvas.SetValue(Canvas.LeftProperty, target.Left);
			canvas.SetValue(FrameworkElement.WidthProperty, target.Width);
			canvas.SetValue(FrameworkElement.HeightProperty, target.Height);
		}
		#endregion
	}
	#endregion
	#endregion
}
