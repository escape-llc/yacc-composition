using eScape.Core;
using eScape.Host;
using eScapeLLC.UWP.Charts.Composition.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using Windows.UI.Xaml;

namespace eScapeLLC.UWP.Charts.Composition {
	public abstract class DataSource_Operation {
		public string Name { get; internal set; }
		protected DataSource_Operation() {
		}
	}
	/// <summary>
	/// Empty all items.
	/// </summary>
	public sealed class DataSource_Clear : DataSource_Operation {
	}
	/// <summary>
	/// Makes it easier to deal with generic versions without knowing the type.
	/// </summary>
	public abstract class DataSource_Typed : DataSource_Operation {
		protected DataSource_Typed(Type type) : base() {
			ItemType = type;
		}
		/// <summary>
		/// Access to generic type without reflection.
		/// </summary>
		public Type ItemType { get; protected set; }
	}
	/// <summary>
	/// Reset contents to the given list.
	/// </summary>
	public sealed class DataSource_Reset : DataSource_Typed {
		public readonly IList Items;
		public DataSource_Reset(IList items, Type type) :base(type) { this.Items = items; }
	}
	/// <summary>
	/// Delete elements from head, append same number to tail.
	/// </summary>
	public sealed class DataSource_SlidingWindow : DataSource_Typed {
		/// <summary>
		/// Count of items determines number of elements to remove.
		/// </summary>
		public readonly IList NewItems;
		public DataSource_SlidingWindow(IList items, Type type) :base(type) { this.NewItems = items; }
	}
	/// <summary>
	/// Add new elements.
	/// </summary>
	public sealed class DataSource_Add : DataSource_Typed {
		public readonly bool AtFront;
		public readonly IList NewItems;
		public DataSource_Add(IList items, Type type, bool af = false) :base(type) { this.NewItems = items; AtFront = af; }
	}
	public class DataSource : FrameworkElement, IConsumer<DataContextChangedEventArgs> {
		static LogTools.Flag _trace = LogTools.Add("DataSource", LogTools.Level.Error);
		#region inner
		public interface IForwardCommandPort {
			void Forward(DataSource_RefreshRequest dso);
		}
		#endregion
		#region DPs
		/// <summary>
		/// Identifies <see cref="CommandPort"/> DP.
		/// </summary>
		public static readonly DependencyProperty CommandPortProperty = DependencyProperty.Register(
			nameof(CommandPort), typeof(DataSource_Operation), typeof(DataSource), new PropertyMetadata(null, new PropertyChangedCallback(CommandPortPropertyChanged))
		);
		/// <summary>
		/// Trigger a refresh when the value changes.
		/// </summary>
		/// <param name="dobj"></param>
		/// <param name="dpcea"></param>
		private static void CommandPortPropertyChanged(DependencyObject dobj, DependencyPropertyChangedEventArgs dpcea) {
			DataSource ds = dobj as DataSource;
			_trace.Verbose($"CommandPort new:{dpcea.NewValue} old:{dpcea.OldValue}");
			if (dpcea.NewValue is DataSource_Operation bx) {
				if (dpcea.NewValue != dpcea.OldValue) {
					ds.Command(bx);
				}
			}
		}
		#endregion
		#region properties
		/// <summary>
		/// Means for an "external source" (like a View Model) to attach a data binding to this property and trigger data source operations.
		/// </summary>
		public DataSource_Operation CommandPort { get { return (DataSource_Operation)GetValue(CommandPortProperty); } set { SetValue(CommandPortProperty, value); } }
		/// <summary>
		/// Used for unsolicited messages.
		/// </summary>
		public IForwardCommandPort Forward { get; set; }
		#endregion
		/// <summary>
		/// Mark as dirty and fire refresh request event.
		/// Use this with sources that <b>don't</b> implement <see cref="INotifyCollectionChanged"/>.
		/// ALSO use this if you are not using <see cref="ExternalRefresh"/> property.
		/// </summary>
		/// <param name="dso">Type of change.</param>
		 void Command(DataSource_Operation dso) {
			dso.Name = Name;
			Forward.Forward(new DataSource_RefreshRequest(Name, dso));
		}
		void IConsumer<DataContextChangedEventArgs>.Consume(DataContextChangedEventArgs args) {
			if (DataContext != args.NewValue) {
				DataContext = args.NewValue;
			}
		}
	}
}
