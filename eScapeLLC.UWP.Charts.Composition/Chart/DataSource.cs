using eScape.Core;
using eScape.Host;
using eScapeLLC.UWP.Charts.Composition.Events;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Windows.UI.Xaml;

namespace eScapeLLC.UWP.Charts.Composition {
	public class DataSource : FrameworkElement, IRequireConsume, IConsumer<DataContextChangedEventArgs> {
		static LogTools.Flag _trace = LogTools.Add("DataSource", LogTools.Level.Error);
		#region DPs
		/// <summary>
		/// Identifies <see cref="Items"/> DP.
		/// </summary>
		public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(
			nameof(Items), typeof(System.Collections.IEnumerable), typeof(DataSource), new PropertyMetadata(null, new PropertyChangedCallback(ItemsPropertyChanged))
		);
		/// <summary>
		/// Identifies <see cref="ExternalRefresh"/> DP.
		/// </summary>
		public static readonly DependencyProperty ExternalRefreshProperty = DependencyProperty.Register(
			nameof(ExternalRefresh), typeof(int), typeof(DataSource), new PropertyMetadata(null, new PropertyChangedCallback(ExternalRefreshPropertyChanged))
		);
		/// <summary>
		/// Trigger a refresh when the value changes.
		/// </summary>
		/// <param name="dobj"></param>
		/// <param name="dpcea"></param>
		private static void ExternalRefreshPropertyChanged(DependencyObject dobj, DependencyPropertyChangedEventArgs dpcea) {
			DataSource ds = dobj as DataSource;
			if (dpcea.NewValue is int bx) {
				if (dpcea.NewValue != dpcea.OldValue && ds.Items != null) {
					ds.Refresh(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
				}
			}
		}
		/// <summary>
		/// Do the <see cref="INotifyCollectionChanged"/> bookkeeping.
		/// </summary>
		/// <param name="dobj"></param>
		/// <param name="dpcea"></param>
		private static void ItemsPropertyChanged(DependencyObject dobj, DependencyPropertyChangedEventArgs dpcea) {
			DataSource ds = dobj as DataSource;
			if (dpcea.OldValue != dpcea.NewValue) {
				DetachCollectionChanged(ds, dpcea.OldValue);
				AttachCollectionChanged(ds, dpcea.NewValue);
				ds.Refresh(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			}
		}
		private static void DetachCollectionChanged(DataSource ds, object dataSource) {
			if (dataSource is INotifyCollectionChanged incc) {
				incc.CollectionChanged -= ds.ItemsCollectionChanged;
			}
		}
		private static void AttachCollectionChanged(DataSource ds, object dataSource) {
			if (dataSource is INotifyCollectionChanged incc) {
				incc.CollectionChanged += ds.ItemsCollectionChanged;
			}
		}
		private void ItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs nccea) {
			// collection has already been modified when we arrive here
			_trace.Verbose($"cc {nccea.Action} nsi:[{nccea.NewStartingIndex}] {nccea.NewItems?.Count} osi:[{nccea.OldStartingIndex}] {nccea.OldItems?.Count}");
			Refresh(nccea);
		}
		#endregion
		#region properties
		/// <summary>
		/// Data source for the series.
		/// If the object implements <see cref="INotifyCollectionChanged"/> (e.g. <see cref="System.Collections.ObjectModel.ObservableCollection{T}"/>), updates are tracked automatically.
		/// Otherwise (e.g. <see cref="System.Collections.IList"/>), owner must call Refresh() after the underlying source is modified.
		/// </summary>
		public System.Collections.IEnumerable Items { get { return (System.Collections.IEnumerable)GetValue(ItemsProperty); } set { SetValue(ItemsProperty, value); } }
		/// <summary>
		/// True: render required.
		/// SHOULD only be used within the framework, as it's not a DP or awt.
		/// </summary>
		public bool IsDirty { get; set; }
		/// <summary>
		/// Means for an "external source" (like a View Model) to attach a data binding to this property and trigger data source refreshes.
		/// ONLY use this if your <see cref="Items"/> DOES NOT implement <see cref="INotifyCollectionChanged"/>.
		/// </summary>
		public int ExternalRefresh { get { return (int)GetValue(ExternalRefreshProperty); } set { SetValue(ExternalRefreshProperty, value); } }
		/// <summary>
		/// Used to validate bindings before any data traversal is performed.
		/// </summary>
		public Type ExpectedItemType { get; set; }
		/// <summary>
		/// Used for unsolicited messages.
		/// </summary>
		public IProvideConsume Bus { get; set; }
		#endregion
		#region helpers
		/// <summary>
		/// Hook for dirty.
		/// Sets IsDirty = True.
		/// Default impl.
		/// </summary>
		protected virtual void Dirty() { IsDirty = true; }
		/// <summary>
		/// Hook for clean.
		/// Sets IsDirty = False.
		/// Default impl.
		/// </summary>
		protected virtual void Clean() { IsDirty = false; }
		#endregion
		/// <summary>
		/// Mark as dirty and fire refresh request event.
		/// Use this with sources that <b>don't</b> implement <see cref="INotifyCollectionChanged"/>.
		/// ALSO use this if you are not using <see cref="ExternalRefresh"/> property.
		/// </summary>
		/// <param name="nccea">Type of change.</param>
		public void Refresh(NotifyCollectionChangedEventArgs nccea) {
			Dirty();
			Bus.Consume(new DataSource_RefreshRequest(Name, nccea));
		}
		public void Consume(DataContextChangedEventArgs args) {
			if (DataContext != args.NewValue) {
				DataContext = args.NewValue;
			}
		}
		public virtual void Render(EventBus bus, IDataSourceRenderContext ctx) {
			_trace.Verbose($"Render {Name} i:{Items} dirty:{IsDirty} eit:{ExpectedItemType?.Name}");
			if (Items == null) return;
			if (IsDirty == false) return;
			// advertise for rendering
			var list = new List<IDataSourceRenderer>();
			bus.Consume(new DataSource_RenderStart(Name, ExpectedItemType, list, bus));
			if(list.Count > 0) {
				// Phase I: init each renderer
				foreach (var idsr in list) {
					idsr.Preamble(ctx);
				}
				// Phase II: traverse the data and distribute to renderers
				int ix = 0;
				foreach (var item in Items) {
					foreach (var idsr in list) {
						idsr.Render(ix, item);
					}
					ix++;
				}
				// Phase III: post-traversal render bookkeeping
				foreach (var idsr in list) {
					idsr.RenderComplete();
				}
				// Phase IV: finalize renderers
				foreach (var idsr in list) {
					idsr.Postamble();
				}
			}
			Clean();
			// notify anyone interested
			bus.Consume(new DataSource_RenderEnd(Name));
		}
	}
}
