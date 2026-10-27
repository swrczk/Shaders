using System.Linq;
using LookDev;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Editor
{
    public class LookDevWindow : EditorWindow
    {
        const float LightDragSpeed = 0.4f;   // stopnie na piksel w Scene view

        LookDevRig rig;
        SerializedObject serializedRig;
        Vector2 scroll;
        bool showPivotHandle = true;
        bool lightDragKeyHeld;               // trzymane L w Scene view

        [MenuItem("Tools/Open Look Dev")]
        static void Open()
        {
            var window = GetWindow<LookDevWindow>("Look Dev");
            window.minSize = new Vector2(300, 400);
        }

        void OnEnable() => SceneView.duringSceneGui += OnSceneGUI;

        void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;

        // W Play mode wartości zmieniają się same (turntable, mysz) - odświeżamy okno
        void OnInspectorUpdate()
        {
            if (Application.isPlaying)
                Repaint();
        }

        void FindRig()
        {
            if (rig == null)
                rig = FindFirstObjectByType<LookDevRig>();
            if (rig != null && (serializedRig == null || serializedRig.targetObject != rig))
                serializedRig = new SerializedObject(rig);
        }

        void OnGUI()
        {
            FindRig();

            if (rig == null)
            {
                EditorGUILayout.HelpBox("Brak LookDevRig w scenie.", MessageType.Info);
                if (GUILayout.Button("Create Look Dev Rig", GUILayout.Height(30)))
                    CreateRig();
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);

            serializedRig.Update();
            EditorGUILayout.PropertyField(serializedRig.FindProperty(nameof(LookDevRig.targetCamera)));
            EditorGUILayout.PropertyField(serializedRig.FindProperty(nameof(LookDevRig.targetLight)));

            // Rysujemy dzieci "settings" płasko, bez foldoutu
            var settings = serializedRig.FindProperty(nameof(LookDevRig.settings));
            var end = settings.GetEndProperty();
            var it = settings.Copy();
            it.NextVisible(true);
            while (!SerializedProperty.EqualContents(it, end))
            {
                EditorGUILayout.PropertyField(it, true);
                if (!it.NextVisible(false))
                    break;
            }
            serializedRig.ApplyModifiedProperties(); // odpala OnValidate -> Apply

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Sync from scene"))
                    Modify("Sync Look Dev from scene", r => r.SyncFromScene());
                if (GUILayout.Button("Pivot → selection"))
                {
                    var renderers = Selection.gameObjects.SelectMany(go => go.GetComponentsInChildren<Renderer>());
                    Modify("Frame selection", r =>
                    {
                        if (!r.FrameRenderers(renderers))
                            ShowNotification(new GUIContent("Zaznacz obiekt z rendererem"));
                    });
                }
                if (GUILayout.Button("Frame all"))
                    Modify("Frame all", r => r.FrameAll());
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Scene view → camera"))
                    Modify("Scene view to camera", CopyFromSceneView);
                if (GUILayout.Button("Camera → scene view"))
                    CopyToSceneView();
            }

            EditorGUI.BeginChangeCheck();
            showPivotHandle = EditorGUILayout.Toggle("Pivot handle in Scene", showPivotHandle);
            if (EditorGUI.EndChangeCheck())
                SceneView.RepaintAll();

            if (Application.isPlaying)
            {
                EditorGUILayout.Space();
                if (GUILayout.Button("Keep values after Play", GUILayout.Height(26)))
                {
                    LookDevPlayModeKeeper.Store(rig);
                    ShowNotification(new GUIContent("Wartości zostaną przeniesione po wyjściu z Play"));
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Scene view: trzymaj L + przeciągnij LMB, żeby obracać światło.\n" +
                "Play mode: LMB orbit, RMB / Shift+LMB światło, MMB pan, scroll zoom, " +
                "Space/L turntable, F frame, R reset, H overlay.",
                MessageType.None);

            EditorGUILayout.EndScrollView();
        }

        void CreateRig()
        {
            var go = new GameObject("LookDev Rig");
            Undo.RegisterCreatedObjectUndo(go, "Create Look Dev Rig");
            rig = go.AddComponent<LookDevRig>();
            rig.AutoAssign();
            rig.SyncFromScene();
            rig.Apply();
            Selection.activeGameObject = go;
        }

        void Modify(string undoName, System.Action<LookDevRig> action)
        {
            Undo.RecordObject(rig, undoName);
            action(rig);
            rig.Apply();
            SceneView.RepaintAll();
        }

        static void CopyFromSceneView(LookDevRig r)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                return;
            Vector3 euler = sceneView.rotation.eulerAngles;
            r.settings.pivot = sceneView.pivot;
            r.settings.cameraPitch = Mathf.DeltaAngle(0f, euler.x);
            r.settings.cameraYaw = Mathf.DeltaAngle(0f, euler.y);
            r.settings.cameraDistance = sceneView.cameraDistance;
        }

        void CopyToSceneView()
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                return;
            // SceneView liczy dystans z "size": distance = size / tan(fov/2)
            float halfFov = sceneView.camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            sceneView.LookAt(rig.settings.pivot, rig.CameraRotation, rig.settings.cameraDistance * Mathf.Tan(halfFov));
        }

        void OnSceneGUI(SceneView sceneView)
        {
            FindRig();
            if (rig == null)
                return;

            DrawLightGizmo();

            if (showPivotHandle)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 pivot = Handles.PositionHandle(rig.settings.pivot, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                    Modify("Move Look Dev pivot", r => r.settings.pivot = pivot);
            }

            HandleLightDrag();
        }

        void DrawLightGizmo()
        {
            Vector3 pivot = rig.settings.pivot;
            float size = HandleUtility.GetHandleSize(pivot) * 1.5f;
            Vector3 dir = rig.LightRotation * Vector3.forward;

            Handles.color = new Color(1f, 0.85f, 0.3f);
            Vector3 from = pivot - dir * size;
            Handles.DrawLine(from, pivot, 2f);
            Handles.ConeHandleCap(0, pivot - dir * size * 0.15f, rig.LightRotation, size * 0.15f, EventType.Repaint);
            Handles.DrawWireDisc(from, dir, size * 0.12f);
        }

        void HandleLightDrag()
        {
            var e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive);

            switch (e.type)
            {
                case EventType.KeyDown when e.keyCode == KeyCode.L:
                    lightDragKeyHeld = true;
                    e.Use();
                    break;
                case EventType.KeyUp when e.keyCode == KeyCode.L:
                    lightDragKeyHeld = false;
                    e.Use();
                    break;
                case EventType.MouseDown when e.button == 0 && lightDragKeyHeld:
                    GUIUtility.hotControl = id;
                    e.Use();
                    break;
                case EventType.MouseDrag when GUIUtility.hotControl == id:
                    Modify("Rotate Look Dev light", r =>
                    {
                        r.settings.lightYaw += e.delta.x * LightDragSpeed;
                        r.settings.lightPitch -= e.delta.y * LightDragSpeed;
                    });
                    Repaint();
                    e.Use();
                    break;
                case EventType.MouseUp when GUIUtility.hotControl == id:
                    GUIUtility.hotControl = 0;
                    e.Use();
                    break;
            }
        }
    }

    // Wartości ustawione w Play mode giną po wyjściu - ten hook przenosi je z powrotem do sceny
    [InitializeOnLoad]
    static class LookDevPlayModeKeeper
    {
        const string Key = "LookDev.KeepValues";

        static LookDevPlayModeKeeper() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        public static void Store(LookDevRig rig) => SessionState.SetString(Key, JsonUtility.ToJson(rig.settings));

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode)
                return;

            string json = SessionState.GetString(Key, string.Empty);
            if (string.IsNullOrEmpty(json))
                return;
            SessionState.EraseString(Key);

            var rig = Object.FindFirstObjectByType<LookDevRig>();
            if (rig == null)
                return;

            Undo.RecordObject(rig, "Keep Look Dev values");
            JsonUtility.FromJsonOverwrite(json, rig.settings);
            rig.Apply();
            EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
        }
    }
}
