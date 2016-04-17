﻿using System;
using System.Collections.Generic;
using System.Text;
using KSP.UI;
using KSP.UI.Dialogs;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DebugStuff
{
    [KSPAddon(KSPAddon.Startup.EveryScene, true)]
    internal class DebugStuff : MonoBehaviour
    {
        private int flip;
        private RaycastHit hit;

        private GameObject currentHoverPart;
        private GameObject previousHoverPart;
        private StringBuilder sb = new StringBuilder();
        private bool showUI;
        private Mode mode;
        private DrawMode drawMode;

        private GUIStyle styleTransform;
        //private GUIStyle styleWindow;
        //private Rect winPos = new Rect(300, 100, 400, 600);

        private static RectTransform window;
        private static Vector2 originalLocalPointerPosition;
        private static Vector3 originalPanelLocalPosition;
        private static Text activeMode;
        private static Text activeDrawMode;
        private static Text partTree;
        private static Text info;
        private static Text limitText;
        private static Font monoSpaceFont;
        

        private int limitDepth = 2;


        private enum Mode
        {
            PART,
            UI,
            OBJECT
        }

        private enum DrawMode
        {
            COLLIDER,
            MESH
        }

        public void Awake()
        {
            DontDestroyOnLoad(this);
        }

        public void Update()
        {
            //if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight)
            //{
            //    if (window != null)
            //        window.gameObject.SetActive(false);
            //    return;
            //}

            if (GameSettings.MODIFIER_KEY.GetKey() && Input.GetKeyDown(KeyCode.P))
            {
                showUI = !showUI;
            }
            flip = 0;

            if (window == null)
            {
                if (UIMasterController.Instance != null)
                {
                    InitFont();
                    //print("Creating the UI");

                    GameObject canvasObj = this.gameObject;

                    // Create a Canvas for the app to avoid hitting the vertex limit 
                    // on the stock canvas.
                    // Clone the stock one instead ?

                    canvasObj.layer = LayerMask.NameToLayer("UI");
                    RectTransform canvasRect = canvasObj.AddComponent<RectTransform>();
                    Canvas countersCanvas = canvasObj.AddComponent<Canvas>();
                    countersCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    countersCanvas.pixelPerfect = true;
                    countersCanvas.worldCamera = UIMasterController.Instance.appCanvas.worldCamera;
                    countersCanvas.planeDistance = 625;

                    CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                    scaler.scaleFactor = 1;
                    scaler.referencePixelsPerUnit = 100;

                    GraphicRaycaster rayCaster = canvasObj.AddComponent<GraphicRaycaster>();

                    window = UICreateWindow(canvasObj);
                    //print("Created the UI");
                }
                return;
            }

            window.gameObject.SetActive(showUI);

            if (showUI && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
            {
                currentHoverPart = CheckForPartUnderCursor();

                if (currentHoverPart && (currentHoverPart != previousHoverPart))
                {
                    previousHoverPart = currentHoverPart;

                    DumpPartHierarchy(currentHoverPart);

                    // A canvas can not have more than 65000 vertex
                    // and a char is 4 vertex
                    int limit = 16000;
                    // not exactly awesome but it works

                    string tree = sb.ToString();

                    if (tree.Length > limit)
                    {
                        partTree.text = sb.ToString().Substring(0, limit) + "\n[Truncated]";
                    }
                    else
                    {
                        partTree.text = tree;
                    }
                }
            }

            if (showUI && currentHoverPart )
            {
                info.text = "something";
            }
        }

        private string GetWheelBaseStatus(Part part)
        {
            var wb = part.GetComponentInChildren<ModuleWheelBase>();
            string status = "";
            if (wb == null)
            {
                return "No ModuleWheelBase";
            }

            if (wb.Wheel == null)
            {
                return "No Wheel?";
            }

            var ws = wb.Wheel.wheelState;
            if (ws == null)
                return "No WheelState";
            
            if (ws.hit.collider == null)
                return "No Collider";

            var col = ws.hit.collider;

            status = col.GetType() + " " + col.name + " ";

            Rigidbody rigidbody = col.attachedRigidbody;
            if (rigidbody == null)
            {
                return status + "No Rigidbody";
            }

            Part collidedPart = rigidbody.GetComponent<Part>();
            if (collidedPart == null)
            {
                return status + "No Part";
            }
            
            if (collidedPart.vessel == part.vessel)
            {
                return status + "Same vessel";
            }
            return status + "Diff vessel";
        }

        public void InitFont()
        {
            if (monoSpaceFont == null)
            {
                monoSpaceFont = Font.CreateDynamicFontFromOSFont("Consolas", 10);
                if (monoSpaceFont == null)
                {
                    monoSpaceFont = Font.CreateDynamicFontFromOSFont("Terminus Font", 10);
                }
                if (monoSpaceFont == null)
                {
                    monoSpaceFont = Font.CreateDynamicFontFromOSFont("Menlo", 10);
                }
                if (monoSpaceFont == null)
                {
                    print("Could not find a MonoSpaced font among those :");
                    foreach (string fontName in Font.GetOSInstalledFontNames()) print(fontName);
                    monoSpaceFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
            }
        }

        public void OnGUI()
        {
            if (styleTransform == null)
            {
                styleTransform = new GUIStyle(GUI.skin.label);
                styleTransform.fontSize = 16;

                if (monoSpaceFont != null)
                {
                    styleTransform.font = monoSpaceFont;
                }
            }

            DrawTools.NewFrame();

            if (showUI && currentHoverPart)
                DrawColliders(currentHoverPart.gameObject);
        }

        private void DumpPartHierarchy(GameObject p)
        {
            sb.Length = 0;
            DumpGameObjectChilds(p, "", sb);
        }

        // A bit messy. The code could be simplified by beeing smarter with when I add
        // characters to pre but it works like that and it does not need to be efficient
        private void DumpGameObjectChilds(GameObject go, string pre, StringBuilder sb)
        {
            bool first = pre == "";
            List<GameObject> neededChilds = new List<GameObject>();
            int count = go.transform.childCount;
            for (int i = 0; i < count; i++)
            {
                GameObject child = go.transform.GetChild(i).gameObject;
                if (!child.GetComponent<Part>() && child.name != "main camera pivot")
                    neededChilds.Add(child);
            }

            count = neededChilds.Count;

            sb.Append(pre);
            if (!first)
            {
                sb.Append(count > 0 ? "--+" : "---");
            }
            else
            {
                sb.Append("+");
            }
            sb.AppendFormat("{0} T:{1} L:{2} ({3})\n", go.name, go.tag, go.layer, LayerMask.LayerToName(go.layer));

            string front = first ? "" : "  ";
            string preComp = pre + front + (count > 0 ? "| " : "  ");

            Component[] comp = go.GetComponents<Component>();

            for (int i = 0; i < comp.Length; i++)
            {
                if (comp[i] is Transform)
                {
                    sb.AppendFormat("{0}  {1} - {2}\n", preComp, comp[i].GetType().Name, go.transform.name);
                }
                else
                {
                    sb.AppendFormat("{0}  {1} - {2}\n", preComp, comp[i].GetType().Name, comp[i].name);
                }
            }

            sb.AppendLine(preComp);

            for (int i = 0; i < count; i++)
            {
                DumpGameObjectChilds(neededChilds[i], i == count - 1 ? pre + front + " " : pre + front + "|", sb);
            }
        }

        private GameObject CheckForPartUnderCursor()
        {
            //if (EventSystem.current.IsPointerOverGameObject())
            //{
            //    return null;
            //}


            //1000000000000000000101

            if (mode == Mode.UI)
            {
                var pointer = new PointerEventData(EventSystem.current);

                pointer.position = Input.mousePosition;
                //pointer.position = Camera.main.ScreenToViewportPoint(Input.mousePosition);

                var raycastResults = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, raycastResults);

                //print(pointer.position.ToString("F3"));

                //if (!Physics2D.Raycast(ray, out hit, 500f, layerMask))
                if (raycastResults.Count == 0)
                {
                    //print("Nothing");
                    return null;
                }

                //print("Found " + raycastResults.Count); 

                GameObject obj = raycastResults[0].gameObject;
                //print(obj.name);

                while (obj.transform.parent && !obj.transform.parent.gameObject.GetComponent<Canvas>())
                {
                    obj = obj.transform.parent.gameObject;
                    //print(obj.name);
                }
                return obj;
            }

            if (mode == Mode.PART)
            {
                return Mouse.HoveredPart ? Mouse.HoveredPart.gameObject : null;
            }


            if (mode == Mode.OBJECT)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                int layerMask = ~LayerMask.NameToLayer("UI");

                if (!Physics.Raycast(ray, out hit, 500f, layerMask))
                {
                    //print("Nothing");
                    return null;
                }

                GameObject obj = hit.collider.gameObject;
                //print(obj.name);
                int d = 0;
                while (obj.transform.parent && d < limitDepth)
                {
                    obj = obj.transform.parent.gameObject;
                    d++;
                    //print(obj.name);
                }
                return obj;
            }

            return null;
        }

        private void DrawColliders(GameObject go)
        {
            DrawTools.DrawTransform(go.transform, 0.3f);

            Camera cam;

            if (HighLogic.LoadedSceneIsEditor)
                cam = EditorLogic.fetch.editorCamera;
            else if (HighLogic.LoadedSceneIsFlight)
                cam = FlightCamera.fetch.mainCamera;
            else
                cam = Camera.current;

            Vector3 point = cam.WorldToScreenPoint(go.transform.position);

            Vector2 size = styleTransform.CalcSize(new GUIContent(go.transform.name));

            // Clearly there is a simpler way but I am half alseep
            switch (flip % 4)
            {
                case 0:
                    point.x = point.x - size.x - 5;
                    point.y = point.y - 2;
                    break;
                case 1:
                    point.x = point.x + 5;
                    point.y = point.y - 2;
                    break;
                case 2:
                    point.x = point.x - size.x - 5;
                    point.y = point.y + size.y + 2;
                    break;
                case 3:
                    point.x = point.x + 5;
                    point.y = point.y + size.y + 2;
                    break;
            }

            if (mode == Mode.PART)
                GUI.Label(new Rect(point.x, Screen.currentResolution.height - point.y, size.x, size.y), go.transform.name, styleTransform);

            flip++;

            if (drawMode == DrawMode.COLLIDER)
            {
                Collider[] comp = go.GetComponents<Collider>();
                for (int i = 0; i < comp.Length; i++)
                {
                    Collider baseCol = comp[i];

                    if (baseCol is BoxCollider)
                    {
                        BoxCollider box = baseCol as BoxCollider;
                        DrawTools.DrawLocalCube(box.transform, box.size, Color.yellow, box.center);
                    }

                    if (baseCol is SphereCollider)
                    {
                        SphereCollider sphere = baseCol as SphereCollider;
                        DrawTools.DrawSphere(sphere.transform.TransformPoint(sphere.center), Color.red, sphere.radius);
                    }

                    if (baseCol is CapsuleCollider)
                    {
                        CapsuleCollider caps = baseCol as CapsuleCollider;
                        Vector3 dir = new Vector3(caps.direction == 0 ? 1 : 0, caps.direction == 1 ? 1 : 0, caps.direction == 2 ? 1 : 0);
                        Vector3 top = caps.transform.TransformPoint(caps.center + caps.height * 0.5f * dir);
                        Vector3 bottom = caps.transform.TransformPoint(caps.center - caps.height * 0.5f * dir);
                        DrawTools.DrawCapsule(top, bottom, Color.green, caps.radius);
                    }

                    if (baseCol is MeshCollider)
                    {
                        MeshCollider mesh = baseCol as MeshCollider;
                        DrawTools.DrawLocalMesh(mesh.transform, mesh.sharedMesh, XKCDColors.ElectricBlue);
                    }
                }
            }
            else
            {
                MeshFilter[] mesh = go.GetComponents<MeshFilter>();

                for (int i = 0; i < mesh.Length; i++)
                {
                    DrawTools.DrawLocalMesh(mesh[i].transform, mesh[i].sharedMesh, XKCDColors.Orange);
                }
            }

            int count = go.transform.childCount;
            for (int i = 0; i < count; i++)
            {
                GameObject child = go.transform.GetChild(i).gameObject;

                if (!child.GetComponent<Part>() && child.name != "main camera pivot")
                    DrawColliders(child);
            }
        }


        private RectTransform UICreateWindow(GameObject parent)
        {
            var panelPos = addEmptyPanel(parent);
            panelPos.localPosition = new Vector3(0, 0, 0);
            panelPos.sizeDelta = new Vector2(100, 100);
            panelPos.anchorMin = new Vector2(0, 1);
            panelPos.anchorMax = new Vector2(0, 1);
            panelPos.pivot = new Vector2(0, 1);
            panelPos.localScale = new Vector3(1, 1, 1);

            var image = panelPos.gameObject.AddComponent<Image>();
            image.color = new Color(0.25f, 0.25f, 0.25f, 1);

            var drag = panelPos.gameObject.AddComponent<DragHandler>();

            drag.AddEvents(OnInitializePotentialDrag, OnBeginDrag, OnDrag, OnEndDrag);

            var layout = panelPos.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;
            layout.padding = new RectOffset(5, 5, 5, 5);

            var csf = panelPos.gameObject.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            addText(panelPos.gameObject, "Move the cursor over a part while holding shift to select a part");

            var buttonPanel = addEmptyPanel(panelPos.gameObject);

            var bpl = buttonPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
            bpl.childAlignment = TextAnchor.UpperCenter;
            bpl.childForceExpandHeight = true;
            bpl.childForceExpandWidth = true;
            bpl.padding = new RectOffset(5, 5, 5, 5);

            var bpsf = buttonPanel.gameObject.AddComponent<ContentSizeFitter>();
            bpsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            bpsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            addButton(buttonPanel.gameObject, "Dump to log", () => { print(sb.ToString()); }, out activeMode);

            addButton(buttonPanel.gameObject, "List", () =>
            {
                var stuff = Resources.LoadAll("");
                foreach (var o in stuff)
                {
                    GameObject obj = o as GameObject;
                    if (obj)
                    {
                        print("GameObject - " + obj.name);
                    }
                    else
                    {
                        print("Type - " + o.GetType());
                    }

                }
            }, out activeMode);

            addButton(buttonPanel.gameObject, mode.ToString(), () =>
            {
                currentHoverPart = previousHoverPart = null;
                partTree.text = "";
                switch (mode)
                {
                    case Mode.PART:
                        mode = Mode.UI;
                        break;
                    case Mode.UI:
                        mode = Mode.OBJECT;
                        break;
                    case Mode.OBJECT:
                        mode = Mode.PART;
                        break;
                }
                activeMode.text = mode.ToString();
            }, out activeMode);

            addButton(buttonPanel.gameObject, "-", () =>
            {
                limitDepth--;
                limitText.text = limitDepth.ToString();
            }, out activeDrawMode);

            limitText = addText(buttonPanel.gameObject, limitDepth.ToString());

            addButton(buttonPanel.gameObject, "+", () =>
            {
                limitDepth++;
                limitText.text = limitDepth.ToString();
            }, out activeDrawMode);

            addButton(buttonPanel.gameObject, drawMode.ToString(), () =>
            {
                switch (drawMode)
                {
                    case DrawMode.COLLIDER:
                        drawMode = DrawMode.MESH;
                        break;
                    case DrawMode.MESH:
                        drawMode = DrawMode.COLLIDER;
                        break;
                }
                activeDrawMode.text = drawMode.ToString();
            }, out activeDrawMode);

            //addButton(buttonPanel.gameObject, "Stuff", () =>
            //{
            //    if (currentHoverPart)
            //    {
            //        var col = currentHoverPart.GetChild("casingCollider");
            //        if (col != null)
            //        {
            //            var c = col.GetComponent<CapsuleCollider>();
            //            c.enabled = !c.enabled;
            //            print("casingCollider is now " + c.enabled);
            //        }
            //    }
            //
            //
            //}, out xxx);


            info = addText(panelPos.gameObject, "");
            info.font = monoSpaceFont;
            info.fontSize = 12;

            partTree = addText(panelPos.gameObject, "");
            partTree.font = monoSpaceFont;
            partTree.fontSize = 11;

            return panelPos;
        }

        private Button addButton(GameObject parent, string text, UnityAction click, out Text textObj)
        {
            GameObject buttonObject = new GameObject("Button");


            buttonObject.layer = LayerMask.NameToLayer("UI");
            ;

            RectTransform trans = buttonObject.AddComponent<RectTransform>();
            trans.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            trans.localPosition.Set(0, 0, 0);

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.5f, 0f, 0, 0.5f);

            Button button = buttonObject.AddComponent<Button>();
            button.interactable = true;
            button.onClick.AddListener(click);

            textObj = addText(buttonObject, text);

            var csf = buttonObject.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var layout = buttonObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;
            layout.padding = new RectOffset(5, 5, 5, 5);

            buttonObject.transform.SetParent(parent.transform, false);

            return button;
        }


        private static Text addText(GameObject parent, string s)
        {
            GameObject text1Obj = new GameObject("Text");

            text1Obj.layer = LayerMask.NameToLayer("UI");

            RectTransform trans = text1Obj.AddComponent<RectTransform>();
            trans.localScale = new Vector3(1, 1, 1);
            trans.localPosition.Set(0, 0, 0);

            Text text = text1Obj.AddComponent<Text>();
            text.supportRichText = true;
            text.text = s;
            text.fontSize = 14;
            text.font = UISkinManager.defaultSkin.font;

            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = Color.white;
            text1Obj.transform.SetParent(parent.transform, false);

            return text;
        }

        private void OnInitializePotentialDrag(PointerEventData e)
        {
            //print("OnInitializePotentialDrag");
            originalPanelLocalPosition = window.localPosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)window.parent.transform, e.position, e.pressEventCamera, out originalLocalPointerPosition);
        }

        private void OnBeginDrag(PointerEventData e)
        {
            //print("onBeginDrag");
        }

        private void OnDrag(PointerEventData e)
        {
            Vector2 localPointerPosition;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)window.parent.transform, e.position, e.pressEventCamera, out localPointerPosition))
            {
                Vector3 offsetToOriginal = localPointerPosition - originalLocalPointerPosition;
                window.localPosition = originalPanelLocalPosition + offsetToOriginal;
            }
        }

        private void OnEndDrag(PointerEventData e)
        {
            //print("onEndDrag");
        }

        private static RectTransform addEmptyPanel(GameObject parent)
        {
            GameObject panelObj = new GameObject(parent.name + "Panel");
            panelObj.layer = LayerMask.NameToLayer("UI");

            RectTransform panelRect = panelObj.AddComponent<RectTransform>();

            // Top Left corner as base
            panelRect.anchorMin = new Vector2(0, 1);
            panelRect.anchorMax = new Vector2(0, 1);
            panelRect.pivot = new Vector2(0, 1);
            panelRect.localPosition = new Vector3(0, 0, 0);
            panelRect.localScale = new Vector3(1, 1, 1);

            panelObj.transform.SetParent(parent.transform, true);

            return panelRect;
        }

        public new static void print(object message)
        {
            MonoBehaviour.print("[DebugStuff] " + message);
        }
    }
}