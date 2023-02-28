using eScape.Host;
using System.Collections.Generic;
using System.Collections.Immutable;
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
	/// Instruct all components to calculate and register extents.
	/// </summary>
	public sealed class Phase_ComponentExtents : PhaseWithLayoutState {
		/// <summary>
		/// Accumulate registered extents.
		/// </summary>
		readonly List<Component_Extents> Extents;
		public Phase_ComponentExtents(LayoutState ls, List<Component_Extents> extents) : base(ls) { Extents = extents; }
		/// <summary>
		/// Component: register one extent for each axis in use.
		/// </summary>
		/// <param name="cc">Component extents on specific axis.</param>
		public void Register(Component_Extents cc) { Extents.Add(cc); }
	}
	/// <summary>
	/// Instruct all axes to calculate and register extents.
	/// </summary>
	public sealed class Phase_AxisExtents : PhaseWithLayoutState {
		/// <summary>
		/// Current component extents.
		/// </summary>
		public readonly IReadOnlyList<Component_Extents> Extents;
		/// <summary>
		/// Accumulate registered extents.
		/// </summary>
		readonly List<Axis_Extents> AxisExtents;
		public Phase_AxisExtents(LayoutState ls, IReadOnlyList<Component_Extents> extents, List<Axis_Extents> aextents) : base(ls) { Extents = extents; AxisExtents = aextents; }
		/// <summary>
		/// Axis: register extents.
		/// </summary>
		/// <param name="axis">Axis extents.</param>
		public void Register(Axis_Extents axis) { AxisExtents.Add(axis); }
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
	/// Change initiated by <see cref="DataSource"/>.  <see cref="DataSource_Operation"/> receivers to execute the operation.
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
	/// Change initiated by a component that DID NOT result from a <see cref="DataSource_Operation"/>.
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
	/// Notify all extents are captured.
	/// Some components MAY defer rendering to this event.
	/// </summary>
	public sealed class Phase_ModelComplete : PhaseWithRenderContext {
		/// <summary>
		/// Current component extents.
		/// </summary>
		public readonly IReadOnlyList<Component_Extents> Extents;
		/// <summary>
		/// Current axis extents.
		/// </summary>
		public readonly IReadOnlyList<Axis_Extents> AxisExtents;
		public Phase_ModelComplete(
			LayoutState ls, Canvas surface, ObservableCollection<ChartComponent> components, object dataContext,
			IReadOnlyList<Component_Extents> extents, List<Axis_Extents> aextents) : base(ls, surface, components, dataContext) {
				Extents = extents;
				AxisExtents = aextents;
			}
	}
	/// <summary>
	/// Instruct components to adjust transforms to new viewport.
	/// </summary>
	public sealed class Phase_Transforms : PhaseWithRenderContext {
		public Phase_Transforms(LayoutState ls, Canvas surface, ObservableCollection<ChartComponent> components, object dataContext) : base(ls, surface, components, dataContext) { }
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
	#region Component_RefreshRequest
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
	#region DataSource_RefreshRequest
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
