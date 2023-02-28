using eScape.Core;
using eScape.Core.Page;
using eScapeLLC.UWP.Charts.Composition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI.Xaml;
using Yacc.Demo.VM;

namespace Yacc.Composition.Demo.Pages {
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class Chart1 : ViewModelPage {
		static readonly LogTools.Flag _trace = LogTools.Add("Chart1", LogTools.Level.Error);
		public DataSource_Operation CommandPort1 { get; set; }
		public DataSource_Operation CommandPort2 { get; set; }
		public double Value1Average { get; set; }
		public double Value2Average { get; set; }
		public Chart1() {
			this.InitializeComponent();
			InitializeDataset();
		}
		readonly Random rnd = new Random();
		int indexcounter = 0;
		Observation Rando() {
			var obs = new Observation(indexcounter++, 10 * rnd.NextDouble() - 5, 10 * rnd.NextDouble() - 4, 6 * rnd.NextDouble() + 2);
			return obs;
		}
		void ResetCounter() { indexcounter = 0; }
		void InitializeDataset() {
			var items = new List<Observation>();
			for(int ix = 0; ix < 5; ix++) {
				items.Add(Rando());
			}
			CommandPort1 = DataSource.Reset(items);
			var items2 = new List<Observation>();
			ResetCounter();
			for (int ix = 0; ix < 5; ix++) {
				items2.Add(Rando());
			}
			CommandPort2 = DataSource.Reset(items2);
			var vm = new ObservationsVM(Dispatcher, items);
			DataContext = vm;
			Recalculate();
		}
		private void Chart_ChartError(eScapeLLC.UWP.Charts.Composition.Chart sender, eScapeLLC.UWP.Charts.Composition.ChartErrorEventArgs args) {
			var errors = args.Results.Select(x => x.ErrorMessage).ToArray();
			var emsg = string.Join("\t", errors);
			_trace.Error($"**ChartError {emsg}");
		}
		void Recalculate() {
			Value1Average = (DataContext as ObservationsVM).Value1Average;
			Changed(nameof(Value1Average));
			Value2Average = (DataContext as ObservationsVM).Value2Average;
			Changed(nameof(Value2Average));
		}
		private void Add_item_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e) {
			var obs = Rando();
			(DataContext as ObservationsVM).AddTail(obs);
			var items = new List<Observation> {
				obs
			};
			CommandPort1 = DataSource.Add(items);
			Changed(nameof(CommandPort1));
			CommandPort2 = DataSource.Add(items);
			Changed(nameof(CommandPort2));
			Recalculate();
		}
		private void Remove_head_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e) {
			(DataContext as ObservationsVM).RemoveHead();
			CommandPort1 = DataSource.Remove(1, true);
			Changed(nameof(CommandPort1));
			CommandPort2 = DataSource.Remove(1, true);
			Changed(nameof(CommandPort2));
			Recalculate();
		}
		private void Add_and_remove_head_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e) {
			var obs = Rando();
			(DataContext as ObservationsVM).AddAndRemoveHead(obs);
			var items = new List<Observation> { obs };
			_trace.Verbose($"slide it");
			CommandPort1 = DataSource.SlidingWindow(items);
			Changed(nameof(CommandPort1));
			CommandPort2 = DataSource.SlidingWindow(items);
			Changed(nameof(CommandPort2));
			Recalculate();
		}
		private void Remove_tail_Click(object sender, RoutedEventArgs e) {
			(DataContext as ObservationsVM).RemoveTail();
			CommandPort1 = DataSource.Remove(1);
			Changed(nameof(CommandPort1));
			CommandPort2 = DataSource.Remove(1);
			Changed(nameof(CommandPort2));
			Recalculate();
		}
		private void Add_head_Click(object sender, RoutedEventArgs e) {
			var obs = Rando();
			(DataContext as ObservationsVM).AddHead(obs);
			var items = new List<Observation>();
			items.Add(obs);
			CommandPort1 = DataSource.Add(items, true);
			Changed(nameof(CommandPort1));
			CommandPort2 = DataSource.Add(items, true);
			Changed(nameof(CommandPort2));
			Recalculate();
		}
	}
}
