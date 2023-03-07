using System;
using System.Numerics;
using Windows.UI.Composition;

namespace eScapeLLC.UWP.Charts.Composition {
	#region interfaces
	/// <summary>
	/// Entry interface for element factory.
	/// A specific context implements this plus other interfaces as necessary to support sprite creation.
	/// </summary>
	public interface IElementFactoryContext {
		/// <summary>
		/// Use for creating composition objects.
		/// </summary>
		Compositor Compositor { get; }
	}
	public interface IElementExtentContext {
		Axis_Extents Component1Axis { get; }
		Axis_Extents Component2Axis { get; }
	}
	public interface IElementValueContext {
		double Value { get; }
	}
	/// <summary>
	/// Additional information for creating rectangle geometry.
	/// </summary>
	public interface IElementRectangleContext {
		/// <summary>
		/// Element index.
		/// </summary>
		int Index { get; }
		/// <summary>
		/// Category offset.
		/// </summary>
		double CategoryOffset { get; }
		/// <summary>
		/// Data value.
		/// </summary>
		double DataValue { get; }
		/// <summary>
		/// X axis extent.
		/// </summary>
		double Width { get; }
		/// <summary>
		/// Y axis extent.
		/// </summary>
		double Height { get; }
	}
	/// <summary>
	/// Additional information for creating Line Segment Geometry.
	/// </summary>
	public interface IElementLineContext {
		Vector2 Start { get; }
		Vector2 End { get; }
	}
	/// <summary>
	/// Additional information about the <see cref="ISeriesItemCategoryValue"/> for this sprite.
	/// </summary>
	public interface IElementCategoryValueContext {
		/// <summary>
		/// Information about category axis C_1.
		/// </summary>
		Axis_Extents CategoryAxis { get; }
		/// <summary>
		/// Information about value axis C_2.
		/// </summary>
		Axis_Extents ValueAxis { get; }
		/// <summary>
		/// The item.
		/// </summary>
		ISeriesItemCategoryValue Item { get; }
	}
	/// <summary>
	/// Additional information about the path to use in the sprite.
	/// </summary>
	public interface IElementCompositionPath {
		/// <summary>
		/// The path to use for this sprite.
		/// </summary>
		CompositionPath Path { get; }
	}
	/// <summary>
	/// Additional information about how element is entering/leaving chart.
	/// </summary>
	public interface IElementDataOperation {
		/// <summary>
		/// Head: occurs at the lowest-indexed end.
		/// Tail: occurs at the highest-indexed end.
		/// None: changing state in-place.
		/// <para/>
		/// Used to compute spawn points etc.
		/// </summary>
		ItemTransition Transition { get; }
	}
	#endregion
	#region CategoryValueContext
	/// <summary>
	/// Default context for basic use case in category/value scenario.
	/// </summary>
	public class CategoryValueContext : IElementFactoryContext, IElementCategoryValueContext, IElementDataOperation {
		/// <summary>
		/// Use for creating composition objects.
		/// </summary>
		public Compositor Compositor { get; private set; }
		/// <summary>
		/// Information about category axis.
		/// </summary>
		public Axis_Extents CategoryAxis { get; private set; }
		/// <summary>
		/// Information about value axis.
		/// </summary>
		public Axis_Extents ValueAxis { get; private set; }
		public ISeriesItemCategoryValue Item { get; private set; }
		public ItemTransition Transition { get; private set; }
		public CategoryValueContext(Compositor cx, ISeriesItemCategoryValue isicv, Axis_Extents ca, Axis_Extents va, ItemTransition transition) {
			Compositor = cx;
			Item = isicv;
			CategoryAxis = ca;
			ValueAxis = va;
			Transition = transition;
		}
	}
	#endregion
	#region DefaultContext
	public class DefaultContext : IElementFactoryContext, IElementExtentContext {
		public Compositor Compositor { get; private set; }

		public Axis_Extents Component1Axis { get; private set; }

		public Axis_Extents Component2Axis { get; private set; }

		public DefaultContext(Compositor compositor, Axis_Extents a1, Axis_Extents a2) {
			Compositor = compositor;
			Component1Axis = a1;
			Component2Axis = a2;
		}
	}
	#endregion
	#region ValueContext
	/// <summary>
	/// Context for single value on <see cref="Component2Axis"/>.
	/// </summary>
	public class ValueContext : IElementFactoryContext, IElementExtentContext, IElementValueContext, IElementDataOperation {
		public Compositor Compositor { get; private set; }
		public Axis_Extents Component1Axis { get; private set; }
		public Axis_Extents Component2Axis { get; private set; }
		public double Value { get; private set; }
		public ItemTransition Transition { get; private set; }

		public ValueContext(Compositor compositor, double value, Axis_Extents component1Axis, Axis_Extents component2Axis, ItemTransition transition) {
			Compositor = compositor;
			Component1Axis = component1Axis;
			Component2Axis = component2Axis;
			Value = value;
			Transition = transition;
		}
	}
	#endregion
	#region ColumnElementContext
	/// <summary>
	/// Context for creating bars.
	/// </summary>
	public class ColumnElementContext : DefaultContext, IElementRectangleContext {
		public ColumnElementContext(Compositor cx, int index, double coffset, double value, double ww, double hh, Axis_Extents ca, Axis_Extents va) :base(cx, ca, va) {
			Index = index;
			CategoryOffset = coffset;
			DataValue = value;
			Width = ww;
			Height = hh;
		}
		public int Index { get; private set; }
		public double CategoryOffset { get; private set; }
		public double DataValue { get; private set; }
		/// <summary>
		/// <inheritdoc/>
		/// </summary>
		public double Width { get; private set; }
		/// <summary>
		/// <inheritdoc/>
		/// </summary>
		public double Height { get; private set; }
	}
	#endregion
	#region LineGeometryContext
	public class LineGeometryContext : IElementFactoryContext, IElementLineContext {
		public Compositor Compositor { get; private set; }
		public Vector2 Start {get; private set;}
		public Vector2 End { get; private set;}
		public LineGeometryContext(Compositor compositor, Vector2 start, Vector2 end) {
			Compositor = compositor;
			Start = start;
			End = end;
		}
	}
	#endregion
	#region PathGeometryContext
	/// <summary>
	/// Context for creating paths.
	/// </summary>
	public class PathGeometryContext : CategoryValueContext, IElementCompositionPath {
		public PathGeometryContext(Compositor cx, ISeriesItemCategoryValue isicv, Axis_Extents ca, Axis_Extents va, ItemTransition it, CompositionPath path) : base(cx, isicv, ca, va, it) {
			Path = path;
		}
		/// <summary>
		/// <inheritdoc/>
		/// </summary>
		public CompositionPath Path { get; private set; }
	}
	#endregion
	#region IElementFactory
	/// <summary>
	/// Ability to create composition elements for given series types.
	/// </summary>
	public interface IElementFactory {
		/// <summary>
		/// Create the sprite.
		/// </summary>
		/// <param name="iefc">Access to all.</param>
		/// <returns>New instance.</returns>
		CompositionShape CreateElement(IElementFactoryContext iefc);
	}
	#endregion
	#region IAnimationController
	public interface IAnimationController : IDisposable {
		/// <summary>
		/// Initialize transform components before first use of <see cref="Transform(IElementFactoryContext, Matrix3x2)"/>.
		/// This prevents an initial animation from Identity Matrix to the first Model.
		/// SHOULD call this ONCE per lifetime.
		/// </summary>
		/// <param name="model">Use to initialize animation properties.</param>
		void InitTransform(Matrix3x2 model);
		/// <summary>
		/// Animate the TransformMatrix to the given value.
		/// SHOULD only call when the transform has actually changed.
		/// </summary>
		/// <param name="iefc">Element context.</param>
		/// <param name="model">New model transform.</param>
		void Transform(IElementFactoryContext iefc, Matrix3x2 model);
		/// <summary>
		/// Add to VT and start the Enter animation.
		/// Connect TransformMatrix expression animation.
		/// </summary>
		/// <param name="key">Enter,Exit.</param>
		/// <param name="iefc">Element context.</param>
		/// <param name="ssc">Container collection to manage VT.</param>
		/// <param name="co">Object to animate.</param>
		/// <param name="cb">Callback to act on the <paramref name="co"/> after it enters the VT.</param>
		/// <returns>true: animation activated; false: no action caller MUST manage manually.</returns>
		bool Enter(IElementFactoryContext iefc, CompositionObject co, CompositionShapeCollection ssc, Action<CompositionObject> cb = null);
		/// <summary>
		/// Start the Exit animation and remove from VT when complete.
		/// Disconnect TransformMatrix expression animation.
		/// </summary>
		/// <param name="iefc">Element context.</param>
		/// <param name="co">Object to animate.</param>
		/// <param name="ssc">Container collection to manage VT.</param>
		/// <param name="cb">Callback to act on the <paramref name="co"/> after it exits the VT.</param>
		/// <returns>true: animation activated; false: no action caller MUST manage manually.</returns>
		bool Exit(IElementFactoryContext iefc, CompositionObject co, CompositionShapeCollection ssc, Action<CompositionObject> cb = null);
		/// <summary>
		/// Start the Offset animation.
		/// </summary>
		/// <param name="iefc">Element context.</param>
		/// <param name="co">Object to animate.</param>
		/// <param name="cb">Callback.</param>
		/// <returns>true: animation activated; false: no action caller MUST manage manually.</returns>
		bool Offset(IElementFactoryContext iefc, CompositionObject co, Action<CompositionObject> cb = null);
		ImplicitAnimationCollection CreateImplcit(IElementFactoryContext iefc);
	}
	#endregion
	#region IAnimationFactory
	/// <summary>
	/// Ability to animate series elements.
	/// </summary>
	public interface IAnimationFactory {
		/// <summary>
		/// Obtain a controller.
		/// </summary>
		/// <param name="cc">Use to create objects.</param>
		/// <returns>New instance.</returns>
		IAnimationController CreateAnimationController(Compositor cc);
	}
	#endregion
}
