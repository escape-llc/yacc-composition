using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace eScapeLLC.UWP.Composition.Charts {
	#region Binding
	/// <summary>
	/// Value extractor.
	/// </summary>
	public abstract class Binding {
		protected Binding() { }
		/// <summary>
		/// Return value if possible.
		/// </summary>
		/// <param name="instance">Source instance.</param>
		/// <param name="value">Value receiver.</param>
		/// <returns>true: value accessed successfully; <paramref name="value"/> is valid (even if NULL). false: value not accessed; <paramref name="value"/> is NULL.</returns>
		public bool GetDateTime(object instance, out DateTime? value) {
			if (ValueType == typeof(DateTime) || ValueType == typeof(DateTime?)) {
				value = Get<DateTime>(instance);
				return true;
			}
			value = null;
			return false;
		}
		/// <summary>
		/// Return value if possible.
		/// </summary>
		/// <param name="instance">Source instance.</param>
		/// <param name="value">Value receiver.</param>
		/// <returns>true: value accessed successfully; <paramref name="value"/> is valid (even if NULL). false: value not accessed; <paramref name="value"/> is NULL.</returns>
		public bool GetDateTimeOffset(object instance, out DateTimeOffset? value) {
			if (ValueType == typeof(DateTimeOffset) || ValueType == typeof(DateTimeOffset?)) {
				value = Get<DateTimeOffset>(instance);
				return true;
			}
			value = null;
			return false;
		}
		/// <summary>
		/// Return value if possible.
		/// </summary>
		/// <param name="instance">Source instance.</param>
		/// <param name="value">Value receiver.</param>
		/// <returns>true: value accessed successfully; <paramref name="value"/> is valid (even if NULL). false: value not accessed; <paramref name="value"/> is NULL.</returns>
		public bool GetDouble(object instance, out double? value) {
			if (ValueType == typeof(double) || ValueType == typeof(double?)) {
				value = Get<double>(instance);
				return true;
			}
			value = null;
			return false;
		}

		/// <summary>
		/// Return value if possible.
		/// </summary>
		/// <param name="instance">Source instance.</param>
		/// <param name="value">Value receiver.</param>
		/// <returns>true: value accessed successfully; <paramref name="value"/> is valid (even if NULL). false: value not accessed; <paramref name="value"/> is NULL.</returns>
		public bool GetInt(object instance, out int? value) {
			if (ValueType == typeof(int) || ValueType == typeof(int?)) {
				value = Get<int>(instance);
				return true;
			}
			value = null;
			return false;
		}
		/// <summary>
		/// Return value if possible.
		/// </summary>
		/// <param name="instance">Source instance.</param>
		/// <param name="value">Value receiver.</param>
		/// <returns>true: value accessed successfully; <paramref name="value"/> is valid (even if NULL). false: value not accessed; <paramref name="value"/> is NULL.</returns>
		public bool GetString(object instance, out string value) {
			if (ValueType == typeof(string)) {
				value = Get(instance);
				return true;
			}
			else {
				value = Get(instance);
				return true;
			}
		}
		/// <summary>
		/// Return the value's type.
		/// </summary>
		public abstract Type ValueType { get; }
		/// <summary>
		/// Access the value.
		/// </summary>
		/// <typeparam name="T">Value type.</typeparam>
		/// <param name="instance">Source instance.</param>
		/// <returns>Value or NULL.</returns>
		protected abstract T? Get<T>(object instance) where T : struct;
		/// <summary>
		/// Access the value for <see cref="string"/>.
		/// C# version limitation of UWP builds (cannot use nullable reference types).
		/// </summary>
		/// <param name="instance"></param>
		/// <returns></returns>
		protected abstract string Get(object instance);
		/// <summary>
		/// Attempt to construct a <see cref="Binding"/> for the given name.
		/// Searches in order: <see cref="PropertyInfo"/>, <see cref="FieldInfo"/>, <see cref="MethodInfo"/> (zero parameters).
		/// </summary>
		/// <param name="ty">Target type.</param>
		/// <param name="name">Element name.</param>
		/// <returns>New instance or NULL.</returns>
		public static Binding For(Type ty, string name) {
			var pi = ty.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
			if(pi != null) {
				return new PropertyInfoBinding(pi);
			}
			var fi = ty.GetField(name, BindingFlags.Instance | BindingFlags.Public);
			if (fi != null) {
				return new FieldInfoBinding(fi);
			}
			var mi = ty.GetMethod(name, BindingFlags.Instance | BindingFlags.Public);
			if(mi != null && mi.GetParameters().Length == 0) {
				return new MethodInfoBinding(mi);	
			}
			return null;
		}
	}
	#endregion
	#region PropertyInfoBinding
	/// <summary>
	/// Value extractor based on <see cref="PropertyInfo"/>.
	/// </summary>
	public class PropertyInfoBinding : Binding {
		protected PropertyInfo pi;
		/// <summary>
		/// <inheritdoc/>
		/// </summary>
		public override Type ValueType => pi.PropertyType;
		public PropertyInfoBinding(PropertyInfo pi) { this.pi = pi; }
		/// <summary>
		/// <inheritdoc/>
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="instance"></param>
		/// <returns></returns>
		protected override T? Get<T>(object instance) {
			if (ValueType == typeof(T)) {
				return (T)pi.GetValue(instance);
			}
			else if (ValueType == typeof(T?)) {
				return (T?)pi.GetValue(instance);
			}
			return null;
		}
		/// <summary>
		/// <inheritdoc/>
		/// </summary>
		/// <param name="instance"></param>
		/// <returns></returns>
		protected override string Get(object instance) {
			if(ValueType == typeof(string)) return (string)pi.GetValue(instance);
			var ox = pi.GetValue(instance);
			return ox?.ToString();
		}
	}
	#endregion
	#region FieldInfoBinding
	/// <summary>
	/// Value extractor based on <see cref="FieldInfo"/>.
	/// </summary>
	public class FieldInfoBinding : Binding {
		protected FieldInfo fi;
		public override Type ValueType => fi.FieldType;
		public FieldInfoBinding(FieldInfo fi) {
			this.fi = fi;
		}
		protected override T? Get<T>(object instance) {
			if (ValueType == typeof(T)) {
				return (T)fi.GetValue(instance);
			}
			else if (ValueType == typeof(T?)) {
				return (T?)fi.GetValue(instance);
			}
			return null;
		}
		protected override string Get(object instance) {
			if (ValueType == typeof(string)) return (string)fi.GetValue(instance);
			var ox = fi.GetValue(instance);
			return ox?.ToString();
		}
	}
	#endregion
	#region MethodInfoBinding
	/// <summary>
	/// Value extract from <see cref="MethodInfo"/> call.
	/// </summary>
	public class MethodInfoBinding : Binding {
		protected MethodInfo mi;
		public override Type ValueType => mi.ReturnType;
		public MethodInfoBinding(MethodInfo mi) {
			if (mi.GetParameters().Length != 0) throw new ArgumentException($"{mi.Name} must take ZERO arguments");
			this.mi = mi;
		}
		protected override T? Get<T>(object instance) {
			if (ValueType == typeof(T)) {
				return (T)mi.Invoke(instance, (object[]) null);
			}
			else if (ValueType == typeof(T?)) {
				return (T?)mi.Invoke(instance, (object[])null);
			}
			return null;
		}
		protected override string Get(object instance) {
			if (ValueType == typeof(string)) return (string)mi.Invoke(instance, (object[])null);
			var ox = mi.Invoke(instance, (object[])null);
			return ox?.ToString();
		}
	}
	#endregion
}
