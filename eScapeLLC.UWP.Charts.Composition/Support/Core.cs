﻿using eScape.Host;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Numerics;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;

namespace eScapeLLC.UWP.Charts.Composition {
	#region AxisOrientation
	/// <summary>
	/// Allowed axis orientations.
	/// </summary>
	public enum AxisOrientation {
		/// <summary>
		/// Horizontal orientation.
		/// </summary>
		Horizontal,
		/// <summary>
		/// Vertical orientation.
		/// </summary>
		Vertical
	};
	#endregion
	#region AxisType
	/// <summary>
	/// Allowed axis types.
	/// </summary>
	public enum AxisType {
		/// <summary>
		/// First component (X-axis) value.
		/// </summary>
		Category,
		/// <summary>
		/// Second component (Y-axis) value.
		/// </summary>
		Value
	};
	#endregion
	#region Side
	/// <summary>
	/// Side to claim space from.
	/// </summary>
	public enum Side {
		/// <summary>
		/// Top.
		/// </summary>
		Top,
		/// <summary>
		/// Right.
		/// </summary>
		Right,
		/// <summary>
		/// Bottom.
		/// </summary>
		Bottom,
		/// <summary>
		/// Left.
		/// </summary>
		Left,
		/// <summary>
		/// No fixed side, no space claimed.
		/// </summary>
		Float
	};
	#endregion
	#region IChartAxis
	/// <summary>
	/// Features for axes.
	/// Axes must be present in the component list, to provide the infrastructure for scaling data series,
	/// even if they will not display.
	/// </summary>
	public interface IChartAxis {
		/// <summary>
		/// The axis type.
		/// </summary>
		AxisType Type { get; }
		/// <summary>
		/// The axis orientation.
		/// </summary>
		AxisOrientation Orientation { get; }
		/// <summary>
		/// The side of the data area this axis attaches to.
		/// Typically Bottom for Category and Right for Value.
		/// </summary>
		Side Side { get; }
	}
	#endregion
	#region IChartLayerCore
	public interface IChartLayerCore {
		/// <summary>
		/// Position the layer.
		/// MUST be called from UI thread.
		/// </summary>
		/// <param name="target"></param>
		void Layout(Rect target);
		/// <summary>
		/// Remove all the components this layer knows about.
		/// MUST be called from UI thread.
		/// </summary>
		void Clear();
	}
	#endregion
	#region IChartLayer
	/// <summary>
	/// Represents a container for chart component visual elements.
	/// </summary>
	public interface IChartLayer : IChartLayerCore {
		/// <summary>
		/// Add content.
		/// MUST be called from UI thread.
		/// </summary>
		/// <param name="fe">Element to add.</param>
		void Add(FrameworkElement fe);
		/// <summary>
		/// Remove content.
		/// MUST be called from UI thread.
		/// </summary>
		/// <param name="fe">Element to remove.</param>
		void Remove(FrameworkElement fe);
		/// <summary>
		/// Add group of elements.
		/// MUST be called from UI thread.
		/// </summary>
		/// <param name="fes"></param>
		void Add(IEnumerable<FrameworkElement> fes);
		/// <summary>
		/// Remove group of elements.
		/// MUST be called from UI thread.
		/// </summary>
		/// <param name="fes"></param>
		void Remove(IEnumerable<FrameworkElement> fes);
	}
	#endregion
	#region IChartCompositionLayer
	public interface IChartCompositionLayer : IChartLayerCore {
		/// <summary>
		/// Obtain access to the underlying <see cref="ShapeVisual"/> for this layer.
		/// </summary>
		/// <param name="useit"></param>
		void Use<V>(Action<V> useit) where V: Visual;
	}
	#endregion
	#region IChartComponentContext
	/// <summary>
	/// General component context.
	/// </summary>
	public interface IChartComponentContext {
		/// <summary>
		/// The data context object.
		/// </summary>
		object DataContext { get; }
		/// <summary>
		/// Look up a component by name.
		/// </summary>
		/// <param name="name">Name.</param>
		/// <returns>Matching component or NULL.</returns>
		ChartComponent Find(String name);
	}
	#endregion
	#region IChartLayoutContext
	/// <summary>
	/// The context for <see cref="IRequireLayout"/> interface.
	/// </summary>
	public interface IChartLayoutContext {
		/// <summary>
		/// Overall dimensions.
		/// </summary>
		Size Dimensions { get; }
		/// <summary>
		/// Space remaining after claims.
		/// This rectangle is passed to all components via <see cref="IChartRenderContext.SeriesArea"/>.
		/// </summary>
		Rect RemainingRect { get; }
		/// <summary>
		/// Subtract space from RemainingRect and register that rectangle for given component.
		/// Returns the allocated rectangle.
		/// The claimed rectangle is passed back to this component via IChartRenderContext.Area.
		/// </summary>
		/// <param name="cc">Component key.</param>
		/// <param name="sd">Side to allocate from.</param>
		/// <param name="amt">Amount.  Refers to Height:Top/Bottom and Width:Left/Right.  Alternate dimension comes from the Dimensions property.</param>
		/// <returns>Allocated and registered rectangle.</returns>
		Rect ClaimSpace(ChartComponent cc, Side sd, double amt);
	}
	#endregion
	#region IChartLayoutCompleteContext
	/// <summary>
	/// Context interface for <see cref="IRequireLayoutComplete"/>.
	/// </summary>
	public interface IChartLayoutCompleteContext {
		/// <summary>
		/// Overall dimensions.
		/// </summary>
		Size Dimensions { get; }
		/// <summary>
		/// Space remaining after claims.
		/// This rectangle is passed to all components via <see cref="IChartRenderContext.SeriesArea"/>.
		/// </summary>
		Rect SeriesArea { get; }
		/// <summary>
		/// Space for this component.
		/// If no space was claimed, equal to <see cref="SeriesArea"/>.
		/// </summary>
		Rect Area { get; }
	}
	#endregion
	#region IProvideLegend
	/// <summary>
	/// Ability to participate in the legend items collection.
	/// </summary>
	public interface IProvideLegend {
		/// <summary>
		/// The legend item(s) for this component.
		/// MUST return a stable enumeration (same values).
		/// MUST NOT be called before <see cref="IRequireEnterLeave.Enter"/>.
		/// </summary>
		IEnumerable<LegendBase> LegendItems { get; }
	}
	#endregion
	#region IProvideLegendDynamic
	/// <summary>
	/// Event args for the <see cref="IProvideLegendDynamic.LegendChanged"/> event.
	/// </summary>
	public sealed class LegendDynamicEventArgs : EventArgs {
		/// <summary>
		/// The previous items.
		/// </summary>
		public IEnumerable<LegendBase> PreviousItems { get; private set; }
		/// <summary>
		/// The current items.
		/// </summary>
		public IEnumerable<LegendBase> CurrentItems { get; private set; }
		/// <summary>
		/// Ctor.
		/// </summary>
		/// <param name="pitems"></param>
		/// <param name="nitems"></param>
		public LegendDynamicEventArgs(IEnumerable<LegendBase> pitems, IEnumerable<LegendBase> nitems) { PreviousItems = pitems; CurrentItems = nitems; }
	}
	/// <summary>
	/// Ability to provide a dynamically-varying set of legend items.
	/// </summary>
	public interface IProvideLegendDynamic : IProvideLegend {
		/// <summary>
		/// Event to signal the legend items have changed.
		/// </summary>
		event TypedEventHandler<ChartComponent, LegendDynamicEventArgs> LegendChanged;
	}
	#endregion
	#region IChartRenderContext
	/// <summary>
	/// Which type of render pipeline is running.
	/// </summary>
	public enum RenderType {
		/// <summary>
		/// Full render.
		/// </summary>
		Full,
		/// <summary>
		/// Chart transforms-only render, or component transforms-only render.
		/// </summary>
		TransformsOnly,
		/// <summary>
		/// Component full render.
		/// </summary>
		Component,
		/// <summary>
		/// Incremental render.
		/// </summary>
		Incremental
	}
	/// <summary>
	/// The context for <see cref="IRequireRender"/> and <see cref="IRequireTransforms"/> interfaces.
	/// MAY also implement <see cref="IChartErrorInfo"/>.
	/// MAY also implement <see cref="IChartComponentContext"/>.
	/// </summary>
	public interface IChartRenderContext {
		/// <summary>
		/// Current overall dimensions.
		/// </summary>
		Size Dimensions { get; }
		/// <summary>
		/// The area to render this component in.
		/// </summary>
		Rect Area { get; }
		/// <summary>
		/// The area where series are displayed.
		/// </summary>
		Rect SeriesArea { get; }
		/// <summary>
		/// Type of render pipeline.
		/// </summary>
		RenderType Type { get; }
	}
	#endregion
	#region IChartEnterLeaveContext
	/// <summary>
	/// The context for <see cref="IRequireEnterLeave"/> interface.
	/// SHOULD also implement <see cref="IChartErrorInfo"/>.
	/// SHOULD also implement <see cref="IChartComponentContext"/>.
	/// </summary>
	public interface IChartEnterLeaveContext {
		/// <summary>
		/// Create a layer.
		/// </summary>
		/// <returns></returns>
		IChartLayer CreateLayer();
		/// <summary>
		/// Create a layer with given initial components.
		/// </summary>
		/// <param name="fes">Initial components.</param>
		/// <returns></returns>
		IChartLayer CreateLayer(params FrameworkElement[] fes);
		/// <summary>
		/// Delete given layer.
		/// This in turn deletes all the components within the layer being tracked.
		/// </summary>
		/// <param name="icl"></param>
		void DeleteLayer(IChartLayer icl);
		/// <summary>
		/// Create a composition layer.
		/// </summary>
		/// <returns></returns>
		IChartCompositionLayer CreateLayer(params CompositionShape[] cos);
		IChartCompositionLayer CreateLayer(Visual root);
		/// <summary>
		/// Delete given composition layer (and its children).
		/// </summary>
		/// <param name="icl"></param>
		void DeleteLayer(IChartCompositionLayer icl);
	}
	#endregion
	#region ChartValidationResult
	/// <summary>
	/// Use internally to report errors to the chart "owner".
	/// </summary>
	public class ChartValidationResult : ValidationResult {
		/// <summary>
		/// Source of the error: chart, series, axis, etc.
		/// MAY be the name of a component.
		/// MAY be the Type of an unnamed component.
		/// </summary>
		public String Source { get; private set; }
		/// <summary>
		/// Ctor.
		/// </summary>
		/// <param name="source"></param>
		/// <param name="errorMessage"></param>
		public ChartValidationResult(string source, string errorMessage) : base(errorMessage) { Source = source; }
		/// <summary>
		/// Ctor.
		/// </summary>
		/// <param name="source"></param>
		/// <param name="errorMessage"></param>
		/// <param name="memberNames"></param>
		public ChartValidationResult(string source, string errorMessage, IEnumerable<string> memberNames) : base(errorMessage, memberNames) { Source = source; }
	}
	#endregion
	#region IChartErrorInfo
	/// <summary>
	/// Ability to accept (and forward) error reports.
	/// Reports MAY be buffered by the context for later delivery.
	/// </summary>
	public interface IChartErrorInfo {
		/// <summary>
		/// Report an error, to aid configuration troubleshooting.
		/// </summary>
		/// <param name="cvr">The error.</param>
		void Report(ChartValidationResult cvr);
	}
	#endregion
	#region IRequireEnterLeave
	/// <summary>
	/// Require component lifecycle.
	/// </summary>
	public interface IRequireEnterLeave {
		/// <summary>
		/// Component is entering the chart.
		/// Opportunity to add objects to the Visual Tree, then obtain/transfer bindings to those objects from the component's DPs.
		/// Framework makes an effort to defer this call until the VT is available.
		/// Example: components included directly in XAML via Chart.Components.
		/// </summary>
		/// <param name="icelc">The context.</param>
		void Enter(IChartEnterLeaveContext icelc);
		/// <summary>
		/// Component is leaving the chart.
		/// Opportunity to remove objects from Visual Tree etc. the dual of Enter().
		/// </summary>
		/// <param name="icelc">The context.</param>
		void Leave(IChartEnterLeaveContext icelc);
	}
	#endregion
	#region IProvideComponentRender
	/// <summary>
	/// Direct channel to component for incremental component updates (not from the Bus).
	/// </summary>
	public interface IProvideComponentRender {
		/// <summary>
		/// Render component.
		/// </summary>
		/// <param name="icrc"></param>
		void Render(IChartRenderContext icrc);
		/// <summary>
		/// Adjust transforms.
		/// </summary>
		/// <param name="icrc"></param>
		void Transforms(IChartRenderContext icrc);
	}
	#endregion
	#region IRequireConsume
	/// <summary>
	/// Component requires ability to send unsolicited messages.
	/// IST: when a message has a <see cref="IProvideConsume"/>, receiver MUST use that instance.
	/// </summary>
	public interface IRequireConsume {
		/// <summary>
		/// Use for unsolicited messages.
		/// </summary>
		IProvideConsume Bus { get; set; }
	}
	#endregion
	#region component update
	/// <summary>
	/// Refresh request type.
	/// Indicates the relative "severity" of requested update.
	/// MUST be honest!
	/// </summary>
	public enum RefreshRequestType {
		/// <summary>
		/// So very dirty...
		/// Implies ValueDirty and TransformsDirty.
		/// </summary>
		LayoutDirty,
		/// <summary>
		/// A value that generates <see cref="Geometry"/> has changed.
		/// Implies TransformsDirty.
		/// </summary>
		ValueDirty,
		/// <summary>
		/// Something that affects the transforms has changed.
		/// </summary>
		TransformsDirty
	};
	/// <summary>
	/// Axis update information.
	/// If the refresh request indicates axis extents are "intact" the refresh SHOULD be optimized.
	/// MUST be honest!
	/// </summary>
	public enum AxisUpdateState {
		/// <summary>
		/// No axis updates required.
		/// </summary>
		None,
		/// <summary>
		/// Value axis update required.
		/// </summary>
		Value,
		/// <summary>
		/// Category axis update required.
		/// </summary>
		Category,
		/// <summary>
		/// Both axes update required.
		/// </summary>
		Both,
		/// <summary>
		/// Unknown or expensive to check; treat as "Both" or "risk it".
		/// </summary>
		Unknown
	};
	#endregion
	#region IProvideSeriesItemLayout
	/// <summary>
	/// Component provides layout session for label placement.
	/// </summary>
	public interface ILayoutSession {
		/// <summary>
		/// Perform layout of the given item (center in PX, label placement direction vector).
		/// </summary>
		/// <param name="isi">Item obtained from the source of the <see cref="ILayoutSession"/>.</param>
		/// <param name="offset">Placement offset (in M coordinates).</param>
		/// <returns>NULL: cannot calculate; !NULL: placement.</returns>
		(Vector2 center, Point direction)? Layout(ISeriesItem isi, Point offset);
	}
	/// <summary>
	/// Ability to provide <see cref="ILayoutSession"/>.
	/// </summary>
	public interface IProvideSeriesItemLayout {
		/// <summary>
		/// Create session for the given rendering area.
		/// </summary>
		/// <param name="area">Rendering area.</param>
		/// <returns>New session.</returns>
		ILayoutSession Create(Rect area);
	}
	#endregion
}
