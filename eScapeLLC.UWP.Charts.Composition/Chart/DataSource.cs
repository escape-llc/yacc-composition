using eScape.Core;
using eScape.Host;
using eScapeLLC.UWP.Charts.Composition.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Shapes;
using static System.Net.Mime.MediaTypeNames;

namespace eScapeLLC.UWP.Charts.Composition {
	public abstract class DataSource_Operation : CommandPort_Operation {
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
	/// Exit all existing elements, Enter contents of the given list.
	/// </summary>
	public sealed class DataSource_Reset : DataSource_Typed {
		public readonly IList Items;
		public DataSource_Reset(IList items, Type type) :base(type) { this.Items = items; }
		public ItemStateOperation<S>[] CreateOperations<S>(List<ItemStateCore> itemstate, Func<IList,IEnumerable<S>> entering) where S: ItemStateCore {
			ItemStateOperation<S> eexit = new ItemsExiting<S>(ItemTransition.Head, itemstate.Select(xx => xx as S).ToList());
			ItemStateOperation<S> eenter = new ItemsEntering<S>(ItemTransition.Tail, entering(Items).ToList());
			return new ItemStateOperation<S>[] { eexit, eenter };
		}
	}
	/// <summary>
	/// Exit elements from head, enter same number to tail.
	/// </summary>
	public sealed class DataSource_SlidingWindow : DataSource_Typed {
		/// <summary>
		/// Count of items determines number of elements to remove.
		/// </summary>
		public readonly IList NewItems;
		public DataSource_SlidingWindow(IList items, Type type) :base(type) { this.NewItems = items; }
		public ItemStateOperation<S>[] CreateOperations<S>(List<ItemStateCore> itemstate, Func<IList,IEnumerable<S>> entering) where S: ItemStateCore {
			ItemsExiting<S> eexit = new ItemsExiting<S>(ItemTransition.Head, itemstate.Take(NewItems.Count).Select(xx => xx as S).ToList());
			ItemsLive<S> elive = new ItemsLive<S>(ItemTransition.None, itemstate.Skip(NewItems.Count).Select(xx => xx as S).ToList());
			ItemsEntering<S> eenter = new ItemsEntering<S>(ItemTransition.Tail, entering(NewItems).ToList(), elive.Items.Count);
			return new ItemStateOperation<S>[] { eexit, elive, eenter };
		}
	}
	/// <summary>
	/// Add new elements to front/rear.
	/// </summary>
	public sealed class DataSource_Add : DataSource_Typed {
		public readonly bool AtFront;
		public readonly IList NewItems;
		public DataSource_Add(IList items, Type type, bool af = false) :base(type) { this.NewItems = items; AtFront = af; }
		public ItemStateOperation<S>[] CreateOperations<S>(List<ItemStateCore> itemstate, Func<IList, IEnumerable<S>> entering) where S : ItemStateCore {
			ItemsLive<S> elive = new ItemsLive<S>(ItemTransition.None, itemstate.Select(xx => xx as S).ToList(), AtFront ? NewItems.Count : 0);
			var itmp = entering(NewItems);
			if (AtFront) {
				itmp = itmp.Reverse();
			}
			ItemStateOperation<S> eenter = new ItemsEntering<S>(AtFront ? ItemTransition.Head : ItemTransition.Tail, itmp.ToList(), AtFront ? 0 : elive.Items.Count);
			return AtFront ? new[] { eenter, elive } : new[] { elive, eenter };
		}
	}
	/// <summary>
	/// Exit existing elements from front/rear.
	/// </summary>
	public sealed class DataSource_Remove : DataSource_Operation {
		public readonly bool AtFront;
		public readonly int Count;
		public DataSource_Remove(int count, bool atFront) {
			AtFront = atFront;
			Count = count;
		}
		public ItemStateOperation<S>[] CreateOperations<S>(List<ItemStateCore> itemstate) where S : ItemStateCore {
			if (AtFront) {
				ItemStateOperation<S> eexit = new ItemsExiting<S>(ItemTransition.Head, itemstate.Take(Count).Select(xx => xx as S).ToList());
				ItemStateOperation<S> elive = new ItemsLive<S>(ItemTransition.None, itemstate.Skip(Count).Select(xx => xx as S).ToList());
				return new[] { eexit, elive };
			}
			else {
				var ct = itemstate.Count - Count;
				ItemStateOperation<S> elive = new ItemsLive<S>(ItemTransition.None, itemstate.Take(ct).Select(xx => xx as S).ToList());
				ItemStateOperation<S> eexit = new ItemsExiting<S>(ItemTransition.Tail, itemstate.Skip(ct).Select(xx => xx as S).ToList());
				return new[] { eexit, elive };
			}
		}
	}
	public class DataSource : FrameworkElement, IConsumer<DataContextChangedEventArgs> {
		static LogTools.Flag _trace = LogTools.Add("DataSource", LogTools.Level.Error);
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
		public IForwardCommandPort<DataSource_RefreshRequest> Forward { get; set; }
		#endregion
		#region operation factory methods
		public static DataSource_Reset Reset<T>(IList<T> items) {
			return new DataSource_Reset(items as IList, typeof(T));
		}
		public static DataSource_Add Add<T>(IList<T> items, bool atfront = false) {
			return new DataSource_Add(items as IList, typeof(T), atfront);
		}
		public static DataSource_SlidingWindow SlidingWindow<T>(IList<T> items) {
			return new DataSource_SlidingWindow(items as IList, typeof(T));
		}
		public static DataSource_Remove Remove(int count, bool atfront = false) {
			return new DataSource_Remove(count, atfront);
		}
		#endregion
		#region helpers
		/// <summary>
		/// Fire refresh request event.
		/// </summary>
		/// <param name="dso">Type of change.</param>
		void Command(DataSource_Operation dso) {
			dso.Name = Name;
			Forward.Forward(new DataSource_RefreshRequest(Name, dso));
		}
		#endregion
		#region handlers
		void IConsumer<DataContextChangedEventArgs>.Consume(DataContextChangedEventArgs args) {
			if (DataContext != args.NewValue) {
				DataContext = args.NewValue;
			}
		}
		#endregion
	}
}
