using eScape.Core;
using eScapeLLC.UWP.Charts.Composition;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI.Xaml.Controls;

namespace Yacc.Composition.Demo {
	public class Observation {
		public int Index { get; set; }
		public string Label => $"Observation[{Index}]";
		public double Value1 { get; set; }
		public double Value2 { get; set; }
		public double Value3 { get; set; }
	}
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class MainPage : Page {
		static readonly LogTools.Flag _trace = LogTools.Add("MainPage", LogTools.Level.Error);
		public DataSource_Operation CommandPort1 { get; private set; }
		public DataSource_Operation CommandPort2 { get; private set; }
		public MainPage() {
			this.InitializeComponent();
			InitializeDataset();
		}
		void InitializeDataset() {
			var items = new List<Observation>();
			items.Add(new Observation { Index = 0, Value1 = 1, Value2= 0.5, Value3 = -0.25 });
			items.Add(new Observation { Index = 1, Value1 = 2, Value2 = 1, Value3 = -1 });
			items.Add(new Observation { Index = 2, Value1 = 3, Value2 = -1, Value3 = -2 });
			items.Add(new Observation { Index = 3, Value1 = 4, Value2 = 2, Value3 = -3 });
			items.Add(new Observation { Index = 4, Value1 = 5, Value2 = -2, Value3 = -4 });
			CommandPort1 = DataSource.Reset(items);
			var items2 = new List<Observation>();
			items2.Add(new Observation { Index = 0, Value1 = 16, Value2 = 10.5, Value3 = -10.25 });
			items2.Add(new Observation { Index = 1, Value1 = 4, Value2 = 10, Value3 = -10 });
			items2.Add(new Observation { Index = 2, Value1 = 0.75, Value2 = -10, Value3 = -20 });
			items2.Add(new Observation { Index = 3, Value1 = -4, Value2 = 20, Value3 = -30 });
			items2.Add(new Observation { Index = 4, Value1 = -16, Value2 = -20, Value3 = -40 });
			CommandPort2 = DataSource.Reset(items2);
		}

		private void Chart_ChartError(eScapeLLC.UWP.Charts.Composition.Chart sender, eScapeLLC.UWP.Charts.Composition.ChartErrorEventArgs args) {
			var errors = args.Results.Select(x => x.ErrorMessage).ToArray();
			var emsg = string.Join("\t", errors);
			_trace.Error($"**ChartError {emsg}");
		}
	}
}
