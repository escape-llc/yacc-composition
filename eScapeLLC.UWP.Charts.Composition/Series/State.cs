using System;
using System.Collections.Generic;
using System.Numerics;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;

namespace eScapeLLC.UWP.Charts.Composition {
	#region item state interfaces
	/// <summary>
	/// Used during bookkeeping operations to track item status.
	/// </summary>
	public enum ItemStatus {
		/// <summary>
		/// Item is "live" i.e. already tracking.
		/// </summary>
		Live,
		/// <summary>
		/// Item is entering Visual Tree.
		/// </summary>
		Enter,
		/// <summary>
		/// Item is exiting the Visual Tree.
		/// </summary>
		Exit,
	};
	/// <summary>
	/// Indicate which end of the list operation occurs.
	/// Only meaningful for Enter and Exit items.
	/// </summary>
	public enum ItemTransition {
	 None,
	 Head,
	 Tail
	}
	#region ISeriesItem
	/// <summary>
	/// Entry point to series item data.
	/// </summary>
	public interface ISeriesItem {
		/// <summary>
		/// The index.
		/// </summary>
		int Index { get; }
	}
	#endregion
	#region ISeriesItemValue/Double/Object
	/// <summary>
	/// Entry point to item values.
	/// </summary>
	public interface ISeriesItemValue {
		/// <summary>
		/// What "channel" this value is tracking.
		/// Value is host-dependent if tracking multiple values, else SHOULD be ZERO.
		/// </summary>
		int Channel { get; }
	}
	/// <summary>
	/// Double value channel.
	/// </summary>
	public interface ISeriesItemValueDouble : ISeriesItemValue {
		double DoubleValue { get; }
	}
	/// <summary>
	/// Custom object value channel.
	/// </summary>
	public interface ISeriesItemValueCustom : ISeriesItemValueDouble {
		object CustomValue { get; }
	}
	#endregion
	#region ISeriesItemCategoryValue
	/// <summary>
	/// Represents a "typical" data series item.
	/// </summary>
	public interface ISeriesItemCategoryValue : ISeriesItemValue {
		/// <summary>
		/// The category axis value for the <see cref="Index"/>.
		/// </summary>
		int CategoryValue { get; }
		/// <summary>
		/// Category axis offset.
		/// </summary>
		double CategoryOffset { get; }
		/// <summary>
		/// The data value.
		/// </summary>
		double DataValue { get; }
	}
	#endregion
	#region ISeriesItemValueValue
	public interface ISeriesItemValueValue : ISeriesItemValue {
		/// <summary>
		/// The data value.
		/// </summary>
		double Value1 { get; }
		/// <summary>
		/// The data value.
		/// </summary>
		double Value2 { get; }
	}
	#endregion
	#region ISeriesItemValues
	/// <summary>
	/// Item tracking multiple channels.
	/// </summary>
	public interface ISeriesItemValues {
		/// <summary>
		/// Enumerator to traverse the values.
		/// SHOULD order-by channel.
		/// </summary>
		IEnumerable<ISeriesItemValue> YValues { get; }
	}
	#endregion
	#region IProvideSeriesItemValues
	/// <summary>
	/// Ability to provide access to the current series item state.
	/// </summary>
	public interface IProvideSeriesItemValues {
		/// <summary>
		/// Enumerator to traverse the item values.
		/// SHOULD operate on a COPY of the actual underlying sequence.
		/// </summary>
		IEnumerable<ISeriesItem> SeriesItemValues { get; }
	}
	#endregion
	#region IProvideOriginalState
	/// <summary>
	/// Signal that this state item actually is wrapping a "stable" item.
	/// This is for components that dynamically "wrap" their internal state up each time it's requested.
	/// This is required for components that support incremental updates!
	/// </summary>
	public interface IProvideOriginalState {
		/// <summary>
		/// The Wrapped "stable" item this instance is wrapping.
		/// </summary>
		ISeriesItem Original { get; }
	}
	#endregion
	#region components
	/// <summary>
	/// Single component: C_1.
	/// </summary>
	public interface IComponentC1 {
		double Component1 { get; }
	}
	/// <summary>
	/// Double component: C_1, C_2.
	/// </summary>
	public interface IComponentC1C2 {
		double Component1 { get; }
		double Component2 { get; }
	}
	#endregion
	#endregion
	#region item state implementations
	/// <summary>
	/// Items are generically described as a "vector" of components, 1-based.
	/// These are not mapped to any particular coordinate axis, e.g. Component1 MAY be mapped to vertical (Y) or horizontal (X) cartesian coordinates.
	/// </summary>
	public abstract class ItemStateCore : ISeriesItem {
		/// <summary>
		/// The index of this value from data source.
		/// </summary>
		public int Index { get; protected set; }
		/// <summary>
		/// Ctor.
		/// </summary>
		/// <param name="index">Item index.</param>
		public ItemStateCore(int index) {
			Index = index;
		}
		/// <summary>
		/// Render components as an array.
		/// </summary>
		/// <returns></returns>
		public virtual double[] Components() => new double[] { Index };
		/// <summary>
		/// Reassign the index.
		/// </summary>
		/// <param name="idx">New index.</param>
		public void Reindex(int idx) { Index = idx; }
	}
	/// <summary>
	/// Suitable for C_1(Value), e.g. Horizontal/Vertical Value Rule that spans series area.
	/// Represents a single value; index is not part of the data.
	/// </summary>
	public class ItemStateC1 : ItemStateCore, IComponentC1 {
		/// <summary>
		/// The C_1 value.
		/// </summary>
		public double Component1 { get; private set; }
		public ItemStateC1(int index, double component1) : base(index) {
			Component1 = component1;
		}
		public override double[] Components() => new double[] { Component1 };
	}
	/// <summary>
	/// Suitable for C_1(Value), C_2(Value), e.g. min/max, e.g. Horizontal/Vertical Value Band that spans series area.
	/// Represents a single value; index is not part of the data.
	/// </summary>
	public class ItemStateRange : ItemStateCore, IComponentC1C2 {
		public ItemStateRange(int index, double c1, double c2) : base(index) {
			Component1 = c1;
			Component2 = c2;
		}
		public double Component1 { get; private set; }
		public double Component2 { get; private set; }
		public override double[] Components() => new double[] { Component1, Component2 };
	}
	/// <summary>
	/// Suitable for C_1(Index), C_2(Value) series.
	/// </summary>
	public class ItemStateC2 : ItemStateCore, ISeriesItemValueDouble, IComponentC1C2 {
		/// <summary>
		/// Alias for the <see cref="Index"/>.
		/// </summary>
		public virtual double Component1 => Index;
		/// <summary>
		/// The C_2 value.
		/// </summary>
		public double Component2 { get; private set; }
		public int Channel { get; private set; }
		double ISeriesItemValueDouble.DoubleValue => Component2;
		public ItemStateC2(int index, double c2, int channel = 0) :base(index) {
			Component2 = c2;
			Channel = channel;
		}
		public override double[] Components() => new double[] { Component1, Component2 };
	}
	/// <summary>
	/// Suitable for C_1(Index + CategoryOffset), C_2(Value) series.
	/// This applies to all series that track a single value with an index.
	/// </summary>
	/// <typeparam name="E">Element type.</typeparam>
	public abstract class ItemState_CategoryValue<E> : ItemStateC2, ISeriesItemValueDouble, ISeriesItemCategoryValue where E: CompositionObject {
		public E Element { get; protected set; }
		public ItemState_CategoryValue(int index, double categoryOffset, double c2, int channel = 0) : base(index, c2, channel) {
			CategoryOffset = categoryOffset;
		}
		#region properties
		/// <summary>
		/// Additional offset in the category component.
		/// Category axis allocates "slots" that are integer indexed.
		/// </summary>
		public double CategoryOffset { get; private set; }
		/// <summary>
		/// Redefine to include the <see cref="CategoryOffset"/>.
		/// </summary>
		public override double Component1 => Index + CategoryOffset;
		#endregion
		#region ISeriesItemCategoryValue
		public int CategoryValue => Index;
		public double DataValue => Component2;
		double ISeriesItemValueDouble.DoubleValue => Component2;
		#endregion
		#region public
		/// <summary>
		/// Release the element.
		/// </summary>
		public virtual void ResetElement() { Element?.Dispose(); Element = null; }
		/// <summary>
		/// Accept new element.
		/// </summary>
		/// <param name="el">New element.</param>
		public virtual void SetElement(E el) { Element?.Dispose(); Element = el; }
		/// <summary>
		/// Calculate offset for sprite.
		/// </summary>
		/// <param name="cori">Category axis orientation.</param>
		/// <param name="vori">Value axis orientation.</param>
		/// <returns>Value to use for the Offset.</returns>
		/// <exception cref="ArgumentException"></exception>
		public abstract Vector2 OffsetFor(AxisOrientation cori, AxisOrientation vori);
		#endregion
	}
	/// <summary>
	/// Suitable for C_1(Value1), C_2(Value2) series, e.g. Scatter Plot.
	/// This applies to all series that track two values; the Index is not part of the data.
	/// </summary>
	/// <typeparam name="C"></typeparam>
	public class ItemState_ValueValue<C> : ItemStateCore where C : CompositionShape {
		public readonly C Element;
		public ItemState_ValueValue(int index, double c1, double c2, C element) : base(index) {
			Element = element;
			Component1 = c1;
			Component2 = c2;
		}
		public double Component1 { get; private set; }
		public double Component2 { get; private set; }
		public override double[] Components() => new double[] { Component1, Component2 };
	}
	/// <summary>
	/// Suitable for C_1(Index), C_2(Index2), C_3(Value) series, e.g. Heatmap.
	/// </summary>
	/// <typeparam name="C"></typeparam>
	public class ItemState_CategoryCategoryValue<C> : ItemStateCore, ISeriesItemValueDouble where C : CompositionShape {
		public readonly C Element;

		public ItemState_CategoryCategoryValue(int index, int index2, double c3, C element, int channel = 0) : base(index) {
			Index2 = index2;
			Component3 = c3;
			Element = element;
			Channel = channel;
		}
		/// <summary>
		/// 2nd category index.
		/// </summary>
		public int Index2 { get; private set; }
		/// <summary>
		/// Alias to <see cref="Index"/>.
		/// </summary>
		public virtual double Component1 => Index;
		/// <summary>
		/// Alias to <see cref="Index2"/>.
		/// </summary>
		public virtual double Component2 => Index2;
		/// <summary>
		/// Value corresponding to C_1 and C_2.
		/// </summary>
		public double Component3 { get; private set; }
		public int Channel { get; private set; }
		double ISeriesItemValueDouble.DoubleValue => Component3;
		public override double[] Components() => new double[] { Component1, Component2, Component3 };
	}
	#endregion
	#region LayoutSession
	/// <summary>
	/// Base implementation for placement session.
	/// <para>
	/// A layout session represents a specific MP matrix combination.  The provider of the session chooses these based on current state.
	/// </para>
	/// <para>
	/// A provider of placement implements an inner <see cref="ILayoutSession"/>
	/// and receives its own <see cref="ISeriesItem"/>s back, then calculates placement based on its "inside knowledge" of the geometry represented by itself.
	/// </para>
	/// </summary>
	public abstract class LayoutSession : ILayoutSession {
		public readonly Matrix3x2 Product;
		/// <summary>
		/// Ctor.
		/// </summary>
		/// <param name="model">Model transform.</param>
		/// <param name="projection">Projection transform.</param>
		public LayoutSession(Matrix3x2 model, Matrix3x2 projection) {
			Product = Matrix3x2.Multiply(model, projection);
		}
		/// <summary>
		/// Take model coords and transform to PX.  This is the center point for placement.
		/// </summary>
		/// <param name="source">M coordinates.</param>
		/// <returns>Placement location in PX.</returns>
		protected Vector2 Project(Vector2 source) {
			return Vector2.Transform(source, Product);
		}
		/// <summary>
		/// Calculate placement information.
		/// </summary>
		/// <param name="isi">Source item.</param>
		/// <param name="offset">Placement offset (in M coordinates).</param>
		/// <returns>NULL: cannot calculate; !NULL: placement info.  center in PX; direction in PX for label placement.</returns>
		public abstract (Vector2 center, Point direction)? Layout(ISeriesItem isi, Point offset);
	}
	#endregion
}
