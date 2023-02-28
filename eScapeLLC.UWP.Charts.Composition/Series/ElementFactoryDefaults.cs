using eScape.Core;
using System;
using System.ComponentModel;
using System.Numerics;
using Windows.ApplicationModel.UserDataTasks;
using Windows.Media.Devices;
using Windows.UI.Composition;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace eScapeLLC.UWP.Charts.Composition.Factory {
	#region AnimationFactory
	public static class AnimationKeys {
		public const string ENTER = "Enter";
		public const string EXIT = "Exit";
		public const string TRANSFORM = "Transform";
		public const string OFFSET = "Offset";
	}
	/// <summary>
	/// Provide default animations for the chart elements.
	/// </summary>
	public class AnimationFactory : IAnimationFactory {
		static readonly LogTools.Flag _trace = LogTools.Add("AnimationFactory", LogTools.Level.Error);
		/// <summary>
		/// Duration of "shift" animation in MS.
		/// </summary>
		public int DurationShift { get; set; } = 500;
		/// <summary>
		/// Duration of "enter" animation in MS.
		/// </summary>
		public int DurationEnter { get; set; } = 500;
		/// <summary>
		/// Duration of "exit" animation in MS.
		/// </summary>
		public int DurationExit { get; set; } = 500;
		Vector2KeyFrameAnimation shift;
		ExpressionAnimation xform;
		ImplicitAnimationCollection iac;
		Vector2KeyFrameAnimation enter;
		Vector2KeyFrameAnimation exit;
		public void Prepare(Compositor cc) {
			Vector2KeyFrameAnimation shift = cc.CreateVector2KeyFrameAnimation();
			shift.InsertExpressionKeyFrame(1f, "Index");
			shift.Duration = TimeSpan.FromMilliseconds(DurationShift);
			shift.StopBehavior = AnimationStopBehavior.SetToFinalValue;
			shift.Target = nameof(CompositionShape.Offset);
			shift.Comment = "Shift";
			this.shift = shift;
			var enter = cc.CreateVector2KeyFrameAnimation();
			enter.InsertExpressionKeyFrame(1f, "this.FinalValue");
			enter.Duration = TimeSpan.FromMilliseconds(DurationEnter);
			enter.Target = nameof(CompositionShape.Offset);
			enter.Comment = "Enter";
			this.enter = enter;
			var exit = cc.CreateVector2KeyFrameAnimation();
			exit.InsertExpressionKeyFrame(1f, "Index");
			exit.Duration = TimeSpan.FromMilliseconds(DurationExit);
			exit.Target = nameof(CompositionShape.Offset);
			exit.Comment = "Exit";
			this.exit = exit;
			// this appears to work but does not interpolate the matrix over time
			ExpressionAnimation xform = cc.CreateExpressionAnimation();
			xform.Properties.InsertVector3("Component1", new Vector3(1, 0, 0));
			xform.Properties.InsertVector3("Component2", new Vector3(0, 1, 0));
			xform.Expression = "Matrix3x2(props.Component1.X,props.Component2.X,props.Component1.Y,props.Component2.Y,props.Component1.Z,props.Component2.Z)";
			xform.SetExpressionReferenceParameter("props", xform.Properties);
			xform.Target = nameof(CompositionShape.TransformMatrix);
			xform.Comment = "TransformMatrix";
			this.xform = xform;
			var iac = cc.CreateImplicitAnimationCollection();
			iac[nameof(CompositionShape.Offset)] = shift;
			iac[nameof(CompositionShape.TransformMatrix)] = this.xform;
			iac.Comment = "Implicit";
			this.iac = iac;
		}
		public void Unprepare(Compositor cc) {
			xform?.Dispose(); xform = null;
			shift?.Dispose(); shift = null;
			exit?.Dispose(); exit = null;
			enter?.Dispose(); enter = null;
			iac?.Dispose(); iac = null;
		}
		public ImplicitAnimationCollection CreateImplcit(IElementFactoryContext iefc) {
			return iac;
		}
		void Enter_CategoryValue(CompositionShapeCollection ssc, CompositionShape cs, IElementCategoryValueContext ieec, IElementDataOperation iedo, Action<CompositionObject> cb) {
			// calculate the spawn point
			double c1 = iedo.Transition == ItemTransition.Head
				? ieec.CategoryAxis.Minimum - 2 + ieec.Item.CategoryOffset
				: ieec.CategoryAxis.Maximum + 2 + ieec.Item.CategoryOffset;
			var vxx = MappingSupport.ToVector(
				c1, ieec.CategoryAxis.Orientation,
				Math.Min(ieec.Item.DataValue, 0), ieec.ValueAxis.Orientation);
			_trace.Verbose($"Enter {cs.Comment} {iedo.Transition} spawn:({vxx.X},{vxx.Y})");
			// enter VT at this offset
			cs.Offset = vxx;
			if (iedo.Transition == ItemTransition.Head) {
				ssc.Insert(0, cs);
			}
			else {
				ssc.Add(cs);
			}
			cb?.Invoke(cs);
			if (enter.Target != nameof(CompositionShape.Offset)) {
				// if it's Offset we expect a call for that next, otherwise start this one
				cs.StartAnimation(enter.Target, enter);
			}
		}
		void Exit_CategoryValue(CompositionShapeCollection ssc, CompositionShape cs, IElementFactoryContext iefc, IElementCategoryValueContext ieec, IElementDataOperation iedo, Action<CompositionObject> cb) {
			CompositionScopedBatch ccb = iefc.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
			ccb.Comment = $"ScopedBatch_{cs.Comment}";
			ccb.Completed += (sender, cbcea) => {
				ssc.Remove(cs);
				cb?.Invoke(cs);
			};
			// calculate the exit point
			double c1 = iedo.Transition == ItemTransition.Head
				? ieec.CategoryAxis.Minimum - 2 + ieec.Item.CategoryOffset
				: ieec.CategoryAxis.Maximum + 2 + ieec.Item.CategoryOffset;
			var vxx = MappingSupport.ToVector(
				c1, ieec.CategoryAxis.Orientation,
				Math.Min(ieec.Item.DataValue, 0), ieec.ValueAxis.Orientation);
			_trace.Verbose($"Exit {cs.Comment} {iedo.Transition} spawn:({vxx.X},{vxx.Y})");
			exit.SetVector2Parameter("Index", vxx);
			cs.StartAnimation(exit.Target, exit);
			ccb.End();
		}
		public bool StartAnimation(string key, IElementFactoryContext iefc, CompositionShapeCollection ssc, CompositionObject co, Action<CompositionObject> cb = null) {
			switch(key) {
				case AnimationKeys.ENTER:
					if (co is CompositionShape cs && iefc is IElementCategoryValueContext ieec && iefc is IElementDataOperation iedo) {
						Enter_CategoryValue(ssc, cs, ieec, iedo, cb);
						return true;
					}
					break;
				case AnimationKeys.EXIT:
					if (co is CompositionShape cs2 && iefc is IElementCategoryValueContext ieec2 && iefc is IElementDataOperation iedo2) {
						Exit_CategoryValue(ssc, cs2, iefc, ieec2, iedo2, cb);
						return true;
					}
					break;
			}
			return false;
		}
		void Offset_CategoryValue(CompositionShape cs, IElementCategoryValueContext ieec) {
			var vxx = MappingSupport.ToVector(
				ieec.Item.CategoryValue + ieec.Item.CategoryOffset, ieec.CategoryAxis.Orientation,
				Math.Min(ieec.Item.DataValue, 0), ieec.ValueAxis.Orientation);
			_trace.Verbose($"Offset {cs.Comment} [{ieec.Item.CategoryValue}] move:({vxx.X},{vxx.Y})");
			if (vxx != cs.Offset) {
				shift.SetVector2Parameter("Index", vxx);
				cs.StartAnimation(shift.Target, shift);
			}
		}
		void Offset_Value(CompositionShape cs, IElementExtentContext ieexc, IElementValueContext ievc) {
			var vxx = MappingSupport.ToVector(
				0, ieexc.Component1Axis.Orientation,
				ievc.Value, ieexc.Component2Axis.Orientation);
			_trace.Verbose($"Offset {cs.Comment} [0] move:({vxx.X},{vxx.Y})");
			if (vxx != cs.Offset) {
				shift.SetVector2Parameter("Index", vxx);
				cs.StartAnimation(shift.Target, shift);
			}
		}
		public bool StartAnimation(string key, IElementFactoryContext iefc, CompositionObject co, Action<CompositionAnimation> cfg = null) {
			switch (key) {
				case AnimationKeys.TRANSFORM:
					_trace.Verbose($"Transform {co.Comment}");
					cfg?.Invoke(xform);
					co.StartAnimation(xform.Target, xform);
					return true;
				case AnimationKeys.OFFSET:
					if (co is CompositionShape cs) {
						switch(iefc) {
							case IElementCategoryValueContext ieec:
								Offset_CategoryValue(cs, ieec);
								return true;
							case IElementValueContext ievc:
								if(iefc is IElementExtentContext ieexc) {
									Offset_Value(cs, ieexc, ievc);
									return true;
								}
								break;
						}
					}
					break;
			}
			return false;
		}
	}
	#endregion
	#region RoundedRectangleGeometryFactory
	/// <summary>
	/// Default factory for creating rounded rectangle sprites.
	/// </summary>
	public class RoundedRectangleGeometryFactory : IElementFactory {
		#region properties
		public Style_Brush FillBrush { get; set; }
		public Style_Brush StrokeBrush { get; set; }
		public Style_Stroke Stroke { get; set; }
		/// <summary>
		/// Corner radius is in NDC units (output side of transform).
		/// </summary>
		public double CornerRadiusX { get; set; } = double.NaN;
		/// <summary>
		/// Corner radius is in NDC units (output side of transform).
		/// </summary>
		public double CornerRadiusY { get; set; } = double.NaN;
		/// <summary>
		/// Whether to flip linear gradients for negative value bars.
		/// Use this when the gradient orients along the bar's axis, and it will "mirror" the gradients around the value axis zero.
		/// </summary>
		public bool FlipGradients { get; set; }
		#endregion
		public RoundedRectangleGeometryFactory() { }
		protected void FlipGradient(Brush_LinearGradient blg, CompositionLinearGradientBrush clgb) {
			var flip = clgb.StartPoint;
			clgb.StartPoint = clgb.EndPoint;
			clgb.EndPoint = flip;
		}
		/// <summary>
		/// <inheritdoc/>
		/// Flips the gradient direction for negative bars.
		/// </summary>
		/// <param name="c"></param>
		/// <param name="width"></param>
		/// <param name="height"></param>
		/// <param name="c1axis"></param>
		/// <param name="c2axis"></param>
		/// <returns></returns>
		public CompositionShape CreateElement(IElementFactoryContext iefc) {
			var rectangle = iefc.Compositor.CreateRoundedRectangleGeometry();
			var sprite = iefc.Compositor.CreateSpriteShape(rectangle);
			Stroke?.Apply(sprite);
			var ierc = iefc as IElementRectangleContext;
			if (FillBrush != null) {
				sprite.FillBrush = FillBrush.CreateBrush(iefc.Compositor);
				if (ierc.DataValue < 0 && FillBrush is Brush_LinearGradient blg && FlipGradients && sprite.FillBrush is CompositionLinearGradientBrush clgb) {
					FlipGradient(blg, clgb);
				}
			}
			if (StrokeBrush != null) {
				sprite.StrokeBrush = StrokeBrush.CreateBrush(iefc.Compositor);
				if (ierc.DataValue < 0 && StrokeBrush is Brush_LinearGradient blg && FlipGradients && sprite.StrokeBrush is CompositionLinearGradientBrush clgb) {
					FlipGradient(blg, clgb);
				}
			}
			if (!double.IsNaN(CornerRadiusX) && !double.IsNaN(CornerRadiusY)) {
				rectangle.CornerRadius = new Vector2((float)CornerRadiusX, (float)CornerRadiusY);
			}
			// Offset and Size are Model units (input side of transform)
			rectangle.Size = new Vector2((float)ierc.Width, (float)ierc.Height);
			// Offset and Transform are managed by the caller
			return sprite;
		}
	}
	#endregion
	#region LineGeometryFactory
	/// <summary>
	/// Use for <see cref="ValueRule"/> etc.
	/// Creates normalized geometry; owner MUST manage the <see cref="CompositionShape.Offset"/> to position correctly.
	/// </summary>
	public class LineGeometryFactory : IElementFactory {
		public Style_Brush StrokeBrush { get; set; }
		public Style_Stroke Stroke { get; set; }
		public CompositionShape CreateElement(IElementFactoryContext iefc) {
			var line = iefc.Compositor.CreateLineGeometry();
			if(iefc is IElementLineContext ielc) {
				line.Start = ielc.Start;
				line.End = ielc.End;
			}
			else {
				line.Start = new Vector2(0, 0);
				line.End = new Vector2(1, 0);
			}
			var sprite = iefc.Compositor.CreateSpriteShape(line);
			Stroke?.Apply(sprite);
			if (StrokeBrush != null) {
				sprite.StrokeBrush = StrokeBrush.CreateBrush(iefc.Compositor);
			}
			return sprite;
		}
	}
	#endregion
	#region PathGeometryFactory
	/// <summary>
	/// Default factor for <see cref="CompositionPath"/> sprites.
	/// </summary>
	public class PathGeometryFactory : IElementFactory {
		public Style_Brush StrokeBrush { get; set; }
		public Style_Stroke Stroke { get; set; }
		public CompositionShape CreateElement(IElementFactoryContext iefc) {
			var pathgeom = iefc.Compositor.CreatePathGeometry((iefc as IElementCompositionPath).Path);
			var sprite = iefc.Compositor.CreateSpriteShape(pathgeom);
			Stroke?.Apply(sprite);
			if (StrokeBrush != null) {
				sprite.StrokeBrush = StrokeBrush.CreateBrush(iefc.Compositor);
			}
			return sprite;
		}
	}
	#endregion
}
