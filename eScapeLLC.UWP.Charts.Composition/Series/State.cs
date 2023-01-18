using System;
using System.Collections.Generic;
using System.Numerics;
using Windows.UI.Composition;
using Windows.UI.Xaml;

namespace eScapeLLC.UWP.Charts.Composition {
	#region item states
	/// <summary>
	/// Items are generically described as a "vector" of components, 1-based.
	/// These are not mapped to any particular coordinate axis, e.g. Component1 MAY be mapped to vertical (Y) or horizontal (X) cartesian coordinates.
	/// </summary>
	public abstract class ItemStateCore {
		/// <summary>
		/// The index of this value from data source.
		/// </summary>
		public int Index { get; private set; }
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
	}
	/// <summary>
	/// Suitable for C_1(Value), e.g. Horizontal/Vertical Value Rule that spans series area.
	/// Represents a single value; index is not part of the data.
	/// </summary>
	public class ItemStateC1 : ItemStateCore {
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
	public class ItemStateRange : ItemStateCore {
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
	public class ItemStateC2 : ItemStateCore {
		/// <summary>
		/// Alias for the <see cref="Index"/>.
		/// </summary>
		public virtual double Component1 => Index;
		/// <summary>
		/// The C_2 value.
		/// </summary>
		public double Component2 { get; private set; }
		public ItemStateC2(int index, double c2) :base(index) {
			Component2 = c2;
		}
		public override double[] Components() => new double[] { Component1, Component2 };
	}
	/// <summary>
	/// Suitable for C_1(Index + CategoryOffset), C_2(Value) series.
	/// This applies to all series that track a single value with an index.
	/// </summary>
	/// <typeparam name="C">Composition shape element type.</typeparam>
	public class ItemState_CategoryValue<C> : ItemStateC2 where C: CompositionObject {
		public readonly C Element;
		public ItemState_CategoryValue(int index, double categoryOffset, double c2, C element) : base(index, c2) {
			CategoryOffset = categoryOffset;
			Element = element;	
		}
		/// <summary>
		/// Additional offset in the category component.
		/// Category axis allocates "slots" that are integer indexed.
		/// </summary>
		public double CategoryOffset { get; private set; }
		/// <summary>
		/// Redefine to include the <see cref="CategoryOffset"/>.
		/// </summary>
		public override double Component1 => Index + CategoryOffset;
		/// <summary>
		/// Calculate offset for Column series sprite.
		/// If the value is negative, adjust the vertical offset by that amount.
		/// </summary>
		/// <param name="cori">Category axis orientation.</param>
		/// <param name="vori">Value axis orientation.</param>
		/// <returns>Value to use for the Offset.</returns>
		/// <exception cref="ArgumentException"></exception>
		public Vector2 OffsetForColumn(AxisOrientation cori, AxisOrientation vori) {
			if (cori == vori) throw new ArgumentException($"Orientations are equal {cori}");
			var (xx, yy) = MappingSupport.MapComponents(Component1, Math.Min(Component2, 0), cori, vori);
			return new Vector2((float)xx, (float)yy);
		}
		/// <summary>
		/// Calculate offset for Marker series sprite.
		/// Offsets to the point (C_1,C_2) exactly.
		/// Sprite MUST be able to keep itself centered based on its current size.
		/// </summary>
		/// <param name="cori">Category axis orientation.</param>
		/// <param name="vori">Value axis orientation.</param>
		/// <returns>Value to use for the Offset.</returns>
		/// <exception cref="ArgumentException"></exception>
		public Vector2 OffsetForMarker(AxisOrientation cori, AxisOrientation vori) {
			if (cori == vori) throw new ArgumentException($"Orientations are equal {cori}");
			var (xx, yy) = MappingSupport.MapComponents(Component1, Component2, cori, vori);
			return new Vector2((float)xx, (float)yy);
		}
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
		public Vector2 OffsetForMarker(AxisOrientation cori, AxisOrientation vori) {
			if (cori == vori) throw new ArgumentException($"Orientations are equal {cori}");
			var (xx, yy) = MappingSupport.MapComponents(Component1, Component2, cori, vori);
			return new Vector2((float)xx, (float)yy);
		}
	}
	/// <summary>
	/// Suitable for C_1(Index), C_2(Index2), C_3(Value) series, e.g. Heatmap.
	/// </summary>
	/// <typeparam name="C"></typeparam>
	public class ItemState_CategoryCategoryValue<C> : ItemStateCore where C : CompositionShape {
		public readonly C Element;

		public ItemState_CategoryCategoryValue(int index, int index2, double c3, C element) : base(index) {
			Index2 = index2;
			Component3 = c3;
			Element = element;
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
		public override double[] Components() => new double[] { Component1, Component2, Component3 };
		public Vector2 OffsetForMarker(AxisOrientation cori, AxisOrientation vori) {
			if (cori == vori) throw new ArgumentException($"Orientations are equal {cori}");
			var (xx, yy) = MappingSupport.MapComponents(Component1, Component2, cori, vori);
			return new Vector2((float)xx, (float)yy);
		}
	}
	#endregion
	#region render states
	#region RenderStateCore<SIS>
	/// <summary>
	/// Render state core.
	/// </summary>
	/// <typeparam name="SIS">State item class.</typeparam>
	public class RenderStateCore<SIS> where SIS : ItemStateCore {
		/// <summary>
		/// Tracks the index from Render().
		/// </summary>
		public int ix;
		/// <summary>
		/// Collects the item states created in Render().
		/// Transfer to host in Postamble().
		/// </summary>
		public readonly List<ItemStateCore> itemstate;
		/// <summary>
		/// The recycler's iterator to generate the elements.
		/// </summary>
		/// <summary>
		/// Ctor.
		/// </summary>
		/// <param name="state">Starting state; SHOULD be empty.</param>
		public RenderStateCore(List<ItemStateCore> state) {
			itemstate = state;
		}
	}
	#endregion
	#region RenderState_ShapeContainer<SIS>
	/// <summary>
	/// Render state with a <see cref="CompositionContainerShape"/> to hold the elements.
	/// Container element SHOULD hold the P matrix, and its children the M matrix.
	/// </summary>
	/// <typeparam name="SIS">State item class.</typeparam>
	public class RenderState_ShapeContainer<SIS> : RenderStateCore<SIS> where SIS: ItemStateCore {
		public readonly CompositionContainerShape container;
		public readonly Compositor compositor = Window.Current.Compositor;
		public RenderState_ShapeContainer(List<ItemStateCore> state) : base(state) {
			container = compositor.CreateContainerShape();
			container.Comment = $"container_{typeof(SIS).Name}";
		}
		/// <summary>
		/// Add to the state.
		/// </summary>
		/// <param name="istate">Item state.</param>
		/// <param name="element">Corresponding composition element; MAY be NULL.</param>
		public void Add(ItemStateCore istate, CompositionShape element) {
			if (element != null) {
				container.Shapes.Add(element);
			}
			itemstate.Add(istate);
		}
	}
	#endregion
	#endregion
}
