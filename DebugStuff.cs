﻿using System;
using System.Collections.Generic;
using System.Text;
using KSP.UI;
using KSP.UI.Dialogs;
using KSP.UI.Screens.DebugToolbar;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;
using UnityEngine.UI;
using TMPro;

namespace DebugStuff
{
    [KSPAddon(KSPAddon.Startup.EveryScene, true)]
    internal class DebugStuff : MonoBehaviour
    {
        private int flip;
        
        private GameObject hoverObject;
        private GameObject previousDisplayedObject;
        private GameObject currentDisplayedObject;
        private StringBuilder sb = new StringBuilder();
        private bool showUI;
        private Mode mode;

        private bool meshes = false;
        private bool colliders = false;
        private bool transforms = true;
        private bool labels = true;
        private bool bounds = true;
        private bool joints = false;
        private bool activeOnly = false;

        private GUIStyle styleTransform;
        //private GUIStyle styleWindow;
        //private Rect winPos = new Rect(300, 100, 400, 600);

        private static RectTransform window;
        private static Vector2 originalLocalPointerPosition;
        private static Vector3 originalPanelLocalPosition;
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

            if (showUI)
            {
                GameObject mouseObject = CheckForObjectUnderCursor();
                info.text = mouseObject ? mouseObject.name : "Nothing";

                bool modPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                
                if (modPressed)
                {
                    hoverObject = mouseObject;
                    currentDisplayedObject = GetRootObject(hoverObject);
                }
                
                if (currentDisplayedObject && (currentDisplayedObject != previousDisplayedObject))
                {
                    previousDisplayedObject = currentDisplayedObject;

                    DumpPartHierarchy(currentDisplayedObject);

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
        }

        public void OnRenderObject()
        {
            if (showUI && currentDisplayedObject)
                DrawObjects(currentDisplayedObject);
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

            if (showUI && currentDisplayedObject && mode != Mode.UI)
                DrawLabels(currentDisplayedObject);
        }

        private void DumpPartHierarchy(GameObject p)
        {
            sb.Length = 0;
            DumpGameObjectChilds(p, "", sb);
        }

        private void DumpComponents(GameObject go, string preComp)
        {
            Component[] comp = go.GetComponents<Component>();
            
            for (int i = 0; i < comp.Length; i++)
            {
                if (comp[i] == null)
                    continue;

                if (comp[i] is RectTransform)
                {
                    RectTransform rect = (RectTransform)comp[i];
                    sb.AppendFormat("{0}  {1} - {2}\n    {0} - Anchor Min {3:F0} - Max {4:F0} - Pivot {5} - Scale: {9:F2}\n{0}    Position {6:F0} - Rotation {8:F5} - Size {7:F0}\n"
                        , preComp, comp[i].GetType().Name, go.transform.name, rect.anchorMin, rect.anchorMax, rect.pivot, rect.anchoredPosition3D, rect.sizeDelta, rect.localRotation.eulerAngles, rect.localScale);
                }
                else if (comp[i] is Canvas)
                {
                    Debug.Log("4-3");
                    Canvas c = (Canvas)comp[i];
                    sb.AppendFormat("{0}  {1} - {2}\n{0} Mode {3} - PP {4} - Camera {5} - Sort {6} - Layer {7} - LayerName {8}\n"
                        , preComp, comp[i].GetType().Name, go.transform.name, c.renderMode, c.pixelPerfect, c.worldCamera?.name, c.sortingOrder, c.sortingLayerID, c.sortingLayerName);
                    Debug.Log("4-4");
                }
                else if (comp[i] is CanvasScaler)
                {
                    CanvasScaler cs = (CanvasScaler)comp[i];
                    sb.AppendFormat("{0}  {1} - {2}\n    {0} - Mode {3} - Scale {4:N1} - PPU {5:N1}\n", preComp, comp[i].GetType().Name, go.transform.name, cs.uiScaleMode, cs.scaleFactor, cs.referencePixelsPerUnit);
                }
                else if (comp[i] is Transform)
                {
                    sb.AppendFormat("{0}  {1} - {2}\n{0} - Position ({3:N4}, {4:N4}, {5:N4}) - Rotation: {6} - Scale: {7}\n", preComp, comp[i].GetType().Name, go.transform.name, comp[i].transform.localPosition.x, comp[i].transform.localPosition.y, comp[i].transform.localPosition.z, comp[i].transform.localRotation.eulerAngles, comp[i].transform.localScale);
                }
                else if (comp[i] is Text)
                {
                    Text t = (Text) comp[i];
                    sb.AppendFormat("{0}  {1} - {2} - {3} - {4} - {5} - {6}\n", preComp, comp[i].GetType().Name, t.text, t.alignByGeometry, t.pixelsPerUnit, t.font.dynamic, t.fontSize);
                }
                else if (comp[i] is TextMeshProUGUI)
                {
                    TextMeshProUGUI tmp = (TextMeshProUGUI)comp[i];
                    sb.AppendFormat("{0}  {1} - {2}\n    {0}  {3} - {4} - {5} - {6}\n", preComp, comp[i].GetType().Name, tmp.text, tmp.fontSize, tmp.fontStyle, tmp.alignment, tmp.color);
                }
                else if (comp[i] is Light)
                {
                    Light l = (Light)comp[i];
                    sb.AppendFormat("{16}  {17} - {18}\n{16}    Range: {0} - Spot Angle: {1} - CookieSize: {2} - CullingMask: {3}\n{16}    ShadowNearPlane: {4} - ShadowNormalBias: {5} - ShadowCustomResolution: {6} - ShadowBias: {7}\n{16}    Color: {8} - ColorTemp: {9} - Intensity: {10} - Type: {11}\n{16}    Shadows: {12} - ShadowStrength: {13} - ShadowResolution: {14} - BounceIntensity: {15}\n"
                        , l.range, l.spotAngle, l.cookieSize, l.cullingMask, l.shadowNearPlane, l.shadowNormalBias, l.shadowCustomResolution
                        , l.shadowBias, l.color, l.colorTemperature, l.intensity, l.type
                        , l.shadows, l.shadowStrength, l.shadowResolution, l.bounceIntensity
                        , preComp, comp[i].GetType().Name, go.transform.name);
                }
                else if (comp[i] is Image)
                {
                    Image im = (Image)comp[i];
                    sb.AppendFormat("{1}  {2} - {3}\n{1}    Color: {0} - Fill: {4}\n", im.color.ToString(), preComp, comp[i].GetType().Name, go.transform.name, im.type.ToString());
                }
                else if (comp[i] is Button)
                {
                    Button bt = (Button)comp[i];
                    sb.AppendFormat("{0}  {1} - {2}\n", preComp, comp[i].GetType().Name, go.transform.name);
                    for (int j = 0; j < bt.onClick.GetPersistentEventCount(); j++)
                    {

                        sb.AppendFormat("{0}  Button Listener: {1}\n", preComp, bt.onClick.GetPersistentMethodName(j));
                    }
                }
                else if (comp[i] is MeshCollider)
                {
                    MeshCollider mc = (MeshCollider)comp[i];
                    sb.AppendFormat("{1}  {2} - {3}\n{1}    Convex: {0}\n", mc.convex, preComp, comp[i].GetType().Name, go.transform.name);
                }
                else if (comp[i] is BoxCollider)
                {
                    BoxCollider bc = (BoxCollider)comp[i];
                    sb.AppendFormat("{2}  {3} - {4}\n{2}    Center: {0} - Size: {1}\n", bc.center, bc.size, preComp, comp[i].GetType().Name, go.transform.name);
                }
                else if (comp[i] is SphereCollider)
                {
                    SphereCollider sc = (SphereCollider)comp[i];
                    sb.AppendFormat("{2}  {3} - {4}\n{2}    Center: {0} - Radius: {1}\n", sc.center, sc.radius, preComp, comp[i].GetType().Name, go.transform.name);
                }
                else if (comp[i] is CapsuleCollider)
                {
                    CapsuleCollider cc = (CapsuleCollider)comp[i];
                    sb.AppendFormat("{4}  {5} - {6}\n{4}    Center: {0} - Radius: {1} - Height: {2} - Direction: {3}\n", cc.center, cc.radius, cc.height, cc.direction
                        , preComp, comp[i].GetType().Name, go.transform.name);
                }
                else if (comp[i] is Animation)
                {
                    Animation anim = (Animation)comp[i];
                    sb.AppendFormat("{0}  {1} - {2}\n{0}    Playing Automatically: {3} - Wrap Mode: {4} - Clip Count: {5}\n{0}    Clips: {6}\n", preComp, comp[i].GetType().Name, go.transform.name
                        , anim.playAutomatically, anim.wrapMode, anim.GetClipCount(), AnimationClips(anim));
                }
                else
                {
                    sb.AppendFormat("{0}  {1} - {2}\n", preComp, comp[i].GetType().Name, comp[i].name);
                }
            }

            sb.AppendLine(preComp);
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
            DumpComponents(go, pre + front + (count > 0 ? "| " : "  "));

            for (int i = 0; i < count; i++)
            {
                DumpGameObjectChilds(neededChilds[i], i == count - 1 ? pre + front + " " : pre + front + "|", sb);
            }
        }

        private string AnimationClips(Animation anim)
        {
            StringBuilder asb = StringBuilderCache.Acquire(256);

            int i = 0;

            foreach (AnimationState clip in anim)
            {
                asb.AppendFormat("{0} - {1} | ", i.ToString(), clip.name);
                i++;
            }

            return asb.ToStringAndRelease();
        }

        private GameObject CheckForObjectUnderCursor()
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

                var raycastResults = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, raycastResults);

                if (raycastResults.Count == 0)
                {
                    //print("Nothing");
                    return null;
                }
                return raycastResults[0].gameObject;
            }

            if (mode == Mode.PART)
            {
                return Mouse.HoveredPart ? Mouse.HoveredPart.gameObject : null;
            }

            if (mode == Mode.OBJECT)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                //int layerMask = ~LayerMask.NameToLayer("UI");
                int layerMask = ~0;

                RaycastHit hit;
                if (!Physics.Raycast(ray, out hit, Mathf.Infinity, layerMask))
                {
                    return null;
                }
                return hit.collider.gameObject;
            }

            return null;
        }

        private GameObject GetRootObject(GameObject leaf)
        {
            if (leaf == null)
                return null;

            if (mode == Mode.UI)
            {
                int d = 0;
                while (leaf.transform.parent /*&& !leaf.transform.parent.gameObject.GetComponent<Canvas>()*/ && d < limitDepth)
                {
                    leaf = leaf.transform.parent.gameObject;
                    d++;
                }
                return leaf;
            }

            if (mode == Mode.PART)
            {
                return leaf;
            }

            if (mode == Mode.OBJECT)
            {
                int d = 0;
                while (leaf.transform.parent && d < limitDepth)
                {
                    leaf = leaf.transform.parent.gameObject;
                    d++;
                }
                return leaf;
            }

            return null;
        }

        private void DrawLabels(GameObject go)
        {
            if (!activeOnly || go.activeInHierarchy)
            {
                Profiler.BeginSample("DrawLabels");

                if (labels)
                {
                    Profiler.BeginSample("labels");
                    Camera cam;

                    if (HighLogic.LoadedSceneIsEditor)
                        cam = EditorLogic.fetch.editorCamera;
                    else if (HighLogic.LoadedSceneIsFlight)
                        cam = FlightCamera.fetch.mainCamera;
                    else
                        cam = Camera.main;

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


                    GUI.Label(new Rect(point.x, Screen.currentResolution.height - point.y, size.x, size.y), go.transform.name, styleTransform);

                    flip++;
                    Profiler.EndSample();
                }

                int count = go.transform.childCount;
                for (int i = 0; i < count; i++)
                {
                    GameObject child = go.transform.GetChild(i).gameObject;

                    if (!child.GetComponent<Part>() && child.name != "main camera pivot")
                        DrawLabels(child);
                }
                Profiler.EndSample();
            }
        }

        private void DrawObjects(GameObject go)
        {
            Profiler.BeginSample("DrawColliders");

            if (transforms)
            {
                if (!activeOnly || go.activeInHierarchy)
                {
                    Profiler.BeginSample("transforms");
                    DrawTools.DrawTransform(go.transform, 0.3f);
                    Profiler.EndSample();
                }
            }

            if (colliders)
            {
                if (!activeOnly || go.activeInHierarchy)
                {
                    Profiler.BeginSample("colliders");
                    Collider[] comp = go.GetComponents<Collider>();
                    for (int i = 0; i < comp.Length; i++)
                    {
                        Collider baseCol = comp[i];

                        if (baseCol is BoxCollider)
                        {
                            Profiler.BeginSample("BoxCollider");
                            BoxCollider box = baseCol as BoxCollider;
                            DrawTools.DrawLocalCube(box.transform, box.size, Color.yellow, box.center);
                            Profiler.EndSample();
                        }

                        if (baseCol is SphereCollider)
                        {
                            Profiler.BeginSample("SphereCollider");
                            SphereCollider sphere = baseCol as SphereCollider;
                            DrawTools.DrawSphere(sphere.transform.TransformPoint(sphere.center), Color.red, sphere.radius);
                            Profiler.EndSample();
                        }

                        if (baseCol is CapsuleCollider)
                        {
                            Profiler.BeginSample("CapsuleCollider");
                            CapsuleCollider caps = baseCol as CapsuleCollider;
                            Vector3 dir = new Vector3(caps.direction == 0 ? 1 : 0, caps.direction == 1 ? 1 : 0, caps.direction == 2 ? 1 : 0);
                            Vector3 top = caps.transform.TransformPoint(caps.center + caps.height * 0.5f * dir);
                            Vector3 bottom = caps.transform.TransformPoint(caps.center - caps.height * 0.5f * dir);
                            DrawTools.DrawCapsule(top, bottom, Color.green, caps.radius);
                            Profiler.EndSample();
                        }

                        if (baseCol is MeshCollider)
                        {
                            Profiler.BeginSample("MeshCollider");
                            MeshCollider mesh = baseCol as MeshCollider;
                            DrawTools.DrawLocalMesh(mesh.transform, mesh.sharedMesh, XKCDColors.ElectricBlue);
                            Profiler.EndSample();
                        }
                    }
                    Profiler.EndSample();
                }
            }

            if (bounds && mode != Mode.UI)
            {
                if (!activeOnly || go.activeInHierarchy)
                {
                    Profiler.BeginSample("bounds");

                    //DrawTools.DrawBounds(go.GetRendererBounds(), XKCDColors.Pink);

                    //Renderer[] renderers = go.GetComponents<Renderer>();
                    //for (int i = 0; i < renderers.Length; i++)
                    //{
                    //    Bounds bound = renderers[i].bounds;
                    //    DrawTools.DrawLocalCube(renderers[i].transform, bound.size, XKCDColors.Pink, bound.center);
                    //}

                    MeshFilter[] mesh = go.GetComponents<MeshFilter>();
                    for (int i = 0; i < mesh.Length; i++)
                    {
                        DrawTools.DrawLocalCube(mesh[i].transform, mesh[i].mesh.bounds.size, XKCDColors.Pink, mesh[i].mesh.bounds.center);
                    }
                    Profiler.EndSample();
                }
            }

            if (bounds && mode == Mode.UI)
            {
                Profiler.BeginSample("bounds");

                RectTransform[] rt = go.GetComponents<RectTransform>();
                for (int i = 0; i < rt.Length; i++)
                {
                    // TODO : search for the actual Canvas ?
                    DrawTools.DrawRectTransform(rt[i], UIMasterController.Instance.appCanvas, XKCDColors.GreenApple);
                }
                Profiler.EndSample();
            }

            if (meshes)
            {
                if (!activeOnly || go.activeInHierarchy)
                {
                    Profiler.BeginSample("meshes");
                    MeshFilter[] mesh = go.GetComponents<MeshFilter>();

                    for (int i = 0; i < mesh.Length; i++)
                    {
                        Profiler.BeginSample("LocalMesh");
                        DrawTools.DrawLocalMesh(mesh[i].transform, mesh[i].sharedMesh, XKCDColors.Orange);
                        Profiler.EndSample();
                    }
                    Profiler.EndSample();
                }
            }

            if (joints && mode == Mode.PART)
            {
                Profiler.BeginSample("joints");
                Part part = go.GetComponent<Part>();

                if (part != null)
                {
                    if (part.isAttached)
                    {
                        DrawTools.DrawJoint(part.attachJoint);
                    }
                }
                Profiler.EndSample();
            }
            
            int count = go.transform.childCount;
            for (int i = 0; i < count; i++)
            {
                GameObject child = go.transform.GetChild(i).gameObject;

                if (!child.GetComponent<Part>() && child.name != "main camera pivot")
                    DrawObjects(child);
            }
            Profiler.EndSample();
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
            layout.padding = new RectOffset(5, 5, 5, 2);

            var csf = panelPos.gameObject.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            addText(panelPos.gameObject, "Move the cursor over while holding shift to select an object");

            var buttonPanel = addEmptyPanel(panelPos.gameObject);

            var bpl = buttonPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
            bpl.childAlignment = TextAnchor.UpperCenter;
            bpl.childForceExpandHeight = true;
            bpl.childForceExpandWidth = true;
            bpl.padding = new RectOffset(5, 5, 2, 5);

            var bpsf = buttonPanel.gameObject.AddComponent<ContentSizeFitter>();
            bpsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            bpsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            addButton(buttonPanel.gameObject, "Dump to log", (b) => { print(sb.ToString()); });

            addButton(buttonPanel.gameObject, getSwitchString(activeOnly, "Active Only"), (b) => 
            {
                activeOnly = !activeOnly;
                b.text = getSwitchString(activeOnly, "Active Only");
            });

            //addButton(buttonPanel.gameObject, "List", (b) =>
            //{
            //    var stuff = Resources.LoadAll("");
            //    foreach (var o in stuff)
            //    {
            //        print(o.GetType() + "- " + o.name);
            //    }
            //});

            addButton(buttonPanel.gameObject, mode.ToString(), (b) =>
            {
                hoverObject = currentDisplayedObject = previousDisplayedObject = null;
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
                b.text = mode.ToString();
            });

            addButton(buttonPanel.gameObject, "-", (b) =>
            {
                limitDepth = Math.Max(0, limitDepth - 1);
                limitText.text = limitDepth.ToString();
                currentDisplayedObject = GetRootObject(hoverObject);
            });

            limitText = addText(buttonPanel.gameObject, limitDepth.ToString());
            limitText.alignment = TextAnchor.MiddleCenter;
            limitText.fontSize = 20;

            addButton(buttonPanel.gameObject, "+", (b) =>
            {
                limitDepth++;
                limitText.text = limitDepth.ToString();
                currentDisplayedObject = GetRootObject(hoverObject);
            });


            addButton(buttonPanel.gameObject, "*", (b) =>
            {
                var debug = GameObject.FindObjectOfType<DebugScreen>();
                print("Found DebugScreen");
                CanvasScaler canvascaler = debug.GetComponentInParent<CanvasScaler>();
                print("Found CanvasScaler");
                Canvas canva = debug.GetComponentInParent<Canvas>();
                print("Found Canvas");


                print(canva.referencePixelsPerUnit + " " + canva.pixelPerfect + " " + canva.name);
                print(canvascaler.referencePixelsPerUnit + " " + canvascaler.dynamicPixelsPerUnit);


                canvascaler.dynamicPixelsPerUnit = canvascaler.referencePixelsPerUnit;

            });



            var switchPanel = addEmptyPanel(panelPos.gameObject);

            var sl = switchPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
            sl.childAlignment = TextAnchor.UpperCenter;
            sl.childForceExpandHeight = true;
            sl.childForceExpandWidth = true;
            sl.padding = new RectOffset(5, 5, 5, 5);

            var ssf = switchPanel.gameObject.AddComponent<ContentSizeFitter>();
            ssf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            ssf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;


            addButton(switchPanel.gameObject, getSwitchString(labels, "Labels"), (b) =>
            {
                labels = !labels;
                b.text = getSwitchString(labels, "Labels");
            });

            addButton(switchPanel.gameObject, getSwitchString(transforms, "Transforms"), (b) =>
            {
                transforms = !transforms;
                b.text = getSwitchString(transforms, "Transforms");
            });

            addButton(switchPanel.gameObject, getSwitchString(colliders, "Colliders"), (b) =>
            {
                colliders = !colliders;
                b.text = getSwitchString(colliders, "Colliders");
            });

            addButton(switchPanel.gameObject, getSwitchString(meshes, "Meshes"), (b) =>
            {
                meshes = !meshes;
                b.text = getSwitchString(meshes, "Meshes");
            });

            addButton(switchPanel.gameObject, getSwitchString(bounds, "Bounds"), (b) =>
            {
                bounds = !bounds;
                b.text = getSwitchString(bounds, "Bounds");
            });

            addButton(switchPanel.gameObject, getSwitchString(joints, "Joints"), (b) =>
            {
                joints = !joints;
                b.text = getSwitchString(joints, "Joints");
            });

            info = addText(panelPos.gameObject, "");
            info.font = monoSpaceFont;
            info.fontSize = 12;

            partTree = addText(panelPos.gameObject, "");
            partTree.font = monoSpaceFont;
            partTree.fontSize = 11;

            return panelPos;
        }

        // Bow to my advanced UI skills !
        private string getSwitchString(bool state, string label)
        {
            return string.Format("[{0}] {1}", state ? "X" : " ", label);
        }

        private Button addButton(GameObject parent, string text, UnityAction<Text> click)
        {
            GameObject buttonObject = new GameObject("Button");


            buttonObject.layer = LayerMask.NameToLayer("UI");

            RectTransform trans = buttonObject.AddComponent<RectTransform>();
            trans.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            trans.localPosition.Set(0, 0, 0);

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.5f, 0f, 0, 0.5f);

            Button button = buttonObject.AddComponent<Button>();
            button.interactable = true;

            Text textObj = addText(buttonObject, text);

            button.onClick.AddListener(() => click(textObj));

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
