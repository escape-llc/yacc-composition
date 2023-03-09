﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace eScapeLLC.UWP.Charts.Composition {
	#region LegendBase
	/// <summary>
	/// Abstract base class for legend implementations.
	/// </summary>
	public abstract class LegendBase : ViewModelBase {
		String _title;
		/// <summary>
		/// The title.
		/// </summary>
		public string Title { get { return _title; } set { _title = value; Changed(nameof(Title)); } }
	}
	#endregion
	#region LegendWithElement
	/// <summary>
	/// Legend VM with a custom <see cref="FrameworkElement"/> for its visualization.
	/// </summary>
	public class LegendWithElement : LegendBase {
		FrameworkElement _element;
		/// <summary>
		/// The element to display in the legend.
		/// MAY come from a <see cref="DataTemplate"/>.
		/// </summary>
		public FrameworkElement Element { get { return _element; } set { _element = value; Changed(nameof(Element)); } }
	}
	#endregion
}
