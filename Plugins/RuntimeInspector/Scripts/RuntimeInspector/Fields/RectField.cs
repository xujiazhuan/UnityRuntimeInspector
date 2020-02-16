﻿using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace RuntimeInspectorNamespace
{
	public class RectField : InspectorField
	{
#pragma warning disable 0649
		[SerializeField]
		private BoundInputField inputX;

		[SerializeField]
		private BoundInputField inputY;

		[SerializeField]
		private BoundInputField inputW;

		[SerializeField]
		private BoundInputField inputH;

		[SerializeField]
		private Text labelX;

		[SerializeField]
		private Text labelY;

		[SerializeField]
		private Text labelW;

		[SerializeField]
		private Text labelH;
#pragma warning restore 0649

#if UNITY_2017_2_OR_NEWER
		private bool isRectInt;
#endif

		protected override float HeightMultiplier { get { return 2f; } }

		public override void Initialize()
		{
			base.Initialize();

			inputX.Initialize();
			inputY.Initialize();
			inputW.Initialize();
			inputH.Initialize();

			inputX.OnValueChanged += OnValueChanged;
			inputY.OnValueChanged += OnValueChanged;
			inputW.OnValueChanged += OnValueChanged;
			inputH.OnValueChanged += OnValueChanged;

			inputX.OnValueSubmitted += OnValueSubmitted;
			inputY.OnValueSubmitted += OnValueSubmitted;
			inputW.OnValueSubmitted += OnValueSubmitted;
			inputH.OnValueSubmitted += OnValueSubmitted;

			inputX.DefaultEmptyValue = "0";
			inputY.DefaultEmptyValue = "0";
			inputW.DefaultEmptyValue = "0";
			inputH.DefaultEmptyValue = "0";
		}

		public override bool SupportsType( Type type )
		{
#if UNITY_2017_2_OR_NEWER
			if( type == typeof( RectInt ) )
				return true;
#endif
			return type == typeof( Rect );
		}

		protected override void OnBound( MemberInfo variable )
		{
			base.OnBound( variable );

#if UNITY_2017_2_OR_NEWER
			isRectInt = BoundVariableType == typeof( RectInt );
			if( isRectInt )
			{
				RectInt val = (RectInt) Value;
				inputX.Text = val.x.ToString();
				inputY.Text = val.y.ToString();
				inputW.Text = val.width.ToString();
				inputH.Text = val.height.ToString();
			}
			else
#endif
			{
				Rect val = (Rect) Value;
				inputX.Text = val.x.ToString();
				inputY.Text = val.y.ToString();
				inputW.Text = val.width.ToString();
				inputH.Text = val.height.ToString();
			}
		}

		private bool OnValueChanged( BoundInputField source, string input )
		{
#if UNITY_2017_2_OR_NEWER
			if( isRectInt )
			{
				int value;
				if( int.TryParse( input, out value ) )
				{
					RectInt val = (RectInt) Value;
					if( source == inputX )
						val.x = value;
					else if( source == inputY )
						val.y = value;
					else if( source == inputW )
						val.width = value;
					else
						val.height = value;

					Value = val;
					return true;
				}
			}
			else
#endif
			{
				float value;
				if( float.TryParse( input, out value ) )
				{
					Rect val = (Rect) Value;
					if( source == inputX )
						val.x = value;
					else if( source == inputY )
						val.y = value;
					else if( source == inputW )
						val.width = value;
					else
						val.height = value;

					Value = val;
					return true;
				}
			}

			return false;
		}

		private bool OnValueSubmitted( BoundInputField source, string input )
		{
			Inspector.RefreshDelayed();
			return OnValueChanged( source, input );
		}

		protected override void OnSkinChanged()
		{
			base.OnSkinChanged();

			labelX.SetSkinText( Skin );
			labelY.SetSkinText( Skin );
			labelW.SetSkinText( Skin );
			labelH.SetSkinText( Skin );

			inputX.Skin = Skin;
			inputY.Skin = Skin;
			inputW.Skin = Skin;
			inputH.Skin = Skin;
		}

		public override void Refresh()
		{
#if UNITY_2017_2_OR_NEWER
			if( isRectInt )
			{
				RectInt prevVal = (RectInt) Value;
				base.Refresh();
				RectInt val = (RectInt) Value;

				if( val.x != prevVal.x )
					inputX.Text = val.x.ToString();
				if( val.y != prevVal.y )
					inputY.Text = val.y.ToString();
				if( val.width != prevVal.width )
					inputW.Text = val.width.ToString();
				if( val.height != prevVal.height )
					inputH.Text = val.height.ToString();
			}
			else
#endif
			{
				Rect prevVal = (Rect) Value;
				base.Refresh();
				Rect val = (Rect) Value;

				if( val.x != prevVal.x )
					inputX.Text = val.x.ToString();
				if( val.y != prevVal.y )
					inputY.Text = val.y.ToString();
				if( val.width != prevVal.width )
					inputW.Text = val.width.ToString();
				if( val.height != prevVal.height )
					inputH.Text = val.height.ToString();
			}
		}
	}
}