using eScape.Core;
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
		public ObservableCollection<Observation> Items { get; private set; } = new ObservableCollection<Observation>();
		public ObservableCollection<Observation> Items2 { get; private set; } = new ObservableCollection<Observation>();
		public MainPage() {
			this.InitializeComponent();
			InitializeDataset();
		}
		void InitializeDataset() {
			Items.Clear();
			Items.Add(new Observation { Index = 0, Value1 = 1, Value2= 0.5, Value3 = -0.25 });
			Items.Add(new Observation { Index = 1, Value1 = 2, Value2 = 1, Value3 = -1 });
			Items.Add(new Observation { Index = 2, Value1 = 3, Value2 = -1, Value3 = -2 });
			Items.Add(new Observation { Index = 3, Value1 = 4, Value2 = 2, Value3 = -3 });
			Items.Add(new Observation { Index = 4, Value1 = 5, Value2 = -2, Value3 = -4 });
			Items2.Clear();
			Items2.Add(new Observation { Index = 0, Value1 = 16, Value2 = 10.5, Value3 = -10.25 });
			Items2.Add(new Observation { Index = 1, Value1 = 4, Value2 = 10, Value3 = -10 });
			Items2.Add(new Observation { Index = 2, Value1 = 0.75, Value2 = -10, Value3 = -20 });
			Items2.Add(new Observation { Index = 3, Value1 = -4, Value2 = 20, Value3 = -30 });
			Items2.Add(new Observation { Index = 4, Value1 = -16, Value2 = -20, Value3 = -40 });
		}

		private void Chart_ChartError(eScapeLLC.UWP.Composition.Charts.Chart sender, eScapeLLC.UWP.Composition.Charts.ChartErrorEventArgs args) {
			var errors = args.Results.Select(x => x.ErrorMessage).ToArray();
			var emsg = string.Join("\t", errors);
			_trace.Error($"**ChartError {emsg}");
		}
	}
}
