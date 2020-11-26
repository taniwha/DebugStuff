using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using KSP.UI;
using KSP.UI.Dialogs;
using KSP.UI.Screens.DebugToolbar;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;
using UnityEngine.UI;
using TMPro;

using KodeUI;

namespace DebugStuff
{
    internal class SpriteViewer : Window
    {
		UIText width;
		UIText height;
		UIImage image;

		public override void CreateUI()
		{
			base.CreateUI();
			this
				.Title("Sprite")
				.Vertical()
				.ControlChildSize(true, true)
				.ChildForceExpand(false,false)
				.PreferredSizeFitter(true, true)
				.PreferredWidth(140)
				.Anchor(AnchorPresets.MiddleCenter)
				.Pivot(PivotPresets.TopLeft)
				.SetSkin("DebugStuff")

				.Add<Layout>()
					.Horizontal()
					.ControlChildSize (true, true)
					.ChildForceExpand (false, false)
					.FlexibleLayout(true, false)
					.Anchor (AnchorPresets.HorStretchTop)
					.Add<UIText>()
						.Text("Width:")
						.Alignment(TextAlignmentOptions.Left)
						.FlexibleLayout (false, true)
						.Finish ()
					.Add<UIEmpty>()
						.FlexibleLayout(true, true)
						.Finish()
					.Add<UIText>(out width)
						.Alignment(TextAlignmentOptions.Right)
						.FlexibleLayout(false, true)
						.Finish()
					.Finish()
				.Add<Layout>()
					.Horizontal()
					.ControlChildSize (true, true)
					.ChildForceExpand (false, false)
					.FlexibleLayout(true, false)
					.Anchor (AnchorPresets.HorStretchTop)
					.Add<UIText>()
						.Text("Height:")
						.Alignment(TextAlignmentOptions.Left)
						.FlexibleLayout (false, true)
						.Finish ()
					.Add<UIEmpty>()
						.FlexibleLayout(true, true)
						.Finish()
					.Add<UIText>(out height)
						.Alignment(TextAlignmentOptions.Right)
						.FlexibleLayout(false, true)
						.Finish()
					.Finish()
				.Add<Layout>()
					.Horizontal()
					.ControlChildSize (true, true)
					.ChildForceExpand (false, false)
					.FlexibleLayout(true, false)
					.Anchor(AnchorPresets.HorStretchTop)
					.Add<UIImage>(out image)
						.PreferredSize(128, 128)
						.Finish()
					.Finish()
				.Finish();
			titlebar
				.Add<UIButton> ()
					.OnClick (CloseWindow)
					.Anchor (AnchorPresets.TopRight)
					.Pivot (new Vector2 (1.25f, 1.25f))
					.SizeDelta (16, 16)
					.Finish();
				;
			Debug.Log($"[SpriteViewer] CreateUI");
		}

		void CloseWindow()
		{
			SetActive(false);
		}

		public override void Style()
		{
			base.Style();
		}

		public void SetSprite(Sprite sprite)
		{
			image.Image(sprite);
			width.Text($"{sprite.rect.width}");
			height.Text($"{sprite.rect.height}");
			Debug.Log($"[SpriteViewer] SetSprite {sprite} {sprite.rect.width} {sprite.rect.height}");
		}

		static SpriteViewer viewer;
		public static void ShowSprite(Sprite sprite)
		{
			if (!viewer) {
				var rect = DebugStuff.instance.gameObject.transform as RectTransform;
				viewer = UIKit.CreateUI<SpriteViewer>(rect, "Window");
			}
			viewer.SetActive(true);
			viewer.SetSprite(sprite);
		}
    }
}
