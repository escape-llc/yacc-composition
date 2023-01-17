using eScape.Host;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace eScapeLLC.UWP.Composition.Charts {
	public class ChartComponent : FrameworkElement, IConsumer<DataContextChangedEventArgs> {
		public bool Dirty { get; protected set; }
		/// <summary>
		/// Return the name if set, otherwise the type.
		/// </summary>
		/// <returns>Name or type.</returns>
		public string NameOrType() { return string.IsNullOrEmpty(Name) ? GetType().Name : Name; }
		public void Consume(DataContextChangedEventArgs args) {
			if (DataContext != args.NewValue) {
				DataContext = args.NewValue;
			}
		}
	}
}
