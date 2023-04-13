using eScape.Core;
using System.Numerics;
using Windows.UI.Composition;

namespace eScapeLLC.UWP.Charts.Composition {
	#region CategoryValue_ShapePerItem<S>
	/// <summary>
	/// Commit to more constraints.
	/// This subclass commits to Category/Value type with one <see cref="CompositionShape"/> per item state, e.g. Column, Marker.
	/// This codifies the operation controller logic and associated methods as virtual with default implementations.
	/// </summary>
	/// <typeparam name="S">Item type. MUST NOT be an Inner Class!</typeparam>
	public abstract class CategoryValue_ShapePerItem<S> : CategoryValueSeries<S>, IOperationController<S> where S : ItemState_CategoryValue<CompositionShape> {
		static readonly LogTools.Flag _trace = LogTools.Add("CategoryValue_ShapePerItem", LogTools.Level.Error);
		#region properties
		/// <summary>
		/// How to create the elements for this series.
		/// </summary>
		public IElementFactory ElementFactory { get; set; }
		/// <summary>
		/// How to create animations for series and its elements.
		/// </summary>
		public IAnimationFactory AnimationFactory { get; set; }
		#endregion
		#region internal
		/// <summary>
		/// Composition layer.
		/// </summary>
		protected IChartCompositionLayer Layer { get; set; }
		/// <summary>
		/// Maintained from axis extents.
		/// </summary>
		protected Matrix3x2 Model { get; set; }
		/// <summary>
		/// Holds all the shapes for this series.
		/// </summary>
		protected CompositionContainerShape Container { get; set; }
		/// <summary>
		/// Created from the <see cref="IAnimationFactory"/> if given.
		/// </summary>
		protected IAnimationController Animate { get; set; }
		#endregion
		#region extension points
		/// <summary>
		/// Factory method for visual elements.
		/// </summary>
		/// <param name="cx"></param>
		/// <param name="item"></param>
		/// <returns>New element.</returns>
		protected abstract CompositionShape CreateShape(Compositor cx, S item);
		/// <summary>
		/// Provide a context for animation controller.
		/// </summary>
		/// <param name="item">Target item.</param>
		/// <param name="it">Transition info.</param>
		/// <returns>New instance.</returns>
		protected abstract IElementFactoryContext CreateAnimateContext(S item, ItemTransition it);
		/// <summary>
		/// Return whether this item is selected for display.
		/// Default implementation returns TRUE.
		/// </summary>
		/// <param name="item">Target item.</param>
		/// <returns>true: display; false: no display.</returns>
		protected virtual bool IsSelected(S item) => true;
		/// <summary>
		/// Apply any dynamic style updates.
		/// </summary>
		/// <param name="item">Target item.</param>
		protected virtual void UpdateStyle(S item) { }
		/// <summary>
		/// Item is entering the chart.
		/// </summary>
		/// <param name="item">Entering item.</param>
		/// <param name="it">Transition info.</param>
		protected virtual void Entering(S item, ItemTransition it) {
			if (item == null || item.Element == null) return;
			if (Animate != null) {
				var ctx = CreateAnimateContext(item, it);
				Animate.Enter(ctx, item.Element, Container.Shapes);
			}
			else {
				if (it == ItemTransition.Head) {
					Container.Shapes.Insert(0, item.Element);
				}
				else {
					Container.Shapes.Add(item.Element);
				}
			}
		}
		/// <summary>
		/// Item is exiting the chart.
		/// </summary>
		/// <param name="item">Exiting item.</param>
		/// <param name="it">Transition info.</param>
		protected virtual void Exiting(S item, ItemTransition it) {
			if (item == null || item.Element == null) return;
			if (Animate != null) {
				var ctx = CreateAnimateContext(item, it);
				Animate.Exit(ctx, item.Element, Container.Shapes, co => {
					item.ResetElement();
				});
			}
			else {
				Container.Shapes.Remove(item.Element);
				item.ResetElement();
			}
		}
		/// <summary>
		/// Update the item element's <see cref="CompositionShape.Offset"/>.
		/// </summary>
		/// <param name="item"></param>
		protected virtual void UpdateOffset(S item) {
			if (item.Element == null) return;
			if (Animate != null) {
				_trace.Verbose($"{Name}[{item.Index}] update-offset val:{item.DataValue} from:{item.Element.Offset.X},{item.Element.Offset.Y}");
				var ctx = CreateAnimateContext(item, ItemTransition.None);
				Animate.Offset(ctx, item.Element);
			}
			else {
				var offset = item.OffsetFor(CategoryAxis.Orientation, ValueAxis.Orientation);
				_trace.Verbose($"{Name}[{item.Index}] update-offset val:{item.DataValue} from:{item.Element.Offset.X},{item.Element.Offset.Y} to:{offset.X},{offset.Y}");
				item.Element.Offset = offset;
			}
		}
		#endregion
		#region IOperationController<S>
		/// <summary>
		/// Logic for a Live item.
		/// If item selection state changes, this MAY cause enter/exit logic to trigger.
		/// Item MAY move to a new location due to resequencing.
		/// </summary>
		/// <param name="index">Index allocated for this item.  MAY differ from current index.</param>
		/// <param name="it">Transition info.</param>
		/// <param name="state">Item state.</param>
		void IOperationController<S>.LiveItem(int index, ItemTransition it, S state) {
			_trace.Verbose($"{Name}.Live index:{index} it:{it} st[{state.Index}]:{state.DataValue} el:{state.Element}");
			state.Reindex(index);
			bool elementSelected = IsSelected(state);
			if (elementSelected && state.Element == null) {
				state.SetElement(CreateShape(Container.Compositor, state));
				Entering(state, it);
				UpdateStyle(state);
				UpdateOffset(state);
			}
			else if (!elementSelected && state.Element != null) {
				Exiting(state, it);
			}
			else {
				UpdateStyle(state);
				UpdateOffset(state);
			}
		}
		/// <summary>
		/// Logic for an Entering item.
		/// If item is selected, it enters the Visual Tree, and MAY trigger an Enter animation.
		/// </summary>
		/// <param name="index">Index allocated for this item.</param>
		/// <param name="it">Transition info.</param>
		/// <param name="state">Item state.</param>
		void IOperationController<S>.EnteringItem(int index, ItemTransition it, S state) {
			_trace.Verbose($"{Name}.Entering index:{index} it:{it} st[{state.Index}]:{state.DataValue} el:{state.Element}");
			state.Reindex(index);
			bool elementSelected = IsSelected(state);
			if (elementSelected) {
				state.SetElement(CreateShape(Container.Compositor, state));
				Entering(state, it);
				UpdateStyle(state);
				UpdateOffset(state);
			}
		}
		/// <summary>
		/// Logic for an Exiting item.
		/// Item MAY trigger an Exit animation, after which it is removed from Visual Tree.
		/// </summary>
		/// <param name="index">Not used.  Index of the exiting item.</param>
		/// <param name="it">Transition info.</param>
		/// <param name="state">Item state.</param>
		void IOperationController<S>.ExitingItem(int index, ItemTransition it, S state) {
			_trace.Verbose($"{Name}.Exiting index:{index} it:{it} st[{state.Index}]:{state.DataValue} el:{state.Element}");
			if (state.Element != null) {
				Exiting(state, it);
			}
		}
		#endregion
	}
	#endregion
}
