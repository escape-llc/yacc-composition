using eScapeLLC.UWP.Charts.Composition.Events;
using System;
using System.ComponentModel;
using Windows.UI.Composition;

namespace eScapeLLC.UWP.Charts.Composition {
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
	/// <summary>
	/// Ability to animate series elements.
	/// </summary>
	public interface IAnimationFactory {
		/// <summary>
		/// Prepare resources.
		/// </summary>
		/// <param name="cc">Use to acquire resources.</param>
		void Prepare(Compositor cc);
		/// <summary>
		/// Release resources.
		/// </summary>
		/// <param name="cc"></param>
		void Unprepare(Compositor cc);
		/// <summary>
		/// Start the indicated animation (sequence).
		/// </summary>
		/// <param name="key">Transform,Offset.</param>
		/// <param name="iefc">Element context.</param>
		/// <param name="co">Object to animate.</param>
		/// <param name="cfg">Callback to act on the <paramref name="co"/>.  Enter: add to VT.  Exit: remove from VT. Transform: configure animation.</param>
		void StartAnimation(string key, IElementFactoryContext iefc, CompositionObject co, Action<CompositionAnimation> cfg = null);
		/// <summary>
		/// Overload for Enter and Exit animation.
		/// </summary>
		/// <param name="key">Enter,Exit.</param>
		/// <param name="iefc">Element context.</param>
		/// <param name="ssc">Container collection to manage VT.</param>
		/// <param name="co">Object to animate.</param>
		/// <param name="cb">Callback to act on the <paramref name="co"/> after it enters/leaves the VT.</param>
		void StartAnimation(string key, IElementFactoryContext iefc, CompositionShapeCollection ssc, CompositionObject co, Action<CompositionObject> cb = null);
		ImplicitAnimationCollection CreateImplcit(IElementFactoryContext iefc);
	}
}
