using eScape.Host;
using System.Collections.ObjectModel;
using Windows.UI.Xaml.Controls;

namespace eScapeLLC.UWP.Charts.Composition.Events {
	#region render pipeline events
	/// <summary>
	/// Instruct axis components to initialize.
	/// </summary>
	public sealed class Phase_InitializeAxes {
		/// <summary>
		/// Use to send <see cref="Axis_Extents"/> events.
		/// </summary>
		readonly IProvideConsume Bus;
		public Phase_InitializeAxes(IProvideConsume eb) { Bus = eb; }
		public void Register(Axis_Extents axis) { Bus.Consume(axis); }
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
	/// Instruct all components to broadcast extents.
	/// </summary>
	public sealed class Phase_ComponentExtents : PhaseWithLayoutState {
		/// <summary>
		/// Use to send <see cref="Component_Extents"/> events.
		/// </summary>
		readonly IProvideConsume Bus;
		public Phase_ComponentExtents(LayoutState ls, IProvideConsume eb) : base(ls) { Bus = eb; }
		public void Register(Component_Extents cc) { Bus.Consume(cc); }
	}
	/// <summary>
	/// Instruct all axes to broadcast extents.
	/// </summary>
	public sealed class Phase_AxisExtents : PhaseWithLayoutState {
		/// <summary>
		/// Use to send <see cref="Axis_Extents"/> events.
		/// </summary>
		readonly IProvideConsume Bus;
		public Phase_AxisExtents(LayoutState ls, IProvideConsume eb) : base(ls) { Bus = eb; }
		public void Register(Axis_Extents axis) { Bus.Consume(axis); }
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
	/// Instruct DSRP components to render.
	/// </summary>
	public sealed class Phase_DataSourceOperation : PhaseWithRenderContext {
		public readonly string Name;
		public readonly DataSource_Operation Operation;
		public Phase_DataSourceOperation(
			string name,
			LayoutState ls, Canvas surface,
			ObservableCollection<ChartComponent> components,
			object dataContext, DataSource_Operation operation) : base(ls, surface, components, dataContext) {
			Name = name;
			Operation = operation;
		}
	}
	/// <summary>
	/// Change initiated by a non-DSRP component.
	/// </summary>
	public sealed class Phase_ComponentOperation : PhaseWithRenderContext {
		public readonly string Name;
		public readonly Component_Operation Operation;
		public Phase_ComponentOperation(
			string name,
			LayoutState ls, Canvas surface,
			ObservableCollection<ChartComponent> components,
			object dataContext, Component_Operation operation) : base(ls, surface, components, dataContext) {
			Name = name;
			Operation = operation;
		}
	}
	/// <summary>
	/// Notify non-DSRP components (axes, decorations) to render after extents are established.
	/// </summary>
	public sealed class Phase_ModelComplete : PhaseWithRenderContext {
		public Phase_ModelComplete(LayoutState ls, Canvas surface, ObservableCollection<ChartComponent> components, object dataContext) : base(ls, surface, components, dataContext) { }
	}
	/// <summary>
	/// Instruct components to adjust transforms to new viewport.
	/// </summary>
	public sealed class Phase_RenderTransforms : PhaseWithRenderContext {
		public Phase_RenderTransforms(LayoutState ls, Canvas surface, ObservableCollection<ChartComponent> components, object dataContext) : base(ls, surface, components, dataContext) { }
	}
	#endregion
	#region Component events
	/// <summary>
	/// Send on EB in response to <see cref="Phase_ComponentExtents"/>.
	/// </summary>
	public sealed class Component_Extents {
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
		public Component_Extents(string seriesName, string dataSourceName, string axisName, double minimum, double maximum) {
			SeriesName = seriesName;
			DataSourceName = dataSourceName;
			AxisName = axisName;
			Minimum = minimum;
			Maximum = maximum;
		}
	}
	/// <summary>
	/// Specific component is requesting refresh via command port.
	/// </summary>
	public sealed class Component_RefreshRequest : CommandPort_RefreshRequest {
		public readonly string Name;
		public readonly Component_Operation Operation;
		public Component_RefreshRequest(Component_Operation op) {
			Operation = op;
			Name = op.Component.Name;
		}
	}
	#endregion
	#region Axis events
	/// <summary>
	/// Send on EB in response to <see cref="Phase_AxisExtents"/>.
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
		public readonly double Range;
		public Axis_Extents(string axisName, double minimum, double maximum, Side axisSide, AxisType axisType, bool reversed) {
			AxisName = axisName;
			Minimum = minimum;
			Maximum = maximum;
			AxisSide = axisSide;
			Type = axisType;
			Reversed = reversed;
			Range = double.IsNaN(minimum) || double.IsNaN(maximum) ? double.NaN : maximum - minimum;
		}
		public AxisOrientation Orientation => AxisSide == Side.Left || AxisSide == Side.Right ? AxisOrientation.Vertical : AxisOrientation.Horizontal;
	}
	#endregion
	#region CommandPort
	/// <summary>
	/// Ability to accept operations forwarded from command port.
	/// </summary>
	/// <typeparam name="C">Command type.</typeparam>
	public interface IForwardCommandPort<C> where C: CommandPort_RefreshRequest {
		/// <summary>
		/// Accept forwarded message.
		/// </summary>
		/// <param name="msg">message.</param>
		void Forward(C msg);
	}
	public abstract class CommandPort_Operation { }
	public abstract class CommandPort_RefreshRequest { }
	#endregion
	#region DataSource events
	/// <summary>
	/// Data source is requesting an operation via command port.
	/// </summary>
	public sealed class DataSource_RefreshRequest : CommandPort_RefreshRequest {
		/// <summary>
		/// Name of data source.
		/// </summary>
		public readonly string Name;
		/// <summary>
		/// Command to execute.
		/// </summary>
		public readonly DataSource_Operation Operation;
		public DataSource_RefreshRequest(string name, DataSource_Operation cmd) {
			Name = name;
			Operation = cmd;
		}
	}
	#endregion
}
