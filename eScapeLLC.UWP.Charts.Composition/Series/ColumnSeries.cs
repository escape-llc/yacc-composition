﻿using eScape.Core;
using eScape.Host;
using eScapeLLC.UWP.Charts.Composition.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;

namespace eScapeLLC.UWP.Charts.Composition {
	/// <summary>
	/// CompositionShapeContainer(proj) -> .Shapes [CompositionSpriteShape(model) ...]
	/// Container takes the P matrix, Shapes each take the (same) M matrix.
	/// </summary>
	public class ColumnSeries : CategoryValueSeries,
		IRequireEnterLeave, IProvideSeriesItemValues, IProvideSeriesItemLayout, IListController<ColumnSeries.Series_ItemState>,
		IConsumer<Phase_RenderTransforms> {
		static LogTools.Flag _trace = LogTools.Add("ColumnSeries", LogTools.Level.Error);
		#region inner
		/// <summary>
		/// Item state.
		/// </summary>
		public class Series_ItemState : ItemState_CategoryValue<CompositionShape> {
			public Series_ItemState(int index, double categoryOffset, double value) : base(index, categoryOffset, value) {
			}
			public void Reindex(int idx) { Index = idx; }
		}
		/// <summary>
		/// Label placement session.
		/// </summary>
		class Series_LayoutSession : LayoutSession {
			readonly double width;
			readonly AxisOrientation c1axis;
			readonly AxisOrientation c2axis;
			internal Series_LayoutSession(Matrix3x2 model, Matrix3x2 projection, double width, AxisOrientation c1axis, AxisOrientation c2axis) : base(model, projection) {
				this.width = width;
				this.c1axis = c1axis;
				this.c2axis = c2axis;
			}
			public override (Vector2 center, Point direction)? Layout(ISeriesItem isi, Point offset) {
				if(isi is Series_ItemState sis) {
					var invert = sis.DataValue < 0 ? -1 : 1;
					double hw = width / 2.0, hh = Math.Abs(sis.DataValue / 2.0);
					var (xx, yy) = MappingSupport.MapComponents(sis.Component1 + hw + offset.X * hw, c1axis, (sis.DataValue / 2.0) + offset.Y * hh * invert, c2axis);
					var (dx, dy) = MappingSupport.MapComponents(1, c1axis, sis.DataValue > 0 ? 1 : -1, c2axis);
					var center = new Vector2((float)xx, (float)yy);
					return (Project(center), new Point(dx, dy));
				}
				return null;
			}
		}
		#endregion
		#region ctor
		public ColumnSeries() {
			ItemState = new List<ItemStateCore>();
		}
		#endregion
		#region properties
		/// <summary>
		/// Fractional offset into the "cell" of the category axis.
		/// BarOffset + BarWidth &lt;= 1.0
		/// </summary>
		public double BarOffset { get; set; } = 0.25;
		/// <summary>
		/// Fractional width in the "cell" of the category axis.
		/// BarOffset + BarWidth &lt;= 1.0
		/// </summary>
		public double BarWidth { get; set; } = 0.5;
		/// <summary>
		/// Return current state as read-only.
		/// </summary>
		public IEnumerable<ISeriesItem> SeriesItemValues => ItemState.AsReadOnly();
		#endregion
		#region internal
		protected IChartCompositionLayer Layer { get; set; }
		/// <summary>
		/// Maintained from axis extents.
		/// </summary>
		protected Matrix3x2 Model { get; set; }
		/// <summary>
		/// Data needed for current state.
		/// </summary>
		protected List<ItemStateCore> ItemState { get; set; }
		/// <summary>
		/// Holds all the shapes for this series.
		/// </summary>
		protected CompositionContainerShape Container { get; set; }
		#endregion
		#region helpers
		IEnumerable<(ItemStatus st, Series_ItemState state)> Entering(System.Collections.IList items) {
			for (int ix = 0; ix < items.Count; ix++) {
				var state = CreateState(Container.Compositor, ix, items[ix]);
				yield return (ItemStatus.Enter, state);
			}
		}
		void UpdateOffset(Series_ItemState item) {
			if (item.Element != null) {
				var offset = item.OffsetForColumn(CategoryAxis.Orientation, ValueAxis.Orientation);
				_trace.Verbose($"{Name}[{item.Index}] update-offset val:{item.DataValue} from:{item.Element.Offset.X},{item.Element.Offset.Y} to:{offset.X},{offset.Y}");
				if (AnimationFactory != null) {
					var ctx = new CategoryValueContext(Container.Compositor, item.Index, item.CategoryOffset, item.Component2, CategoryAxis, ValueAxis);
					AnimationFactory.StartAnimation("Offset", ctx, item.Element);
				}
				else {
					item.Element.Offset = offset;
				}
			}
		}
		#endregion
		#region IListController<>
		void IListController<Series_ItemState>.LiveItem(int index, Series_ItemState state) {
			state.Reindex(index);
			bool elementSelected = IsSelected(state);
			if (elementSelected && state.Element == null) {
				UpdateLimits(index, state.DataValue, 0);
				state.SetElement(CreateShape(Container.Compositor, index, state.DataValue));
				Entering(state);
				UpdateStyle(state);
				UpdateOffset(state);
			}
			else if (!elementSelected && state.Element != null) {
				Exiting(state);
			}
			else {
				UpdateLimits(index, state.DataValue, 0);
				UpdateStyle(state);
				UpdateOffset(state);
			}
		}
		void IListController<Series_ItemState>.EnteringItem(int index, Series_ItemState state) {
			state.Reindex(index);
			bool elementSelected2 = IsSelected(state);
			if (elementSelected2) {
				UpdateLimits(index, state.DataValue, 0);
				state.SetElement(CreateShape(Container.Compositor, index, state.DataValue));
				Entering(state);
				UpdateStyle(state);
				UpdateOffset(state);
			}
		}
		void IListController<Series_ItemState>.ExitingItem(int index, Series_ItemState state) {
			if (state.Element != null) {
				Exiting(state);
			}
		}
		#endregion
		#region extension points
		/// <summary>
		/// Create some state or NULL.
		/// </summary>
		/// <param name="cx"></param>
		/// <param name="index"></param>
		/// <param name="item"></param>
		/// <returns>NULL or new instance.</returns>
		protected virtual Series_ItemState CreateState(Compositor cx, int index, object item) {
			if (ValueBinding.GetDouble(item, out double? value_val)) {
				// short-circuit if it's NaN or NULL
				if (!value_val.HasValue || double.IsNaN(value_val.Value)) {
					return null;
				}
				var istate = new Series_ItemState(index, BarOffset, value_val.Value);
				_trace.Verbose($"{Name}[{index}] create-state val:{istate.DataValue}");
				return istate;
			}
			return null;
		}
		/// <summary>
		/// Create shape with <see cref="ElementFactory"/>.
		/// </summary>
		/// <param name="cx"></param>
		/// <param name="index"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		protected virtual CompositionShape CreateShape(Compositor cx, int index, double value) {
			var (xx, yy) = MappingSupport.MapComponents(BarWidth, CategoryAxis.Orientation, Math.Abs(value), ValueAxis.Orientation);
			var ctx = new ColumnElementContext(cx, index, BarOffset, value, xx, yy, CategoryAxis, ValueAxis);
			var element = ElementFactory.CreateElement(ctx);
			element.Comment = $"{Name}[{index}]";
			_trace.Verbose($"{Name}[{index}] create-shape val:{value} dim:{xx:F2},{yy:F2}");
			return element;
		}
		/// <summary>
		/// Item is entering the list.
		/// </summary>
		/// <param name="item"></param>
		protected virtual void Entering(Series_ItemState item) {
			if (item != null && item.Element != null) {
				// entry location depends on which "end" we are entering
				//var offset = item.OffsetForColumn(CategoryAxis.Orientation, ValueAxis.Orientation);
				//item.Element.Offset = new Vector2(-1, offset.Y);
				//Container.Shapes.Add(item.Element);
				if (AnimationFactory != null) {
					var ctx = new CategoryValueContext(Container.Compositor, item.Index, item.CategoryOffset, item.Component2, CategoryAxis, ValueAxis);
					AnimationFactory.StartAnimation("Enter", ctx, item.Element, ca => {
						Container.Shapes.Add(item.Element);
					});
				}
				else {
					Container.Shapes.Add(item.Element);
				}
			}
		}
		/// <summary>
		/// Item is exiting the list.
		/// </summary>
		/// <param name="item"></param>
		protected virtual void Exiting(Series_ItemState item) {
			if (item != null && item.Element != null) {
				if (AnimationFactory != null) {
					var ctx = new CategoryValueContext(Container.Compositor, item.Index, item.CategoryOffset, item.Component2, CategoryAxis, ValueAxis);
					AnimationFactory.StartAnimation("Exit", ctx, item.Element, ca => {
						Container.Shapes.Remove(item.Element);
						item.ResetElement();
					});
				}
				else {
					Container.Shapes.Remove(item.Element);
					item.ResetElement();
				}
			}
		}
		protected virtual void UpdateStyle(Series_ItemState item) {
		}
		protected virtual bool IsSelected(Series_ItemState item) {
			return true;
		}
		/// <summary>
		/// Core part of the update cycle.
		/// </summary>
		/// <param name="items">Sequence of item states.</param>
		protected virtual void UpdateCore(IEnumerable<(ItemStatus st, Series_ItemState state)> items) {
			var itemstate = new List<ItemStateCore>();
			ResetLimits();
			Model = Matrix3x2.Identity;
			ProcessItems<Series_ItemState>(items, this, itemstate);
			UpdateLimits(itemstate.Count);
			ItemState = itemstate;
		}
		#endregion
		#region data operation extensions
		protected override void Add(DataSource_Add add) {
			if (Container == null) return;
			// axes are not checked for by super class
			if (CategoryAxis == null) return;
			if (ValueAxis == null) return;
			IEnumerable<(ItemStatus st, Series_ItemState state)> live = ItemState.Select(xx => (ItemStatus.Live, xx as Series_ItemState));
			IEnumerable<(ItemStatus st, Series_ItemState state)> enter = Entering(add.NewItems);
			IEnumerable<(ItemStatus st, Series_ItemState state)> final = add.AtFront ? enter.Concat(live) : live.Concat(enter);
			UpdateCore(final);
		}
		protected override void Remove(DataSource_Remove remove) {
			if (Container == null) return;
			// axes are not checked for by super class
			if (CategoryAxis == null) return;
			if (ValueAxis == null) return;
			if(remove.AtFront) {
				IEnumerable<(ItemStatus st, Series_ItemState state)> exit = ItemState.Take(remove.Count).Select(xx => (ItemStatus.Exit, xx as Series_ItemState));
				IEnumerable<(ItemStatus st, Series_ItemState state)> live = ItemState.Skip(remove.Count).Select(xx => (ItemStatus.Live, xx as Series_ItemState));
				UpdateCore(exit.Concat(live));
			}
			else {
				IEnumerable<(ItemStatus st, Series_ItemState state)> live = ItemState.Take(ItemState.Count - remove.Count).Select(xx => (ItemStatus.Live, xx as Series_ItemState));
				IEnumerable<(ItemStatus st, Series_ItemState state)> exit = ItemState.Skip(ItemState.Count - remove.Count).Select(xx => (ItemStatus.Exit, xx as Series_ItemState));
				UpdateCore(live.Concat(exit));
			}
		}
		protected override void SlidingWindow(DataSource_SlidingWindow slidingWindow) {
			if (Container == null) return;
			// axes are not checked for by super class
			if (CategoryAxis == null) return;
			if (ValueAxis == null) return;
			IEnumerable<(ItemStatus st, Series_ItemState state)> exit = ItemState.Take(slidingWindow.NewItems.Count).Select(xx => (ItemStatus.Exit, xx as Series_ItemState));
			IEnumerable<(ItemStatus st, Series_ItemState state)> live = ItemState.Skip(slidingWindow.NewItems.Count).Select(xx => (ItemStatus.Live, xx as Series_ItemState));
			IEnumerable<(ItemStatus st, Series_ItemState state)> enter = Entering(slidingWindow.NewItems);
			UpdateCore(exit.Concat(live).Concat(enter));
		}
		protected override void Reset(DataSource_Reset dsr) {
			if (Container == null) return;
			// axes are not checked for by super class
			if (CategoryAxis == null) return;
			if (ValueAxis == null) return;
			IEnumerable<(ItemStatus st, Series_ItemState state)> exit = ItemState.Select(xx => (ItemStatus.Exit, xx as Series_ItemState));
			IEnumerable<(ItemStatus st, Series_ItemState state)> enter = Entering(dsr.Items);
			UpdateCore(exit.Concat(enter));
		}
		protected override void UpdateModelTransform() {
			if (CategoryAxis == null || ValueAxis == null) return;
			if (double.IsNaN(CategoryAxis.Minimum) || double.IsNaN(CategoryAxis.Maximum)) return;
			if (double.IsNaN(ValueAxis.Minimum) || double.IsNaN(ValueAxis.Maximum)) return;
			if (CategoryAxis.Orientation == AxisOrientation.Horizontal) {
				Model = MatrixSupport.ModelFor(CategoryAxis.Minimum, CategoryAxis.Maximum, ValueAxis.Minimum, ValueAxis.Maximum);
			}
			else {
				Model = MatrixSupport.ModelFor(ValueAxis.Minimum, ValueAxis.Maximum, CategoryAxis.Minimum, CategoryAxis.Maximum);
			}
			foreach(Series_ItemState item in ItemState) {
				// apply new model transform
				if (item != null && item.Element != null) {
					if (AnimationFactory != null) {
						var ctx = new DefaultContext(Container.Compositor, CategoryAxis, ValueAxis);
						AnimationFactory.StartAnimation("Transform", ctx, item.Element, cc => {
							// doesn't really work
							cc.Properties.InsertVector3("Component1", new Vector3(Model.M11, Model.M21, Model.M31));
							cc.Properties.InsertVector3("Component2", new Vector3(Model.M12, Model.M22, Model.M32));
						});
					}
					else {
						item.Element.TransformMatrix = Model;
					}
				}
			}
		}
		#endregion
		#region handlers
		/// <summary>
		/// Render area participates in the Projection transform.
		/// </summary>
		/// <param name="message"></param>
		void IConsumer<Phase_RenderTransforms>.Consume(Phase_RenderTransforms message) {
			if (CategoryAxis == null || ValueAxis == null) return;
			if (ItemState.Count == 0) return;
			if (Container == null) return;
			var rctx = message.ContextFor(this);
			var xaxis = CategoryAxis.Orientation == AxisOrientation.Horizontal ? CategoryAxis.Reversed : ValueAxis.Reversed;
			var yaxis = CategoryAxis.Orientation == AxisOrientation.Vertical ? CategoryAxis.Reversed : ValueAxis.Reversed;
			var q = MatrixSupport.QuadrantFor(!xaxis, !yaxis);
			var proj = MatrixSupport.ProjectForQuadrant(q, rctx.SeriesArea);
			Container.TransformMatrix = proj;
		}
		#endregion
		#region IRequireEnterLeave
		public void Enter(IChartEnterLeaveContext icelc) {
			EnsureAxes(icelc as IChartComponentContext);
			EnsureValuePath(icelc as IChartComponentContext);
			var icei = icelc as IChartErrorInfo;
			if (ElementFactory == null) {
				icei?.Report(new ChartValidationResult(NameOrType(), $"Property '{nameof(ElementFactory)}' was not set", new[] { nameof(ElementFactory) }));
			}
			Compositor compositor = Window.Current.Compositor;
			Container = compositor.CreateContainerShape();
			Container.Comment = $"container_{Name}";
			Layer = icelc.CreateCompositionLayer(Container);
			if(AnimationFactory != null) {
				AnimationFactory.Prepare(compositor);
			}
			_trace.Verbose($"{Name} enter v:{ValueAxisName} {ValueAxis} c:{CategoryAxisName} {CategoryAxis} d:{DataSourceName}");
		}
		public void Leave(IChartEnterLeaveContext icelc) {
			_trace.Verbose($"{Name} leave");
			if (AnimationFactory != null) {
				AnimationFactory.Unprepare(Window.Current.Compositor);
			}
			Container = null;
			icelc.DeleteCompositionLayer(Layer);
			Layer = null;
		}
		#endregion
		#region IProvideSeriesItemLayout
		public ILayoutSession Create(Rect area) {
			var xaxis = CategoryAxis.Orientation == AxisOrientation.Horizontal ? CategoryAxis.Reversed : ValueAxis.Reversed;
			var yaxis = CategoryAxis.Orientation == AxisOrientation.Vertical ? CategoryAxis.Reversed : ValueAxis.Reversed;
			var q = MatrixSupport.QuadrantFor(!xaxis, !yaxis);
			var proj = MatrixSupport.ProjectForQuadrant(q, area);
			return new Series_LayoutSession(Model, proj, BarWidth, CategoryAxis.Orientation, ValueAxis.Orientation);
		}
		#endregion
	}
}
