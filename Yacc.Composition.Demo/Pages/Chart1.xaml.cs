using eScape.Core;
using eScape.Core.Page;
using eScapeLLC.UWP.Charts.Composition;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI.Xaml;
using Yacc.Demo.VM;

namespace Yacc.Composition.Demo.Pages {
	/// <summary>
	/// The <see cref="ViewModelPage"/> makes x:Bind more fun!
	/// </summary>
	public sealed partial class Chart1 : ViewModelPage {
		static readonly LogTools.Flag _trace = LogTools.Add("Chart1", LogTools.Level.Error);
		#region properties
		public bool ShowBand { get { return _band; } set { _band = value; Changed(nameof(ShowBand)); } }
		public bool ShowGrid { get { return _grid; } set { _grid = value; Changed(nameof(ShowGrid)); } }
		/// <summary>
		/// Submit commands to DataSource 1.  DataSource sends OperationComplete when completed.
		/// </summary>
		public DataSource_Operation CommandPort1 { get; set; }
		/// <summary>
		/// Updated on OperationComplete 1.
		/// </summary>
		public double Value1Average { get; set; }
		/// <summary>
		/// Updated on OperationComplete 1.
		/// </summary>
		public double Value2Average { get; set; }
		/// <summary>
		/// Used as DataSource 1 ItemSink. When OperationComplete, this reflects the current item list.
		/// </summary>
		public ObservableCollection<Observation> Observations1 { get; set; } = new ObservableCollection<Observation>();
		#endregion
		public Chart1() {
			this.InitializeComponent();
			InitializeDataset();
		}
		#region data
		readonly Random rnd = new Random();
		int indexcounter = 0;
		bool _band;
		bool _grid;
		#endregion
		Observation Rando() {
			var obs = new Observation(indexcounter++, 10 * rnd.NextDouble() - 5, 10 * rnd.NextDouble() - 4, 6 * rnd.NextDouble() + 2);
			return obs;
		}
		void InitializeDataset() {
			var items = new List<Observation>();
			for(int ix = 0; ix < 5; ix++) {
				items.Add(Rando());
			}
			CommandPort1 = DataSource.Reset(items);
		}
		private void Chart_ChartError(Chart sender, ChartErrorEventArgs args) {
			var errors = args.Results.Select(x => x.ErrorMessage).ToArray();
			var emsg = string.Join("\t", errors);
			_trace.Error($"**ChartError {emsg}");
		}
		private void Add_item_Click(object sender, RoutedEventArgs e) {
			var items = new List<Observation> { Rando() };
			// Use a fresh instance each time
			CommandPort1 = DataSource.Add(items);
			Changed(nameof(CommandPort1));
		}
		private void Remove_head_Click(object sender, RoutedEventArgs e) {
			CommandPort1 = DataSource.Remove(1, true);
			Changed(nameof(CommandPort1));
		}
		private void Add_and_remove_head_Click(object sender, RoutedEventArgs e) {
			var items = new List<Observation> { Rando() };
			CommandPort1 = DataSource.SlidingWindow(items);
			Changed(nameof(CommandPort1));
		}
		private void Remove_tail_Click(object sender, RoutedEventArgs e) {
			CommandPort1 = DataSource.Remove(1);
			Changed(nameof(CommandPort1));
		}
		private void Add_head_Click(object sender, RoutedEventArgs e) {
			var items = new List<Observation>() { Rando() };
			CommandPort1 = DataSource.Add(items, true);
			Changed(nameof(CommandPort1));
		}
		void Recalculate() {
			if (Observations1.Count > 0) {
				Value1Average = Observations1.Average((ob) => ob.Value1);
				Value2Average = Observations1.Average((ob) => ob.Value2);
			}
			else {
				Value1Average = 0;
				Value2Average = 0;
			}
			Changed(nameof(Value1Average));
			Changed(nameof(Value2Average));
		}
		private void OnSource1Complete(DataSource sender, OperationCompleteEventArgs e) {
			_trace.Verbose($"Source1Complete {e}");
			Recalculate();
		}
	}
}
