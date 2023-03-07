using eScape.Core.Host;
using eScape.Core.VM;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel;
using Windows.Data.Json;
using Windows.UI.Core;
using Yacc.Composition.Demo.Pages;

namespace Yacc.Demo.VM {
	#region MainPageVM
	/// <summary>
	/// Represents an item on the nav pane.
	/// </summary>
	public class PageItem {
		public String Glyph { get; set; } = "?";
		public String Title { get; set; }
		public String Description { get; set; }
		public Type PageType { get; set; }
	}
	/// <summary>
	/// VM for the main page.
	/// </summary>
	public class MainPageVM : CoreViewModel {
		public ObservableCollection<PageItem> PageList { get; private set; }
		/// <summary>
		/// Indicate whether we're in RDP or not.
		/// Some features appear disabled when Remote, like implicit composition show/hide animations.
		/// Other implicit composition animations appear to work, e.g. Offset.
		/// </summary>
		public bool IsRemote { get; } = Windows.System.RemoteDesktop.InteractiveSession.IsRemote;
		public bool IsImplicitSupported { get; } = true;
		public bool IsShowHideSupported { get; } = true;
		public MainPageVM(CoreDispatcher dx) : base(dx) {
			// build offline so we don't trigger events
			var pl = new ObservableCollection<PageItem> {
				new PageItem() { Glyph="\u2460", Title = "Demo", Description = "The demo chart (as seen on The Internet) with implicit animations.", PageType = typeof(Chart1) },
				//new PageItem() { Glyph="\u2461", Title = "Default", Description = "Default styles in case you forget!", PageType = typeof(Chart2) },
				//new PageItem() { Glyph="\u2462", Title = "Animations", Description = "Incremental updates dovetail beautifully with implicit animations.", PageType = typeof(Chart3) },
				//new PageItem() { Glyph="\u2463", Title = "Stock Chart", Description = "Cached real data, double Y-axis, rotated X-axis labels, conditional X-axis labels.", PageType = typeof(Chart4) },
				//new PageItem() { Glyph="\u2464", Title = "Stacked", Description = "Stacked column chart with labels.", PageType = typeof(Chart5) },
				//new PageItem() { Glyph="\u2465", Title = "Sharing", Description = "Share a single collection among multiple charts.", PageType = typeof(Chart7) },
				//new PageItem() { Glyph="\u2466", Title = "Heatmap", Description = "Present color-coded data cells on 2 category axes.", PageType = typeof(Chart8) },
				//new PageItem() { Glyph="\u2467", Title = "Pie", Description = "Pie chart.", PageType = typeof(Chart6) },
				//new PageItem() { Glyph="\u2468", Title = "Horizontal Bar", Description = "Create horizontal charts!", PageType = typeof(Chart9) },
			};
			PageList = pl;
		}
	}
	#endregion
	#region ObservationsVM
	public class Observation {
		public static string FormatString = "{0:F2}";
		public int Index { get; set; }
		public string Label => $"Observation[{Index}]";
		public double Value1 { get; set; }
		public string Value1Formatted => string.Format(FormatString, Value1);
		public double Value2 { get; set; }
		public string Value2Formatted => string.Format(FormatString, Value2);
		public double Value3 { get; set; }
		public string Value3Formatted => string.Format(FormatString, Value3);
		public Observation(int index, double value1, double value2, double value3) {
			Index = index;
			Value1 = value1;
			Value2 = value2;
			Value3 = value3;
		}
	}
	/// <summary>
	/// Represents one "cell" of the chart.
	/// This would be typical of a SQL DAO or other domain object.
	/// </summary>
	public class ObservationX {
		public String Label { get; private set; }
		public double Value1 { get; private set; }
		public double Value2 { get; private set; }
		public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
		public ObservationX(String label, double v1, double v2) { Label = label; Value1 = v1; Value2 = v2; }
	}
	/// <summary>
	/// Simple VM that maintains list of observations and the average of its two values.
	/// </summary>
	public class ObservationsVM : CoreViewModel {
		readonly Random rnd = new Random();
		bool _band;
		bool _grid;
		public int GroupCounter { get; private set; }
		public double Value1Average { get; private set; }
		public double Value2Average { get; private set; }
		public ObservableCollection<Observation> Data { get; private set; }
		public bool ShowBand { get { return _band; } set { _band = value; Changed(nameof(ShowBand)); } }
		public bool ShowGrid { get { return _grid; } set { _grid = value; Changed(nameof(ShowGrid)); } }
		public ObservationsVM(CoreDispatcher dx, IEnumerable<Observation> initial): base(dx) {
			Data = new ObservableCollection<Observation>(initial);
			GroupCounter = Data.Count;
			Recalculate();
			_band = true;
			_grid = true;
		}
		/// <summary>
		/// Randomly generate an item and add it to end and recalculate.
		/// </summary>
		public void AddTail(Observation obs) {
			Data.Add(obs);
			Recalculate();
		}
		/// <summary>
		/// Randomly generate an item and add it to front and recalculate.
		/// </summary>
		public void AddHead(Observation obs) {
			Data.Insert(0, obs);
			Recalculate();
		}
		/// <summary>
		/// Remove the first item and recalculate.
		/// </summary>
		public void RemoveHead() {
			if (Data.Count > 0) {
				Data.RemoveAt(0);
				Recalculate();
			}
		}
		/// <summary>
		/// Remove the last item and recalculate
		/// </summary>
		public void RemoveTail() {
			if (Data.Count > 0) {
				Data.RemoveAt(Data.Count - 1);
				Recalculate();
			}
		}
		/// <summary>
		/// Modify both ends of list and recalculate.
		/// </summary>
		public void AddAndRemoveHead(Observation obs) {
			RemoveHead();
			AddTail(obs);
			Recalculate();
		}
		/// <summary>
		/// Recalculate and trigger INPC.
		/// </summary>
		void Recalculate() {
			if (Data.Count > 0) {
				Value1Average = Data.Average((ob) => ob.Value1);
				Value2Average = Data.Average((ob) => ob.Value2);
			} else {
				Value1Average = 0;
				Value2Average = 0;
			}
			Changed(nameof(Value1Average));
			Changed(nameof(Value2Average));
		}
	}
	#endregion
	#region Observation2
	/// <summary>
	/// Simple extension to hold more values.
	/// </summary>
	public class Observation2 : ObservationX {
		public double Value3 { get; private set; }
		public double Value4 { get; private set; }
		public int Value5 { get; private set; }
		public Observation2(string label, double v1, double v2, double v3, double v4, int v5 = 0) : base(label, v1, v2) { Value3 = v3; Value4 = v4; Value5 = v5; }
		/// <summary>
		/// Static placeholder value.  Makes a "hole".
		/// </summary>
		public static Observation2 PLACEHOLDER = new Observation2("-", double.NaN, double.NaN, double.NaN, double.NaN, 0);
	}
	#endregion
	#region ObservationGrouped
	public class ObservationGrouped {
		public string Label1 { get; private set; }
		public string Label2 { get; private set; }
		public int Label1Index { get; private set; }
		public int Label2Index { get; private set; }
		public double Value { get; private set; }
		public ObservationGrouped(string l1, string l2, int l1i, int l2i, double vx) { Label1 = l1; Label1Index = l1i; Label2 = l2; Label2Index = l2i; Value = vx; }
		public double Accumulate(double vx) {
			Value += vx;
			return Value;
		}
	}
	public class GroupInfo {
		public int Index { get; private set; }
		public string Label { get; private set; }
		public GroupInfo(int ix, string lx) { Index = ix; Label = lx; }
	}
	public class ObservationGroupedVM : CoreViewModel {
		readonly Random rnd = new Random();
		bool _band;
		bool _grid;
		public int GroupCounter { get; private set; }
		public ObservableCollection<ObservationGrouped> Data { get; private set; }
		public ObservableCollection<GroupInfo> Group1 { get; private set; }
		public ObservableCollection<GroupInfo> Group2 { get; private set; }
		public bool ShowBand { get { return _band; } set { _band = value; Changed(nameof(ShowBand)); } }
		public bool ShowGrid { get { return _grid; } set { _grid = value; Changed(nameof(ShowGrid)); } }
		public ObservationGroupedVM(CoreDispatcher dx, IEnumerable<GroupInfo> g1, IEnumerable<GroupInfo> g2, IEnumerable<ObservationGrouped> initial) : base(dx) {
			Data = new ObservableCollection<ObservationGrouped>(initial);
			Group1 = new ObservableCollection<GroupInfo>(g1);
			Group2 = new ObservableCollection<GroupInfo>(g2);
			GroupCounter = Data.Count;
			Recalculate();
			_band = true;
			_grid = true;
		}
		void Recalculate() { }
	}
	#endregion
	#region DateRangeVM
	public class DateRangeVM : CoreViewModel, IRequireRefresh {
		/// <summary>
		/// The list of data.
		/// </summary>
		public List<Observation2> Data { get; private set; }
		protected List<Observation2> AllTheData { get; set; }
		public DateTimeOffset MinDate { get; private set; }
		public DateTimeOffset MaxDate { get; private set; }
		public DateTimeOffset Starting { get; set; }
		public DateTimeOffset Ending { get; set; }
		public DateRangeVM(CoreDispatcher dx) : base(dx) { }
		async Task<List<Observation2>> LoadTheData() {
			var sf = await Package.Current.InstalledLocation.GetFileAsync(@"Assets\MSFT.json");
			var jsonText = await Windows.Storage.FileIO.ReadTextAsync(sf);
			var json = JsonObject.Parse(jsonText);
			var prices = json.GetNamedObject("Time Series (Daily)");
			var list = new List<Observation2>();
			foreach (var price in prices) {
				// this is an object not an array so we lose the ordering
				var date = price.Key;
				var ts = DateTime.Parse(date);
				var ohlcv = price.Value.GetObject();
				var open = ohlcv.GetNamedString("1. open");
				var high = ohlcv.GetNamedString("2. high");
				var low = ohlcv.GetNamedString("3. low");
				var close = ohlcv.GetNamedString("4. close");
				var volume = ohlcv.GetNamedString("5. volume");
				var obs = new Observation2(date, double.Parse(open), double.Parse(high), double.Parse(low), double.Parse(close), int.Parse(volume)) { Timestamp = ts };
				list.Add(obs);
			}
			// since it's a Map we need to sort things based on the "date" keys
			list.Sort((o1, o2) => {
				return o1.Timestamp.CompareTo(o2.Timestamp);
			});
			return list;
		}
		async Task IRequireRefresh.RefreshAsync() {
			AllTheData = await LoadTheData();
			Data = AllTheData.Take(21).ToList();
			MinDate = AllTheData[0].Timestamp.Date;
			MaxDate = AllTheData[AllTheData.Count - 1].Timestamp.Date;
			Starting = Data[0].Timestamp.Date;
			Ending = Data[Data.Count - 1].Timestamp.Date;
			Changed(nameof(Data));
			Changed(nameof(MinDate));
			Changed(nameof(MaxDate));
			Changed(nameof(Starting));
			Changed(nameof(Ending));
		}
		public void RefreshData() {
			if (AllTheData == null) return;
			var data = AllTheData.Where(ob => ob.Timestamp >= Starting && ob.Timestamp <= Ending).ToList();
			Data = data;
			Changed(nameof(Data));
		}
	}
	#endregion
	#region TimedObservationsCore
	/// <summary>
	/// Common base for VMs that add elements based on a timer.
	/// </summary>
	public abstract class TimedObservationsCore : CoreViewModel, IRequireRefresh, IRequireRelease {
		protected readonly Random rnd = new Random();
		/// <summary>
		/// Command to toggle the timer.
		/// </summary>
		public ICommand Toggle { get; private set; }
		/// <summary>
		/// How fast to add values in MS.
		/// </summary>
		public int Speed { get; set; } = 500;
		/// <summary>
		/// Size to maintain the collection at.
		/// </summary>
		public int WindowSize { get; set; } = 30;
		/// <summary>
		/// Reflects state of timer.
		/// </summary>
		public bool IsRunning { get; private set; }
		protected Timer Timer { get; set; }
		public TimedObservationsCore(CoreDispatcher dx) : base(dx) {
			Toggle = new DelegateCommand((ox) => { if (IsRunning) StopTimer(); else StartTimer(); }, (ox) => true);
		}
		void StartTimer() {
			try {
				if (Timer == null) {
					Timer = new Timer(TimerCallback, this, Speed, Speed);
				} else {
					Timer.Change(Speed, Speed);
				}
			} finally {
				IsRunning = true;
				Changed(nameof(IsRunning));
			}
		}
		void StopTimer() {
			try {
				if (Timer != null) Timer.Change(Timeout.Infinite, Timeout.Infinite);
			} finally {
				IsRunning = false;
				Changed(nameof(IsRunning));
			}
		}
		/// <summary>
		/// Fabricate the "next" observation.
		/// </summary>
		/// <param name="label"></param>
		/// <returns></returns>
		protected Observation2 Next(string label) {
			var v1 = 4 + .5 - rnd.NextDouble();
			var v2 = 3 + .5 - rnd.NextDouble();
			var v3 = -2 + .5 - rnd.NextDouble();
			var v4 = -4 + .5 - rnd.NextDouble();
			var v5 = 10 * rnd.NextDouble();
			var obs = new Observation2(label, v1, v2, v3, v4, (int)v5);
			return obs;
		}
		/// <summary>
		/// Do the stuff when the timer tick goes off.
		/// </summary>
		protected abstract void DoTimerCallback();
		async void TimerCallback(object ox) {
			await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
				DoTimerCallback();
			});
		}
		async Task IRequireRefresh.RefreshAsync() {
			await Task.Delay(500);
			StartTimer();
		}
		void IRequireRelease.Release() {
			try {
				if (Timer != null) {
					Timer.Change(Timeout.Infinite, Timeout.Infinite);
					Timer.Dispose();
				}
			} finally {
				Timer = null;
			}
		}
	}
	#endregion
	#region TimedObservationsVM
	/// <summary>
	/// This VM demonstrates how to use a NOT <see cref="ObservableCollection{T}"/>, and leverages the <see cref="DataSource.ExternalRefresh"/> mechanism.
	/// Path recycling stabilizes once chart reaches <see cref="WindowSize"/> elements.
	/// </summary>
	public class TimedObservationsVM : TimedObservationsCore {
		/// <summary>
		/// In this VM, the group counter is bound to the <see cref="DataSource.ExternalRefresh"/> DP.
		/// Whenever the list is done changing, an <see cref="INotifyPropertyChanged"/> is triggered.
		/// <para/>
		/// NOTE that we are in manual control of when that happens (see <see cref="DoTimerCallback"/>)
		/// </summary>
		public int GroupCounter { get; private set; }
		/// <summary>
		/// The list of data.
		/// NOTE this DOES NOT implement <see cref="INotifyCollectionChanged"/>.
		/// </summary>
		public List<Observation2> Data { get; private set; }
		public TimedObservationsVM(CoreDispatcher dx) :this(dx, new List<Observation2>()) {}
		public TimedObservationsVM(CoreDispatcher dx, List<Observation2> data) :base(dx) {
			Data = data;
			GroupCounter = Data.Count;
		}
		public void ResetCounter() { GroupCounter = 0; }
		/// <summary>
		/// Since we're NOT using <see cref="ObservableCollection{T}"/>, we MUST trigger a refresh when we're done changing the list!
		/// </summary>
		protected override void DoTimerCallback() {
			RemoveHead();
			AddTail();
			Changed(nameof(GroupCounter));
		}
		void AddTail() {
			GroupCounter++;
			var obs = Next($"[{GroupCounter}]");
			Data.Add(obs);
		}
		void RemoveHead() {
			if (Data.Count > WindowSize) {
				Data.RemoveAt(0);
			}
		}
	}
	#endregion
	#region TimedObservations2VM
	/// <summary>
	/// This version uses <see cref="ObservableCollection{T}"/> and leverages the incremental update features of <see cref="Chart"/> and <see cref="DataSource"/>.
	/// </summary>
	public class TimedObservations2VM : TimedObservationsCore {
		/// <summary>
		/// In this VM, the group counter is NOT bound to the DataSource.ExternalRefresh DP.
		/// This is only used for creating value labels.
		/// </summary>
		public int GroupCounter { get; private set; }
		/// <summary>
		/// The list of data.
		/// NOTE this DOES implement <see cref="INotifyCollectionChanged"/>.
		/// </summary>
		public ObservableCollection<Observation2> Data { get; private set; }
		public TimedObservations2VM(CoreDispatcher dx) : this(dx, new List<Observation2>()) { }
		public TimedObservations2VM(CoreDispatcher dx, List<Observation2> data) : base(dx) {
			Data = new ObservableCollection<Observation2>(data);
			GroupCounter = Data.Count;
		}
		public void ResetCounter() { GroupCounter = 0; }
		/// <summary>
		/// Since we ARE using <see cref="ObservableCollection{T}"/>, nothing else required!
		/// </summary>
		protected override void DoTimerCallback() {
			RemoveHead();
			AddTail();
		}
		void AddTail() {
			var obs = Next($"[{GroupCounter++}]");
			Data.Add(obs);
		}
		void RemoveHead() {
			if (Data.Count > WindowSize) {
				Data.RemoveAt(0);
			}
		}
	}
	#endregion
}
