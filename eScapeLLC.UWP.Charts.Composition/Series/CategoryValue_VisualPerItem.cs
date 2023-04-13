using eScape.Core;
using Windows.UI.Composition;

namespace eScapeLLC.UWP.Charts.Composition {
	#region CategoryValue_VisualPerItem<S>
	/// <summary>
	/// This subclass commits to Category/Value type with one <see cref="Visual"/> per item state, e.g. Image Marker.
	/// This is because some series require a <see cref="Visual"/> instead of a <see cref="CompositionShape"/>.
	/// </summary>
	/// <typeparam name="S">Item type. MUST NOT be an Inner Class!</typeparam>
	public abstract class CategoryValue_VisualPerItem<S> : CategoryValueSeries<S>, IOperationController<S> where S : ItemState_CategoryValue<Visual> {
		static readonly LogTools.Flag _trace = LogTools.Add("CategoryValue_VisualPerItem", LogTools.Level.Error);
		#region internal
		/// <summary>
		/// Composition layer.
		/// </summary>
		protected IChartCompositionLayer Layer { get; set; }
		/// <summary>
		/// Holds all the visuals for this series.
		/// </summary>
		protected ContainerVisual Container { get; set; }
		#endregion
		#region extension points
		/// <summary>
		/// Return whether this item is selected for display.
		/// Default implementation returns TRUE.
		/// </summary>
		/// <param name="item">Target item.</param>
		/// <returns>true: display; false: no display.</returns>
		protected virtual bool IsSelected(S item) => true;
		/// <summary>
		/// Factory method for visual elements.
		/// </summary>
		/// <param name="cx"></param>
		/// <param name="item"></param>
		/// <returns>New instance.</returns>
		protected abstract Visual CreateVisual(Compositor cx, S item);
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
		protected abstract void Entering(S item, ItemTransition it);
		/// <summary>
		/// Item is exiting the chart.
		/// </summary>
		/// <param name="item">Exiting item.</param>
		/// <param name="it">Transition info.</param>
		protected abstract void Exiting(S item, ItemTransition it);
		/// <summary>
		/// Update the item element's <see cref="Visual.Offset"/>.
		/// </summary>
		/// <param name="item">Current item MAY be moving to new position.</param>
		protected abstract void UpdateOffset(S item);
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
				state.SetElement(CreateVisual(Container.Compositor, state));
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
				state.SetElement(CreateVisual(Container.Compositor, state));
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
