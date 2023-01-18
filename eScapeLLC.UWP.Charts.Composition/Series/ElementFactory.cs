using eScapeLLC.UWP.Charts.Composition.Events;
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
	/// <summary>
	/// Additional information for creating rectangle geometry.
	/// </summary>
	public interface IElementRectangleContext {
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
	/// Additional information about the current value's data for this sprite.
	/// </summary>
	public interface IElementCategoryValueContext {
		/// <summary>
		/// Information about category axis.
		/// </summary>
		Axis_Extents CategoryAxis { get; }
		/// <summary>
		/// Information about value axis.
		/// </summary>
		Axis_Extents ValueAxis { get; }
		/// <summary>
		/// The category (index) value.
		/// </summary>
		int Category { get; }
		/// <summary>
		/// The category offset for placement of geometry.
		/// </summary>
		double CategoryOffset { get; }
		/// <summary>
		/// The data value.
		/// </summary>
		double Value { get; }
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
	/// Default context for basic use case in category/value scenario.
	/// </summary>
	public class CategoryValueContext : IElementFactoryContext, IElementCategoryValueContext {
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
		/// <summary>
		/// The category (index) value.
		/// </summary>
		public int Category { get; private set; }
		/// <summary>
		/// The category offset for placement of geometry.
		/// </summary>
		public double CategoryOffset { get; private set; }
		/// <summary>
		/// The data value.
		/// </summary>
		public double Value { get; private set; }
		public CategoryValueContext(Compositor cx, int cc, double oo, double vv, Axis_Extents ca, Axis_Extents va) {
			Compositor = cx;
			Category = cc;
			CategoryOffset = oo;
			Value = vv;
			CategoryAxis = ca;
			ValueAxis = va;
		}
	}
	/// <summary>
	/// Context for creating bars.
	/// </summary>
	public class ColumnElementContext : CategoryValueContext, IElementRectangleContext {
		public ColumnElementContext(Compositor cx, int cc, double oo, double vv, double ww, double hh, Axis_Extents ca, Axis_Extents va) :base(cx, cc, oo, vv, ca, va) {
			Width = ww;
			Height = hh;
		}
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
		public PathGeometryContext(Compositor cx, int cc, double oo, double vv, Axis_Extents ca, Axis_Extents va, CompositionPath path) : base(cx, cc, oo, vv, ca, va) {
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
}
