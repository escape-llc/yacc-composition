using System;
using System.Collections.Generic;

namespace eScapeLLC.UWP.Charts {
	#region RecyclerBase<T>
	/// <summary>
	/// Abstract base for recyclers.
	/// Designed for one-time use; there's no "reset".
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public abstract class RecyclerBase<T> {
		#region data
		/// <summary>
		/// Internal list for bookkeeping.
		/// </summary>
		protected readonly List<T> _unused = new List<T>();
		/// <summary>
		/// Internal list for bookkeeping.
		/// </summary>
		protected readonly List<T> _created = new List<T>();
		#endregion
		#region properties
		/// <summary>
		/// Original items that were not used up by iterating.
		/// </summary>
		public IEnumerable<T> Unused { get { return _unused; } }
		/// <summary>
		/// Excess items that were created after original items were used up.
		/// </summary>
		public IEnumerable<T> Created { get { return _created; } }
		#endregion
		#region ctor
		/// <summary>
		/// Ctor.
		/// Initializes <see cref="_unused"/> with items.
		/// </summary>
		/// <param name="source">Initial list to reuse; MAY be NULL or empty.</param>
		protected RecyclerBase(IEnumerable<T> source) {
			if (source != null) _unused.AddRange(source);
		}
		#endregion
	}
	#endregion
	#region Recycler<T, S>
	/// <summary>
	/// Recycler that uses a factory method to produce instances.
	/// The <see cref="_factory"/> can receive a state parameter per call to <see cref="Next"/>.
	/// </summary>
	/// <typeparam name="T">Recycled element type.</typeparam>
	/// <typeparam name="S">Factory state type.</typeparam>
	public class Recycler<T, S> : RecyclerBase<T> {
		#region data
		readonly Func<S, T> _factory;
		#endregion
		#region ctor
		/// <summary>
		/// Ctor.
		/// </summary>
		/// <param name="source">Initial list to reuse; MAY be empty.</param>
		/// <param name="factory">Used to create new instances when SOURCE runs out.</param>
		public Recycler(IEnumerable<T> source, Func<S, T> factory) : base(source) {
			_factory = factory ?? throw new ArgumentNullException(nameof(factory));
		}
		/// <summary>
		/// Ctor.
		/// Makes an empty source, so it only creates new items.
		/// </summary>
		/// <param name="factory"></param>
		public Recycler(Func<S, T> factory) :this(new List<T>(), factory) {}
		#endregion
		#region public
		/// <summary>
		/// Return the next item.
		/// </summary>
		/// <param name="state">Some state the factory function can operate with.</param>
		/// <returns>Item1: true=created, false=reused; Item2: Another instance.</returns>
		public Tuple<bool, T> Next(S state) {
			if (_unused.Count > 0) {
				var tx = _unused[0];
				_unused.Remove(tx);
				return new Tuple<bool, T>(false, tx);
			} else {
				var tx = _factory(state);
				_created.Add(tx);
				return new Tuple<bool, T>(true, tx);
			}
		}
		#endregion
	}
	#endregion
}
