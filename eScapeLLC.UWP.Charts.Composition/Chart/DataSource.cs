﻿using eScape.Core;
using eScape.Host;
using eScapeLLC.UWP.Charts.Composition.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Windows.UI.Xaml;

namespace eScapeLLC.UWP.Charts.Composition {
	#region DataSource_Operation
	public abstract class DataSource_Operation : CommandPort_Operation {
		public string Name { get; internal set; }
		protected DataSource_Operation() { }
	}
	#endregion
	#region DataSource_Clear
	/// <summary>
	/// Empty all items.
	/// </summary>
	public sealed class DataSource_Clear : DataSource_Operation {
	}
	#endregion
	#region DataSource_Typed
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
	#endregion
	#region DataSource_Reset
	/// <summary>
	/// Exit all existing elements, Enter contents of the given list.
	/// </summary>
	public sealed class DataSource_Reset : DataSource_Typed {
		public readonly IList Items;
		public DataSource_Reset(IList items, Type type) :base(type) { this.Items = items; }
		public ItemStateOperation<S>[] CreateOperations<S>(List<ItemStateCore> items, Func<IList,IEnumerable<S>> entering) where S: ItemStateCore {
			ItemStateOperation<S> exit = new ItemsExiting<S>(ItemTransition.Head, items.Select(xx => xx as S).ToList());
			ItemStateOperation<S> enter = new ItemsEntering<S>(ItemTransition.Tail, entering(Items).ToList());
			return new ItemStateOperation<S>[] { exit, enter };
		}
	}
	#endregion
	#region DataSource_SlidingWindow
	/// <summary>
	/// Exit elements from head, enter same number to tail.
	/// </summary>
	public sealed class DataSource_SlidingWindow : DataSource_Typed {
		/// <summary>
		/// Count of items determines number of elements to remove.
		/// </summary>
		public readonly IList NewItems;
		public DataSource_SlidingWindow(IList items, Type type) :base(type) { this.NewItems = items; }
		public ItemStateOperation<S>[] CreateOperations<S>(List<ItemStateCore> items, Func<IList,IEnumerable<S>> entering) where S: ItemStateCore {
			ItemsExiting<S> exit = new ItemsExiting<S>(ItemTransition.Head, items.Take(NewItems.Count).Select(xx => xx as S).ToList());
			ItemsLive<S> live = new ItemsLive<S>(ItemTransition.None, items.Skip(NewItems.Count).Select(xx => xx as S).ToList());
			ItemsEntering<S> enter = new ItemsEntering<S>(ItemTransition.Tail, entering(NewItems).ToList(), live.Items.Count);
			return new ItemStateOperation<S>[] { exit, live, enter };
		}
	}
	#endregion
	#region DataSource_Add
	/// <summary>
	/// Add new elements to front/rear.
	/// </summary>
	public sealed class DataSource_Add : DataSource_Typed {
		public readonly bool AtFront;
		public readonly IList NewItems;
		public DataSource_Add(IList items, Type type, bool af = false) :base(type) { this.NewItems = items; AtFront = af; }
		public ItemStateOperation<S>[] CreateOperations<S>(List<ItemStateCore> items, Func<IList, IEnumerable<S>> entering) where S : ItemStateCore {
			ItemsLive<S> live = new ItemsLive<S>(ItemTransition.None, items.Select(xx => xx as S).ToList(), AtFront ? NewItems.Count : 0);
			var itmp = entering(NewItems);
			if (AtFront) {
				itmp = itmp.Reverse();
			}
			ItemStateOperation<S> enter = new ItemsEntering<S>(AtFront ? ItemTransition.Head : ItemTransition.Tail, itmp.ToList(), AtFront ? 0 : live.Items.Count);
			return AtFront ? new[] { enter, live } : new[] { live, enter };
		}
	}
	#endregion
	#region DataSource_Remove
	/// <summary>
	/// Exit existing elements from front/rear.
	/// </summary>
	public sealed class DataSource_Remove : DataSource_Operation {
		public readonly bool AtFront;
		public readonly uint Count;
		public DataSource_Remove(uint count, bool atFront) {
			AtFront = atFront;
			Count = count;
		}
		public ItemStateOperation<S>[] CreateOperations<S>(List<ItemStateCore> items) where S : ItemStateCore {
			if (AtFront) {
				ItemStateOperation<S> exit = new ItemsExiting<S>(ItemTransition.Head, items.Take((int)Count).Select(xx => xx as S).ToList());
				ItemStateOperation<S> live = new ItemsLive<S>(ItemTransition.None, items.Skip((int)Count).Select(xx => xx as S).ToList());
				return new[] { exit, live };
			}
			else {
				var ct = items.Count - Count;
				ItemStateOperation<S> live = new ItemsLive<S>(ItemTransition.None, items.Take((int)ct).Select(xx => xx as S).ToList());
				ItemStateOperation<S> exit = new ItemsExiting<S>(ItemTransition.Tail, items.Skip((int)ct).Select(xx => xx as S).ToList());
				return new[] { exit, live };
			}
		}
	}
	#endregion
	#region DataSource
	public class DataSource : FrameworkElement, IConsumer<DataContextChangedEventArgs> {
		static readonly LogTools.Flag _trace = LogTools.Add("DataSource", LogTools.Level.Error);
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
		/// An "external source" (like a View Model) SHOULD attach a data binding to this property to trigger data source operations.
		/// </summary>
		public DataSource_Operation CommandPort { get { return (DataSource_Operation)GetValue(CommandPortProperty); } set { SetValue(CommandPortProperty, value); } }
		/// <summary>
		/// Used to forward unsolicited messages.
		/// </summary>
		public IForwardCommandPort<DataSource_Request, DataSource_Operation> Forward { get; set; }
		#endregion
		#region operation factory methods
		/// <summary>
		/// Produce a clear operation.
		/// </summary>
		/// <returns>New instance.</returns>
		public static DataSource_Clear Clear() { return new DataSource_Clear(); }
		/// <summary>
		/// Produce a reset operation.
		/// </summary>
		/// <typeparam name="T">Item type.</typeparam>
		/// <param name="items">New items.</param>
		/// <returns>New instance.</returns>
		public static DataSource_Reset Reset<T>(IList<T> items) {
			if (items.Count == 0) throw new ArgumentException(nameof(items));
			return new DataSource_Reset(items as IList, typeof(T));
		}
		/// <summary>
		/// Produce an Add operation.
		/// </summary>
		/// <typeparam name="T">Item type.</typeparam>
		/// <param name="items">New items.</param>
		/// <param name="athead">true: target head; false: target tail.</param>
		/// <returns>New instance.</returns>
		public static DataSource_Add Add<T>(IList<T> items, bool athead = false) {
			if (items.Count == 0) throw new ArgumentException(nameof(items));
			return new DataSource_Add(items as IList, typeof(T), athead);
		}
		/// <summary>
		/// Produce a Sliding Window operation.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="items"></param>
		/// <returns>New instance.</returns>
		public static DataSource_SlidingWindow SlidingWindow<T>(IList<T> items) {
			if (items.Count == 0) throw new ArgumentException(nameof(items));
			return new DataSource_SlidingWindow(items as IList, typeof(T));
		}
		/// <summary>
		/// Produce a Remove operation.
		/// </summary>
		/// <param name="count">Number of items.</param>
		/// <param name="athead">true: target head; false: target tail.</param>
		/// <returns>New instance.</returns>
		public static DataSource_Remove Remove(uint count, bool athead = false) {
			if (count == 0) throw new ArgumentException(nameof(count));
			return new DataSource_Remove(count, athead);
		}
		#endregion
		#region helpers
		/// <summary>
		/// Forward <see cref="CommandPort"/> operations.
		/// </summary>
		/// <param name="dso">Data source operation.</param>
		void Command(DataSource_Operation dso) {
			dso.Name = Name;
			Forward?.Forward(new DataSource_Request(Name, dso));
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
	#endregion
}
