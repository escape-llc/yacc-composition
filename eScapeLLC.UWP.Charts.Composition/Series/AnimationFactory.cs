using eScape.Core;
using System;
using System.Numerics;
using Windows.UI.Composition;

namespace eScapeLLC.UWP.Charts.Composition.Factory {
	#region AnimationFactory
	/// <summary>
	/// Factory for the <see cref="AnimationController"/>.
	/// </summary>
	public class AnimationFactory : IAnimationFactory {
		public const int DEFAULT = 400;
		#region properties
		/// <summary>
		/// Duration of "shift" animation in MS.
		/// </summary>
		public int DurationShift { get; set; } = DEFAULT;
		/// <summary>
		/// Duration of "transformMatrix" animation in MS.
		/// </summary>
		public int DurationTransform { get; set; } = DEFAULT;
		/// <summary>
		/// Duration of "enter" animation in MS.
		/// </summary>
		public int DurationEnter { get; set; } = DEFAULT;
		/// <summary>
		/// Duration of "exit" animation in MS.
		/// </summary>
		public int DurationExit { get; set; } = DEFAULT;
		#endregion
		public IAnimationController CreateAnimationController(Compositor cc) {
			var iac = new AnimationController() {
				DurationEnter = DurationEnter,
				DurationExit = DurationExit,
				DurationShift = DurationShift,
				DurationTransform = DurationTransform
			};
			iac.Prepare(cc);
			return iac;
		}
	}
	#endregion
	#region NullAnimationController
	/// <summary>
	/// NULL-Object pattern.
	/// </summary>
	public class NullAnimationController : IAnimationController {
		public ImplicitAnimationCollection CreateImplcit(IElementFactoryContext iefc) {
			return null;
		}
		public void Dispose() { }
		public bool Enter(IElementFactoryContext iefc, CompositionObject co, CompositionShapeCollection ssc, Action<CompositionObject> cb = null) {
			if(co is CompositionShape cs) {
				ssc.Add(cs);
				cb?.Invoke(co);
				return true;
			}
			return false;
		}
		public bool Exit(IElementFactoryContext iefc, CompositionObject co, CompositionShapeCollection ssc, Action<CompositionObject> cb = null) {
			if(co is CompositionShape cs) {
				ssc.Remove(cs);
				cb?.Invoke(co);
				return true;
			}
			return false;
		}
		public void InitTransform(Matrix3x2 model) { }
		public bool Offset(IElementFactoryContext iefc, CompositionObject co, Action<CompositionObject> cb = null) {
			return false;
		}
		public void Transform(IElementFactoryContext iefc, Matrix3x2 model) { }
	}
	#endregion
	#region AnimationController
	/// <summary>
	/// Provide default animations for the chart elements: Enter, Exit, Offset, TransformMatrix.
	/// </summary>
	public class AnimationController : IAnimationController {
		static readonly LogTools.Flag _trace = LogTools.Add("AnimationController", LogTools.Level.Error);
		#region properties
		/// <summary>
		/// Duration of "shift" animation in MS.
		/// </summary>
		public int DurationShift { get; set; } = AnimationFactory.DEFAULT;
		/// <summary>
		/// Duration of "transformMatrix" animation in MS.
		/// </summary>
		public int DurationTransform { get; set; } = AnimationFactory.DEFAULT;
		/// <summary>
		/// Duration of "enter" animation in MS.
		/// </summary>
		public int DurationEnter { get; set; } = AnimationFactory.DEFAULT;
		/// <summary>
		/// Duration of "exit" animation in MS.
		/// </summary>
		public int DurationExit { get; set; } = AnimationFactory.DEFAULT;
		#endregion
		#region data
		Vector2KeyFrameAnimation offset;
		ExpressionAnimation xform;
		ImplicitAnimationCollection iac;
		Vector2KeyFrameAnimation enter;
		Vector2KeyFrameAnimation exit;
		CompositionPropertySet axisprops;
		Vector3KeyFrameAnimation axis1;
		Vector3KeyFrameAnimation axis2;
		private bool disposedValue;
		#endregion
		#region helpers
		/// <summary>
		/// Calculate the spawn point.
		/// </summary>
		/// <param name="ieec"></param>
		/// <param name="iedo"></param>
		/// <returns></returns>
		Vector2 Spawn(IElementCategoryValueContext ieec, IElementDataOperation iedo) {
			double c1 = iedo.Transition == ItemTransition.Head
				? ieec.CategoryAxis.Minimum - 2 + ieec.Item.CategoryOffset
				: ieec.CategoryAxis.Maximum + 2 + ieec.Item.CategoryOffset;
			return MappingSupport.OffsetForColumn(c1, ieec.CategoryAxis.Orientation, ieec.Item.DataValue, ieec.ValueAxis.Orientation);
		}
		void Enter_CategoryValue(CompositionShapeCollection ssc, CompositionShape cs, IElementCategoryValueContext ieec, IElementDataOperation iedo, Action<CompositionObject> cb) {
			var enter = Spawn(ieec, iedo);
			_trace.Verbose($"Enter {cs.Comment} {iedo.Transition} spawn:({enter.X},{enter.Y})");
			// Enter VT at spawn point
			cs.Offset = enter;
			if (iedo.Transition == ItemTransition.Head) {
				ssc.Insert(0, cs);
			}
			else {
				ssc.Add(cs);
			}
			cb?.Invoke(cs);
			if (this.enter.Target != nameof(CompositionShape.Offset)) {
				// if it's Offset we expect a call for that next, otherwise start this one
				cs.StartAnimation(this.enter.Target, this.enter);
			}
			// connect to expression for TransformMatrix
			cs.StartAnimation(xform.Target, xform);
		}
		void Exit_CategoryValue(CompositionShapeCollection ssc, CompositionShape cs, IElementFactoryContext iefc, IElementCategoryValueContext ieec, IElementDataOperation iedo, Action<CompositionObject> cb) {
			CompositionScopedBatch ccb = iefc.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
			ccb.Comment = $"ScopedBatch_{cs.Comment}";
			ccb.Completed += (sender, cbcea) => {
				try {
					// needed?
					cs.StopAnimation(xform.Target);
					ssc.Remove(cs);
					cb?.Invoke(cs);
				}
				catch (Exception ex) {
					_trace.Error($"ccb.Completed: {ex}");
				}
				finally {
					ccb.Dispose();
				}
			};
			var exit = Spawn(ieec, iedo);
			_trace.Verbose($"Exit {cs.Comment} {iedo.Transition} spawn:({exit.X},{exit.Y})");
			this.exit.SetVector2Parameter("Index", exit);
			cs.StartAnimation(this.exit.Target, this.exit);
			ccb.End();
		}
		void Offset_CategoryValue(CompositionShape cs, IElementCategoryValueContext ieec) {
			var vxx = MappingSupport.OffsetForColumn(
				ieec.Item.CategoryValue + ieec.Item.CategoryOffset, ieec.CategoryAxis.Orientation,
				ieec.Item.DataValue, ieec.ValueAxis.Orientation);
			_trace.Verbose($"Offset {cs.Comment} [{ieec.Item.CategoryValue}] move:({vxx.X},{vxx.Y})");
			if (vxx != cs.Offset) {
				offset.SetVector2Parameter("Index", vxx);
				cs.StartAnimation(offset.Target, offset);
			}
		}
		void Offset_Value(CompositionShape cs, IElementExtentContext ieexc, IElementValueContext ievc) {
			var vxx = MappingSupport.OffsetFor(
				0, ieexc.Component1Axis.Orientation,
				ievc.Value, ieexc.Component2Axis.Orientation);
			_trace.Verbose($"Offset {cs.Comment} [0] move:({vxx.X},{vxx.Y})");
			if (vxx != cs.Offset) {
				offset.SetVector2Parameter("Index", vxx);
				cs.StartAnimation(offset.Target, offset);
			}
		}
		#endregion
		#region IAnimationController
		public void Prepare(Compositor cc) {
			#region Offset
			Vector2KeyFrameAnimation offset = cc.CreateVector2KeyFrameAnimation();
			offset.InsertExpressionKeyFrame(1f, "Index");
			offset.Duration = TimeSpan.FromMilliseconds(DurationShift);
			offset.StopBehavior = AnimationStopBehavior.SetToFinalValue;
			offset.Target = nameof(CompositionShape.Offset);
			offset.Comment = "Offset";
			this.offset = offset;
			#endregion
			#region Enter
			var enter = cc.CreateVector2KeyFrameAnimation();
			enter.InsertExpressionKeyFrame(1f, "this.FinalValue");
			enter.Duration = TimeSpan.FromMilliseconds(DurationEnter);
			enter.Target = nameof(CompositionShape.Offset);
			enter.Comment = "Enter";
			this.enter = enter;
			#endregion
			#region Exit
			var exit = cc.CreateVector2KeyFrameAnimation();
			exit.InsertExpressionKeyFrame(1f, "Index");
			exit.Duration = TimeSpan.FromMilliseconds(DurationExit);
			exit.Target = nameof(CompositionShape.Offset);
			exit.Comment = "Exit";
			this.exit = exit;
			#endregion
			#region Transform
			// axis1 has a separate keyframe animation to drive it
			var axis1 = cc.CreateVector3KeyFrameAnimation();
			axis1.InsertExpressionKeyFrame(1f, "Component1");
			axis1.Duration = TimeSpan.FromMilliseconds(DurationTransform);
			axis1.StopBehavior = AnimationStopBehavior.SetToFinalValue;
			axis1.Target = "Component1";
			axis1.Comment = "Component1";
			this.axis1 = axis1;
			// axis2 has a separate keyframe animation to drive it
			var axis2 = cc.CreateVector3KeyFrameAnimation();
			axis2.InsertExpressionKeyFrame(1f, "Component2");
			axis2.Duration = TimeSpan.FromMilliseconds(DurationTransform);
			axis2.StopBehavior = AnimationStopBehavior.SetToFinalValue;
			axis2.Target = "Component2";
			axis2.Comment = "Component2";
			this.axis2 = axis2;
			// axisprops holds the animated values from axis1/axis2
			// this holds the axis animation targets
			var axisprops = cc.CreatePropertySet();
			axisprops.Comment = "Axisprops";
			axisprops.InsertVector3("Component1", new Vector3(1, 0, 0));
			axisprops.InsertVector3("Component2", new Vector3(0, 1, 0));
			this.axisprops = axisprops;
			// xform is an expression animating the Matrix3x2 based on the axisprops
			// entering components are connected to this animation
			var xform = cc.CreateExpressionAnimation();
			xform.Expression = "Matrix3x2(props.Component1.X,props.Component2.X,props.Component1.Y,props.Component2.Y,props.Component1.Z,props.Component2.Z)";
			xform.SetExpressionReferenceParameter("props", this.axisprops);
			xform.Target = nameof(CompositionShape.TransformMatrix);
			xform.Comment = "TransformMatrix";
			this.xform = xform;
			#endregion
			#region Implicit
			var iac = cc.CreateImplicitAnimationCollection();
			iac[nameof(CompositionShape.Offset)] = offset;
			iac[nameof(CompositionShape.TransformMatrix)] = this.xform;
			iac.Comment = "Implicit";
			this.iac = iac;
			#endregion
		}
		/// <summary>
		/// Initialize transform components before first use of <see cref="Transform(IElementFactoryContext, Matrix3x2)"/>.
		/// Default components form the Identity Matrix.
		/// </summary>
		/// <param name="model">Use to initialize animation properties.</param>
		public void InitTransform(Matrix3x2 model) {
			_trace.Verbose($"InitTransform {model}");
			axis1.SetVector3Parameter("Component1", new Vector3(model.M11, model.M21, model.M31));
			axis2.SetVector3Parameter("Component2", new Vector3(model.M12, model.M22, model.M32));
			axisprops.InsertVector3("Component1", new Vector3(model.M11, model.M21, model.M31));
			axisprops.InsertVector3("Component2", new Vector3(model.M12, model.M22, model.M32));
		}
		public ImplicitAnimationCollection CreateImplcit(IElementFactoryContext iefc) {
			return iac;
		}
		public bool Enter(IElementFactoryContext iefc, CompositionObject co, CompositionShapeCollection ssc, Action<CompositionObject> cb = null) {
			if (co is CompositionShape cs && iefc is IElementCategoryValueContext ieec && iefc is IElementDataOperation iedo) {
				Enter_CategoryValue(ssc, cs, ieec, iedo, cb);
				return true;
			}
			return false;
		}
		public bool Exit(IElementFactoryContext iefc, CompositionObject co, CompositionShapeCollection ssc, Action<CompositionObject> cb = null) {
			if (co is CompositionShape cs2 && iefc is IElementCategoryValueContext ieec2 && iefc is IElementDataOperation iedo2) {
				Exit_CategoryValue(ssc, cs2, iefc, ieec2, iedo2, cb);
				return true;
			}
			return false;
		}
		public void Transform(IElementFactoryContext iefc, Matrix3x2 model) {
			_trace.Verbose($"Transform {model}");
			axis1.SetVector3Parameter("Component1", new Vector3(model.M11, model.M21, model.M31));
			axis2.SetVector3Parameter("Component2", new Vector3(model.M12, model.M22, model.M32));
			axisprops.StartAnimation(axis1.Target, axis1);
			axisprops.StartAnimation(axis2.Target, axis2);
		}
		public bool Offset(IElementFactoryContext iefc, CompositionObject co, Action<CompositionObject> cb = null) {
			if (co is CompositionShape cs) {
				switch (iefc) {
					case IElementCategoryValueContext ieec:
						Offset_CategoryValue(cs, ieec);
						cb?.Invoke(co);
						return true;
					case IElementValueContext ievc:
						if (iefc is IElementExtentContext ieexc) {
							Offset_Value(cs, ieexc, ievc);
							cb?.Invoke(co);
							return true;
						}
						break;
				}
			}
			return false;
		}
		#endregion
		#region Dispose
		protected virtual void Dispose(bool disposing) {
			if (!disposedValue) {
				if (disposing) {
					xform?.Dispose(); xform = null;
					axisprops?.Dispose(); axisprops = null;
					axis2?.Dispose(); axis2 = null;
					axis1?.Dispose(); axis1 = null;
					offset?.Dispose(); offset = null;
					exit?.Dispose(); exit = null;
					enter?.Dispose(); enter = null;
					iac?.Dispose(); iac = null;
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
}
