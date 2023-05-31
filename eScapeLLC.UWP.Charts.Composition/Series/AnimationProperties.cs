using System;
using System.Numerics;
using Windows.UI.Composition;
using Windows.UI.Xaml.Hosting;

namespace eScapeLLC.UWP.Charts.Composition.Animation {
	#region AnimationProperties
	/// <summary>
	/// Common values.
	/// </summary>
	public static class AnimationProperties {
		#region local
		public const string PROP_Position = "Position";
		public const string PARM_Position = "Position";
		public const string PARM_global = "global";
		public const string PARM_local = "local";
		public readonly static string POSITION_X = $"({PARM_local}.{PROP_Position}.X * {PARM_global}.ModelX.X + {PARM_global}.ModelX.Z) * {PARM_global}.ProjX.X + {PARM_global}.ProjX.Z";
		public readonly static string POSITION_Y = $"({PARM_local}.{PROP_Position}.Y * {PARM_global}.ModelY.Y + {PARM_global}.ModelY.Z) * {PARM_global}.ProjY.Y + {PARM_global}.ProjY.Z";
		public readonly static string OFFSET = $"Vector3({POSITION_X}, {POSITION_Y}, 0)";
		#endregion
		#region parameters
		public const string PARM_Index = "Index";
		#endregion
		#region defaults
		public const double DURATION = 300;
		#endregion
		/// <summary>
		/// Create an animation with parameter <see cref="PARM_Index"/> duration <see cref="DURATION"/> and given target.
		/// </summary>
		/// <param name="cx">Use to obtain objects.</param>
		/// <param name="name">Use in name.</param>
		/// <param name="initial">Initial value for parameter.</param>
		/// <param name="target">Animation target property.</param>
		/// <returns></returns>
		public static Vector3KeyFrameAnimation Model(Compositor cx, string name, Vector3 initial, string target) {
			var kfa = cx.CreateVector3KeyFrameAnimation();
			kfa.Comment = $"{name}_{target}";
			kfa.InsertExpressionKeyFrame(1f, PARM_Index);
			kfa.SetVector3Parameter(PARM_Index, initial);
			kfa.Duration = TimeSpan.FromMilliseconds(DURATION);
			kfa.StopBehavior = AnimationStopBehavior.SetToFinalValue;
			kfa.Target = target;
			return kfa;
		}
	}
	#endregion
	#region Global
	/// <summary>
	/// Maintains and animates the global Model/Projection transforms.  The animation target is the <see cref="PropertySet"/> property.
	/// </summary>
	public class Global : IDisposable {
		#region names
		public const string PROP_ModelX = "ModelX";
		public const string PROP_ModelY = "ModelY";
		public const string PROP_ProjX = "ProjX";
		public const string PROP_ProjY = "ProjY";
		#endregion
		#region data
		/// <summary>
		/// For Dispose pattern.
		/// </summary>
		private bool disposedValue;
		/// <summary>
		/// Init transform animation values exactly once.
		/// </summary>
		private bool isModelInit;
		private bool isProjInit;
		#endregion
		#region properties
		/// <summary>
		/// Access to the last-updated model from <see cref="Model(Matrix3x2)"/>.
		/// </summary>
		public Matrix3x2 CurrentModel { get; protected set; }
		/// <summary>
		/// Access to the last-updated projection from <see cref="Projection(Matrix3x2)"/>.
		/// </summary>
		public Matrix3x2 CurrentProjection { get; protected set; }
		/// <summary>
		/// Access to the global properties.
		/// This is the target of animations.  "Link" this to other property sets driving individual items.
		/// </summary>
		public CompositionPropertySet PropertySet { get; protected set; }
		Vector3KeyFrameAnimation ModelX { get; set; }
		Vector3KeyFrameAnimation ModelY { get; set; }
		Vector3KeyFrameAnimation ProjX { get; set; }
		Vector3KeyFrameAnimation ProjY { get; set; }
		#endregion
		#region ctor
		/// <summary>
		/// Ctor.
		/// Acquires composition resources.
		/// </summary>
		/// <param name="cx">Use to acquire objects.</param>
		/// <param name="name">Use for names.</param>
		public Global(Compositor cx, string name) {
			PropertySet = cx.CreatePropertySet();
			PropertySet.Comment = $"{name}_global";
			PropertySet.InsertVector3(PROP_ModelX, new Vector3(1, 0, 0));
			PropertySet.InsertVector3(PROP_ModelY, new Vector3(0, 1, 0));
			PropertySet.InsertVector3(PROP_ProjX, new Vector3(1, 0, 0));
			PropertySet.InsertVector3(PROP_ProjY, new Vector3(0, 1, 0));
			ModelX = AnimationProperties.Model(cx, name, new Vector3(1, 0, 0), PROP_ModelX);
			ModelY = AnimationProperties.Model(cx, name, new Vector3(0, 1, 0), PROP_ModelY);
			ProjX = AnimationProperties.Model(cx, name, new Vector3(1, 0, 0), PROP_ProjX);
			ProjY = AnimationProperties.Model(cx, name, new Vector3(0, 1, 0), PROP_ProjY);
		}
		#endregion
		#region public
		/// <summary>
		/// Apply logic for the Model transform.
		/// If the matrix changed, animations are started on the property set.
		/// </summary>
		/// <param name="model">Model transform.</param>
		public virtual void Model(Matrix3x2 model) {
			var a1 = new Vector3(model.M11, model.M21, model.M31);
			var a2 = new Vector3(model.M12, model.M22, model.M32);
			if (!isModelInit) {
				PropertySet.InsertVector3(PROP_ModelX, a1);
				PropertySet.InsertVector3(PROP_ModelY, a2);
				isModelInit = true;
			}
			if (model == CurrentModel) return;
			CurrentModel = model;
			ModelX.SetVector3Parameter(AnimationProperties.PARM_Index, a1);
			ModelY.SetVector3Parameter(AnimationProperties.PARM_Index, a2);
			PropertySet.StartAnimation(ModelX.Target, ModelX);
			PropertySet.StartAnimation(ModelY.Target, ModelY);
		}
		/// <summary>
		/// Apply logic for the Projection transform.
		/// If the matrix changed, animations are started on the property set.
		/// </summary>
		/// <param name="proj">Projection transform.</param>
		public virtual void Projection(Matrix3x2 proj) {
			var a1 = new Vector3(proj.M11, proj.M21, proj.M31);
			var a2 = new Vector3(proj.M12, proj.M22, proj.M32);
			if (!isProjInit) {
				PropertySet.InsertVector3(PROP_ProjX, a1);
				PropertySet.InsertVector3(PROP_ProjY, a2);
				isProjInit = true;
			}
			if (proj == CurrentProjection) return;
			CurrentProjection = proj;
			ProjX.SetVector3Parameter(AnimationProperties.PARM_Index, a1);
			ProjY.SetVector3Parameter(AnimationProperties.PARM_Index, a2);
			PropertySet.StartAnimation(ProjX.Target, ProjX);
			PropertySet.StartAnimation(ProjY.Target, ProjY);
		}
		#endregion
		#region Dispose pattern
		protected virtual void Dispose(bool disposing) {
			if (!disposedValue) {
				if (disposing) {
					// dispose managed state (managed objects)
					ProjX?.Dispose(); ProjX = null;
					ProjY?.Dispose(); ProjY = null;
					ModelX?.Dispose(); ModelX = null;
					ModelY?.Dispose(); ModelY = null;
					PropertySet?.Dispose(); PropertySet = null;
				}
				disposedValue = true;
			}
		}
		public void Dispose() {
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
		#endregion
	}
	#endregion
	#region ModelTransform
	/// <summary>
	/// Animates the Model for <see cref="CompositionShape.TransformMatrix"/>.
	/// </summary>
	public class ModelTransform : IDisposable {
		public const string PROP_Component1 = "Component1";
		public const string PROP_Component2 = "Component2";
		private bool isModelInit;
		private bool disposedValue;
		#region properties
		/// <summary>
		/// Access to the last-updated model from <see cref="Model(Matrix3x2)"/>.
		/// </summary>
		public Matrix3x2 CurrentModel { get; protected set; }
		/// <summary>
		/// Access to property set hosting animated values.
		/// </summary>
		public CompositionPropertySet PropertySet { get; private set; }
		Vector3KeyFrameAnimation Axis1 { get; set; }
		Vector3KeyFrameAnimation Axis2 { get; set; }
		ExpressionAnimation Transform { get; set; }
		#endregion
		#region ctor
		public ModelTransform(Compositor cx, string name, int dur) {
			Axis1 = cx.CreateVector3KeyFrameAnimation();
			Axis1.InsertExpressionKeyFrame(1f, PROP_Component1);
			Axis1.Duration = TimeSpan.FromMilliseconds(dur);
			Axis1.StopBehavior = AnimationStopBehavior.SetToFinalValue;
			Axis1.Target = PROP_Component1;
			Axis1.Comment = PROP_Component1;
			Axis2 = cx.CreateVector3KeyFrameAnimation();
			Axis2.InsertExpressionKeyFrame(1f, PROP_Component2);
			Axis2.Duration = TimeSpan.FromMilliseconds(dur);
			Axis2.StopBehavior = AnimationStopBehavior.SetToFinalValue;
			Axis2.Target = PROP_Component2;
			Axis2.Comment = PROP_Component2;
			PropertySet = cx.CreatePropertySet();
			PropertySet.Comment = $"{name}_Axisprops";
			PropertySet.InsertVector3(PROP_Component1, new Vector3(1, 0, 0));
			PropertySet.InsertVector3(PROP_Component2, new Vector3(0, 1, 0));
			// Transform is an expression animating the Matrix3x2 based on the axisprops
			// entering components are connected to this animation
			Transform = cx.CreateExpressionAnimation();
			Transform.Expression = "Matrix3x2(props.Component1.X, props.Component2.X, props.Component1.Y, props.Component2.Y, props.Component1.Z, props.Component2.Z)";
			Transform.SetExpressionReferenceParameter("props", PropertySet);
			Transform.Target = nameof(CompositionShape.TransformMatrix);
			Transform.Comment = $"{name}_TransformMatrix";
		}
		#endregion
		#region public
		/// <summary>
		/// Manage animations for the model transform when it changes.
		/// </summary>
		/// <param name="model">New value for model; MAY equal previous value.</param>
		public virtual void Model(Matrix3x2 model) {
			var a1 = new Vector3(model.M11, model.M21, model.M31);
			var a2 = new Vector3(model.M12, model.M22, model.M32);
			if (!isModelInit) {
				Axis1.SetVector3Parameter(PROP_Component1, a1);
				Axis2.SetVector3Parameter(PROP_Component2, a2);
				PropertySet.InsertVector3(PROP_Component1, a1);
				PropertySet.InsertVector3(PROP_Component2, a2);
				isModelInit = true;
			}
			if (model == CurrentModel) return;
			CurrentModel = model;
			Axis1.SetVector3Parameter(PROP_Component1, a1);
			Axis2.SetVector3Parameter(PROP_Component2, a2);
			PropertySet.StartAnimation(Axis1.Target, Axis1);
			PropertySet.StartAnimation(Axis2.Target, Axis2);
		}
		/// <summary>
		/// Start animation(s).
		/// </summary>
		/// <param name="co">Target object.</param>
		public virtual void Start(CompositionObject co) {
			co.StartAnimation(Transform.Target, Transform);
		}
		/// <summary>
		/// Stop animation(s).
		/// </summary>
		/// <param name="co">Target object.</param>
		public virtual void Stop(CompositionObject co) {
			co.StopAnimation(Transform.Target);
		}
		#endregion
		#region Dispose pattern
		protected virtual void InternalDispose() {
			Axis1?.Dispose(); Axis1 = null;
			Axis2?.Dispose(); Axis2 = null;
			PropertySet?.Dispose(); PropertySet = null;
		}
		void Dispose(bool disposing) {
			if (!disposedValue) {
				if (disposing) {
					InternalDispose();
				}
				disposedValue = true;
			}
		}
		public void Dispose() {
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
		#endregion
	}
	#endregion
	#region LocalVisual
	/// <summary>
	/// Core of the per-item composition animations.
	/// </summary>
	public class LocalVisual : IDisposable {
		private bool disposedValue;
		#region properties
		public CompositionPropertySet PropertySet { get; private set; }
		protected Vector2KeyFrameAnimation Position { get; set; }
		#endregion
		#region ctor
		/// <summary>
		/// Ctor.
		/// </summary>
		/// <param name="cx">Use for obtaining objects.</param>
		/// <param name="global">Global property set.</param>
		public LocalVisual(Compositor cx, CompositionPropertySet global) {
			if (cx == null) throw new ArgumentNullException(nameof(cx));
			if (global == null) throw new ArgumentNullException(nameof(global));
			Position = cx.CreateVector2KeyFrameAnimation();
			Position.Comment = $"Local[{GetHashCode()}]_Position";
			Position.StopBehavior = AnimationStopBehavior.SetToFinalValue;
			Position.InsertExpressionKeyFrame(1f, AnimationProperties.PARM_Position);
			Position.Target = AnimationProperties.PROP_Position;
			PropertySet = cx.CreatePropertySet();
			PropertySet.Comment = $"Local[{GetHashCode()}]_PropertySet";
		}
		#endregion
		#region Dispose pattern
		protected void Dispose(bool disposing) {
			if (!disposedValue) {
				if (disposing) {
					// dispose managed state (managed objects)
					InternalDispose();
				}
				disposedValue = true;
			}
		}
		public void Dispose() {
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
		#endregion
		#region extension points
		protected virtual void InternalStop(Visual element) { }
		protected virtual void InternalEnter(Visual element) { }
		protected virtual void InternalDispose() {
			Position?.Dispose(); Position = null;
			PropertySet?.Dispose(); PropertySet = null;
		}
		#endregion
		#region public
		/// <summary>
		/// Initialize the starting location for subsequent animations.
		/// </summary>
		/// <param name="initial">Initial location.</param>
		public virtual void Initial(Vector2 initial) {
			Position.SetVector2Parameter(AnimationProperties.PARM_Position, initial);
			PropertySet.InsertVector2(AnimationProperties.PROP_Position, initial);
		}
		/// <summary>
		/// Terminate all animations.
		/// </summary>
		/// <param name="element"></param>
		public virtual void Stop(Visual element) {
			if (element != null) {
				InternalStop(element);
			}
			PropertySet.StopAnimation(Position.Target);
		}
		/// <summary>
		/// Initialize location and start animations.
		/// </summary>
		/// <param name="element">Target element MUST NOT be NULL.</param>
		/// <param name="position">Entry location.</param>
		public virtual void Enter(Visual element, Vector2 position) {
			PropertySet.InsertVector2(AnimationProperties.PROP_Position, position);
			Position.SetVector2Parameter(AnimationProperties.PARM_Position, position);
			if(element != null) {
				InternalEnter(element);
			}
		}
		/// <summary>
		/// Start animation to the given location.
		/// </summary>
		/// <param name="position">New location.</param>
		public virtual void To(Vector2 position) {
			Position.SetVector2Parameter(AnimationProperties.PARM_Position, position);
			PropertySet.StartAnimation(Position.Target, Position);
		}
		#endregion
	}
	#endregion
	#region MarkerBrush
	/// <summary>
	/// Maintains animations for a "Marker Brush" which is a rectangle placed at the World Coordinates.
	/// The Marker Width/Height is in Category Units and is the basis for the size calculations.
	/// </summary>
	public class MarkerBrush : LocalVisual {
		/// <summary>
		/// Name of property to place in GLOBAL property set for C1 Horizontal.
		/// </summary>
		public const string PROP_MarkerWidth = "MarkerWidth";
		/// <summary>
		/// Name of property to place in GLOBAL property set for C1 Vertical.
		/// </summary>
		public const string PROP_MarkerHeight = "MarkerHeight";
		/// <summary>
		/// Name of property to place in GLOBAL property set.
		/// </summary>
		public const string PROP_AspectRatio = "AspectRatio";
		readonly static string C1_HORZ = $"{AnimationProperties.PARM_global}.MarkerWidth*{AnimationProperties.PARM_global}.ModelX.X*{AnimationProperties.PARM_global}.ProjX.X";
		readonly static string C1_VERT = $"{AnimationProperties.PARM_global}.MarkerHeight*{AnimationProperties.PARM_global}.ModelY.Y*{AnimationProperties.PARM_global}.ProjY.Y";
		readonly static string SIZE_HORZ = $"Vector2({C1_HORZ}, ({C1_HORZ})*{AnimationProperties.PARM_global}.AspectRatio)";
		readonly static string SIZE_VERT = $"Vector2(({C1_VERT})*{AnimationProperties.PARM_global}.AspectRatio, {C1_VERT})";
		#region properties
		ExpressionAnimation Offset { get; set; }
		ExpressionAnimation Size { get; set; }
		#endregion
		#region ctor
		/// <summary>
		/// Ctor.
		/// </summary>
		/// <param name="cx">Use to obtain objects.</param>
		/// <param name="global">Use for GLOBAL property set.</param>
		public MarkerBrush(Compositor cx, CompositionPropertySet global) :base(cx, global) {
			Offset = cx.CreateExpressionAnimation(AnimationProperties.OFFSET);
			Offset.Comment = $"Marker[{GetHashCode()}]_Offset";
			Offset.SetExpressionReferenceParameter(AnimationProperties.PARM_local, PropertySet);
			Offset.SetExpressionReferenceParameter(AnimationProperties.PARM_global, global);
			Offset.Target = nameof(Visual.Offset);
			Size = cx.CreateExpressionAnimation(SIZE_HORZ);
			Size.Comment = $"Marker[{GetHashCode()}]_Size";
			Size.SetExpressionReferenceParameter(AnimationProperties.PARM_local, PropertySet);
			Size.SetExpressionReferenceParameter(AnimationProperties.PARM_global, global);
			Size.Target = nameof(Visual.Size);
		}
		#endregion
		#region extensions
		protected override void InternalStop(Visual element) {
			element.StopAnimation(Size.Target);
			element.StopAnimation(Offset.Target);
		}
		protected override void InternalEnter(Visual element) {
			element.StartAnimation(Offset.Target, Offset);
			element.StartAnimation(Size.Target, Size);
		}
		#endregion
		#region Dispose pattern
		/// <summary>
		/// Subclasses MUST call base version of <see cref="InternalDispose"/>.
		/// </summary>
		protected override void InternalDispose() {
			base.InternalDispose();
			Offset?.Dispose(); Offset = null;
			Size?.Dispose(); Size = null;
		}
		#endregion
	}
	#endregion
	#region XamlElement
	/// <summary>
	/// Manages the <see cref="Windows.UI.Xaml.UIElement.Translation"/> property instead of <see cref="Visual.Offset"/> to animate a <see cref="Windows.UI.Xaml.UIElement"/>.
	/// </summary>
	public class XamlElement : LocalVisual {
		#region properties
		ExpressionAnimation Translate { get; set; }
		#endregion
		#region ctor
		/// <summary>
		/// Ctor.
		/// </summary>
		/// <param name="cx">Use to obtain objects.</param>
		/// <param name="global">Use for GLOBAL property set.</param>
		public XamlElement(Compositor cx, CompositionPropertySet global) :base(cx, global) {
			Translate = cx.CreateExpressionAnimation(AnimationProperties.OFFSET);
			Translate.Comment = $"UIElement[{GetHashCode()}]_Translation";
			Translate.SetExpressionReferenceParameter(AnimationProperties.PARM_local, PropertySet);
			Translate.SetExpressionReferenceParameter(AnimationProperties.PARM_global, global);
			Translate.Target = nameof(Windows.UI.Xaml.UIElement.Translation);
		}
		#endregion
		#region extensions
		protected override void InternalStop(Visual element) {
			element.StopAnimation(Translate.Target);
		}
		protected override void InternalEnter(Visual element) {
			element.StartAnimation(Translate.Target, Translate);
		}
		protected override void InternalDispose() {
			base.InternalDispose();
			Translate?.Dispose(); Translate = null;
		}
		#endregion
		#region public
		/// <summary>
		/// Terminate all animations.
		/// </summary>
		/// <param name="uie">Use to obtain <see cref="Visual"/> or NULL.</param>
		public virtual void Stop(Windows.UI.Xaml.UIElement uie) {
			Visual element = uie != null ? ElementCompositionPreview.GetElementVisual(uie) : null;
			Stop(element);
		}
		/// <summary>
		/// Initialize location and start animations.
		/// </summary>
		/// <param name="uie">Use to obtain <see cref="Visual"/> or NULL.</param>
		/// <param name="position">Entry location.</param>
		public virtual void Enter(Windows.UI.Xaml.UIElement uie, Vector2 position) {
			Visual element = uie != null ? ElementCompositionPreview.GetElementVisual(uie) : null;
			Enter(element, position);
		}
		#endregion
	}
	#endregion
}
