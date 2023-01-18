using eScape.Host;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Windows.UI.Xaml.Controls;

namespace eScapeLLC.UWP.Charts.Composition.Events {
	/// <summary>
	/// Instruct axis components to initialize.
	/// </summary>
	public sealed class Phase_InitializeAxes {
		/// <summary>
		/// Use to send <see cref="Axis_Extents"/> events.
		/// </summary>
		readonly public EventBus Bus;
		public Phase_InitializeAxes(EventBus eb) { Bus = eb; }
	}
	/// <summary>
	/// Instruct components to claim space.  Components using the Series Area MUST NOT claim space.
	/// </summary>
	public sealed class Phase_Layout {
		public readonly IChartLayoutContext Context;
		public Phase_Layout(IChartLayoutContext context) {
			Context = context;
		}
	}
	public abstract class PhaseWithLayoutState {
		protected readonly LayoutState ls;
		protected PhaseWithLayoutState(LayoutState ls) { this.ls = ls; }
		public RenderType Type => ls.Type;
	}
	/// <summary>
	/// Notify components layout has completed.
	/// </summary>
	public sealed class Phase_LayoutComplete : PhaseWithLayoutState {
		public Phase_LayoutComplete(LayoutState ls) :base(ls) { }
		public IChartLayoutCompleteContext ContextFor(ChartComponent cc) {
			var rect = ls.Layout.For(cc);
			var ctx = new DefaultLayoutCompleteContext(ls.Layout.Dimensions, rect, ls.Layout.RemainingRect);
			return ctx;
		}
	}
	/// <summary>
	/// Instruct axes to broadcast extents.
	/// </summary>
	public sealed class Phase_FinalizeAxes : PhaseWithLayoutState {
		/// <summary>
		/// Use to send <see cref="Axis_Extents"/> events.
		/// </summary>
		readonly public EventBus Bus;
		public Phase_FinalizeAxes(LayoutState ls, EventBus eb) : base(ls) { Bus = eb; }
	}
	/// <summary>
	/// Core for phases with render context.
	/// </summary>
	public class PhaseWithRenderContext : PhaseWithLayoutState {
		readonly Canvas Surface;
		readonly ObservableCollection<ChartComponent> Components;
		readonly object DataContext;
		public PhaseWithRenderContext(LayoutState ls, Canvas surface, ObservableCollection<ChartComponent> components, object dataContext) : base(ls) {
			Surface = surface;
			Components = components;
			DataContext = dataContext;
		}
		public IChartRenderContext ContextFor(ChartComponent cc) {
			var ctx = ls.RenderFor(cc, Surface, Components, DataContext);
			return ctx;
		}
	}
	/// <summary>
	/// Instruct non-DSRP components to render.
	/// </summary>
	public sealed class Phase_RenderComponents : PhaseWithRenderContext {
		public Phase_RenderComponents(LayoutState ls, Canvas surface, ObservableCollection<ChartComponent> components, object dataContext) :base(ls, surface, components, dataContext) {}
	}
	/// <summary>
	/// Instruct axis components to render.
	/// </summary>
	public sealed class Phase_RenderAxes : PhaseWithRenderContext {
		public Phase_RenderAxes(LayoutState ls, Canvas surface, ObservableCollection<ChartComponent> components, object dataContext) : base(ls, surface, components, dataContext) { }
	}
	/// <summary>
	/// Instruct components to adjust transforms to new viewport.
	/// </summary>
	public sealed class Phase_RenderTransforms : PhaseWithRenderContext {
		public Phase_RenderTransforms(LayoutState ls, Canvas surface, ObservableCollection<ChartComponent> components, object dataContext) : base(ls, surface, components, dataContext) { }
	}
	/// <summary>
	/// Send on EB when a series has update to its extents.
	/// </summary>
	public sealed class Series_Extents {
		/// <summary>
		/// MUST match receiver's series name.
		/// </summary>
		public readonly string SeriesName;
		/// <summary>
		/// MUST match receiver's data source.
		/// </summary>
		public readonly string DataSourceName;
		/// <summary>
		/// SHOULD receive one event for each axis.
		/// </summary>
		public readonly string AxisName;
		public readonly double Minimum;
		public readonly double Maximum;
		public Series_Extents(string seriesName, string dataSourceName, string axisName, double minimum, double maximum) {
			SeriesName = seriesName;
			DataSourceName = dataSourceName;
			AxisName = axisName;
			Minimum = minimum;
			Maximum = maximum;
		}
	}
	/// <summary>
	/// Send on EB when an axis has update to its extents.
	/// </summary>
	public sealed class Axis_Extents {
		public readonly string AxisName;
		/// <summary>
		/// MAY be <see cref="double.NaN"/>.
		/// </summary>
		public readonly double Minimum;
		/// <summary>
		/// MAY be <see cref="double.NaN"/>.
		/// </summary>
		public readonly double Maximum;
		public readonly Side AxisSide;
		public readonly AxisType Type;
		public readonly bool Reversed;
		public Axis_Extents(string axisName, double minimum, double maximum, Side axisSide, AxisType axisType, bool reversed) {
			AxisName = axisName;
			Minimum = minimum;
			Maximum = maximum;
			AxisSide = axisSide;
			Type = axisType;
			Reversed = reversed;
		}
		public AxisOrientation Orientation => AxisSide == Side.Left || AxisSide == Side.Right ? AxisOrientation.Vertical : AxisOrientation.Horizontal;
	}
	public sealed class DataSource_RefreshRequest { }
	/// <summary>
	/// Advertise start of Data Source Render Pipeline (DSRP).
	/// Interested parties MUST call <see cref="Register(IDataSourceRenderer)"/> to participate.
	/// </summary>
	public sealed class DataSource_RenderStart {
		readonly List<IDataSourceRenderer> states;
		/// <summary>
		/// MUST match name of receiver's data source.
		/// </summary>
		readonly public string Name;
		/// <summary>
		/// Receiver SHOULD verify property access prior to registration.
		/// </summary>
		readonly public Type ExpectedItemType;
		/// <summary>
		/// Use for messaging.  MAY retain a reference.
		/// </summary>
		readonly public EventBus Bus;
		/// <summary>
		/// Ctor.
		/// </summary>
		/// <param name="name">Name of data source advertising.</param>
		/// <param name="type">Expected item type.</param>
		/// <param name="states">Accumulate registrations.</param>
		/// <param name="bus">Use for messaging.</param>
		public DataSource_RenderStart(string name, Type type, List<IDataSourceRenderer> states, EventBus bus) {
			Name = name;
			ExpectedItemType = type;
			this.states = states;
			Bus = bus;
		}
		/// <summary>
		/// Express interest in participating in DSRP on this data source.
		/// Event receiver MAY register multiple instances.
		/// </summary>
		/// <param name="state">Renderer to register.</param>
		public void Register(IDataSourceRenderer state) { states.Add(state); }
	}
	/// <summary>
	/// Advertise end of DSRP for data source.
	/// </summary>
	public sealed class DataSource_RenderEnd  {
		public readonly string Name;
		public DataSource_RenderEnd(string name) { Name = name; }
	}
}
