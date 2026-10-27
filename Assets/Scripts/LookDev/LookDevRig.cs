using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LookDev
{
    [Serializable]
    public class LookDevSettings
    {
        [Header("Pivot")]
        public Vector3 pivot;

        [Header("Camera")]
        [Range(-180f, 180f)] public float cameraYaw;
        [Range(-89f, 89f)] public float cameraPitch = 15f;
        [Min(0.05f)] public float cameraDistance = 5f;
        [Range(5f, 120f)] public float fieldOfView = 60f;
        public bool cameraTurntable;
        public float cameraTurntableSpeed = 20f;   // stopnie na sekundę (tylko Play)

        [Header("Light")]
        [Range(-180f, 180f)] public float lightYaw = -30f;
        [Range(-90f, 90f)] public float lightPitch = 50f;
        [Tooltip("Yaw światła liczony względem kamery - światło 'jedzie' razem z kamerą")]
        public bool lightFollowsCamera;
        [Min(0f)] public float lightIntensity = 1f;
        public Color lightColor = Color.white;
        [Range(0f, 1f)] public float shadowStrength = 1f;
        [Tooltip("Odległość od pivota - tylko dla Point/Spot")]
        [Min(0f)] public float lightDistance = 5f;
        public bool lightTurntable;
        public float lightTurntableSpeed = 30f;    // stopnie na sekundę (tylko Play)

        public LookDevSettings Clone() => JsonUtility.FromJson<LookDevSettings>(JsonUtility.ToJson(this));
    }

    // Steruje kamerą (orbita wokół pivota) i światłem (yaw/pitch) z jednego miejsca.
    // Edit mode: inspector / okno Tools > Open Look Dev. Play mode: mysz + klawiatura + overlay (H).
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class LookDevRig : MonoBehaviour
    {
        public Camera targetCamera;
        public Light targetLight;
        public LookDevSettings settings = new LookDevSettings();

        [Header("Play mode")]
        public bool showOverlay = true;
        public float orbitSpeed = 0.25f;   // stopnie na piksel ruchu myszy
        public float panSpeed = 1f;
        [Range(0.01f, 0.5f)] public float zoomStep = 0.1f;

        const float OverlayWidth = 270f;

        LookDevSettings startSettings;     // stan z wejścia w Play - pod R
        float maxDistance = 20f;
        bool dragIgnored;                  // drag zaczęty nad overlayem nie kręci kamerą
        Rect overlayRect;
        Vector2 overlayScroll;

        void Reset()
        {
            AutoAssign();
            SyncFromScene();
        }

        void OnEnable()
        {
            if (!Application.isPlaying)
                return;
            startSettings = settings.Clone();
            maxDistance = Mathf.Max(20f, settings.cameraDistance * 4f);
        }

        void OnValidate() => Apply();

        void Update()
        {
            // W edit mode wszystko idzie przez OnValidate / okno - Update nie nadpisuje ręcznych zmian co klatkę
            if (!Application.isPlaying)
                return;

            HandleInput();

            if (settings.cameraTurntable)
                settings.cameraYaw += settings.cameraTurntableSpeed * Time.deltaTime;
            if (settings.lightTurntable)
                settings.lightYaw += settings.lightTurntableSpeed * Time.deltaTime;

            Apply();
        }

        public void AutoAssign()
        {
            if (targetCamera == null)
                targetCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();

            if (targetLight == null)
            {
                foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if (light.type == LightType.Directional)
                    {
                        targetLight = light;
                        break;
                    }
                }
            }
        }

        // Zaczytuje obecne transformy kamery i światła do ustawień, żeby dodanie riga niczego nie przeskoczyło
        public void SyncFromScene()
        {
            if (targetCamera != null)
            {
                var t = targetCamera.transform;
                Vector3 euler = t.rotation.eulerAngles;
                settings.cameraPitch = Mathf.DeltaAngle(0f, euler.x);
                settings.cameraYaw = Mathf.DeltaAngle(0f, euler.y);
                // pivot = rzut środka świata na oś widzenia kamery
                settings.cameraDistance = Mathf.Max(1f, Vector3.Dot(-t.position, t.forward));
                settings.pivot = t.position + t.forward * settings.cameraDistance;
                if (!targetCamera.orthographic)
                    settings.fieldOfView = targetCamera.fieldOfView;
            }

            if (targetLight != null)
            {
                Vector3 euler = targetLight.transform.rotation.eulerAngles;
                settings.lightPitch = Mathf.DeltaAngle(0f, euler.x);
                settings.lightYaw = Mathf.DeltaAngle(0f, euler.y);
                if (settings.lightFollowsCamera)
                    settings.lightYaw = Mathf.DeltaAngle(settings.cameraYaw, settings.lightYaw);
                settings.lightIntensity = targetLight.intensity;
                settings.lightColor = targetLight.color;
                settings.shadowStrength = targetLight.shadowStrength;
                if (targetLight.type != LightType.Directional)
                    settings.lightDistance = Vector3.Distance(settings.pivot, targetLight.transform.position);
            }
        }

        public void Apply()
        {
            Clamp();

            if (targetCamera != null)
            {
                Quaternion rot = CameraRotation;
                targetCamera.transform.SetPositionAndRotation(settings.pivot - rot * Vector3.forward * settings.cameraDistance, rot);
                if (!targetCamera.orthographic)
                    targetCamera.fieldOfView = settings.fieldOfView;
            }

            if (targetLight != null)
            {
                Quaternion rot = LightRotation;
                var t = targetLight.transform;
                if (targetLight.type == LightType.Directional)
                    t.rotation = rot;
                else
                    t.SetPositionAndRotation(settings.pivot - rot * Vector3.forward * settings.lightDistance, rot);

                targetLight.intensity = settings.lightIntensity;
                targetLight.color = settings.lightColor;
                targetLight.shadowStrength = settings.shadowStrength;
            }
        }

        public Quaternion CameraRotation => Quaternion.Euler(settings.cameraPitch, settings.cameraYaw, 0f);

        public Quaternion LightRotation =>
            Quaternion.Euler(settings.lightPitch, settings.lightYaw + (settings.lightFollowsCamera ? settings.cameraYaw : 0f), 0f);

        public void Frame(Bounds bounds)
        {
            settings.pivot = bounds.center;
            float radius = Mathf.Max(0.1f, bounds.extents.magnitude);
            float halfFov = settings.fieldOfView * 0.5f * Mathf.Deg2Rad;
            settings.cameraDistance = radius / Mathf.Sin(halfFov);
            maxDistance = Mathf.Max(maxDistance, settings.cameraDistance * 2f);
            Apply();
        }

        public void FrameAll() => FrameRenderers(FindObjectsByType<Renderer>(FindObjectsSortMode.None));

        public bool FrameRenderers(IEnumerable<Renderer> renderers)
        {
            bool found = false;
            var bounds = new Bounds();
            foreach (var r in renderers)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy)
                    continue;
                if (found)
                    bounds.Encapsulate(r.bounds);
                else
                    bounds = r.bounds;
                found = true;
            }

            if (found)
                Frame(bounds);
            return found;
        }

        void Clamp()
        {
            settings.cameraYaw = WrapAngle(settings.cameraYaw);
            settings.lightYaw = WrapAngle(settings.lightYaw);
            settings.cameraPitch = Mathf.Clamp(settings.cameraPitch, -89f, 89f);
            settings.lightPitch = Mathf.Clamp(settings.lightPitch, -90f, 90f);
            settings.cameraDistance = Mathf.Max(0.05f, settings.cameraDistance);
            settings.fieldOfView = Mathf.Clamp(settings.fieldOfView, 5f, 120f);
        }

        static float WrapAngle(float a) => Mathf.Repeat(a + 180f, 360f) - 180f;

        void HandleInput()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            if (keyboard != null)
            {
                if (keyboard.hKey.wasPressedThisFrame) showOverlay = !showOverlay;
                if (keyboard.spaceKey.wasPressedThisFrame) settings.cameraTurntable = !settings.cameraTurntable;
                if (keyboard.lKey.wasPressedThisFrame) settings.lightTurntable = !settings.lightTurntable;
                if (keyboard.fKey.wasPressedThisFrame) FrameAll();
                if (keyboard.rKey.wasPressedThisFrame && startSettings != null) settings = startSettings.Clone();
            }

            if (mouse == null)
                return;

            Vector2 mousePos = mouse.position.ReadValue();
            bool overOverlay = IsOverOverlay(mousePos);

            if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame)
                dragIgnored = overOverlay;
            if (!mouse.leftButton.isPressed && !mouse.rightButton.isPressed && !mouse.middleButton.isPressed)
                dragIgnored = false;

            if (!dragIgnored)
            {
                Vector2 delta = mouse.delta.ReadValue();
                bool shift = keyboard != null && keyboard.shiftKey.isPressed;

                if (mouse.rightButton.isPressed || (shift && mouse.leftButton.isPressed))
                {
                    settings.lightYaw += delta.x * orbitSpeed;
                    settings.lightPitch += delta.y * orbitSpeed;
                }
                else if (mouse.leftButton.isPressed)
                {
                    settings.cameraYaw += delta.x * orbitSpeed;
                    settings.cameraPitch -= delta.y * orbitSpeed;
                }

                if (mouse.middleButton.isPressed)
                {
                    float scale = settings.cameraDistance * panSpeed * 0.002f;
                    Quaternion rot = CameraRotation;
                    settings.pivot -= (rot * Vector3.right * delta.x + rot * Vector3.up * delta.y) * scale;
                }
            }

            // Wartości scrolla różnią się między platformami (1 vs 120 na ząbek) - liczy się tylko kierunek
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f && !overOverlay)
                settings.cameraDistance *= Mathf.Pow(1f - zoomStep, Mathf.Sign(scroll));
        }

        bool IsOverOverlay(Vector2 screenPos)
        {
            if (!showOverlay)
                return false;
            // Input System: y od dołu, IMGUI: y od góry
            return overlayRect.Contains(new Vector2(screenPos.x, Screen.height - screenPos.y));
        }

        void OnGUI()
        {
            if (!Application.isPlaying || !showOverlay)
                return;

            overlayRect = new Rect(10f, 10f, OverlayWidth, Mathf.Min(620f, Screen.height - 20f));
            GUILayout.BeginArea(overlayRect, GUI.skin.box);
            overlayScroll = GUILayout.BeginScrollView(overlayScroll);

            GUILayout.Label("<b>Camera</b>", RichLabel);
            settings.cameraYaw = Slider("Yaw", settings.cameraYaw, -180f, 180f);
            settings.cameraPitch = Slider("Pitch", settings.cameraPitch, -89f, 89f);
            maxDistance = Mathf.Max(maxDistance, settings.cameraDistance); // scroll może wyjechać poza zakres slidera
            settings.cameraDistance = Slider("Distance", settings.cameraDistance, 0.1f, maxDistance);
            settings.fieldOfView = Slider("FOV", settings.fieldOfView, 5f, 120f);
            settings.cameraTurntable = GUILayout.Toggle(settings.cameraTurntable, " Turntable (Space)");
            if (settings.cameraTurntable)
                settings.cameraTurntableSpeed = Slider("Speed", settings.cameraTurntableSpeed, -180f, 180f);

            GUILayout.Space(6f);
            GUILayout.Label("<b>Light</b>", RichLabel);
            settings.lightYaw = Slider("Yaw", settings.lightYaw, -180f, 180f);
            settings.lightPitch = Slider("Pitch", settings.lightPitch, -90f, 90f);
            settings.lightFollowsCamera = GUILayout.Toggle(settings.lightFollowsCamera, " Follow camera");
            settings.lightIntensity = Slider("Intensity", settings.lightIntensity, 0f, 10f);
            settings.shadowStrength = Slider("Shadow strength", settings.shadowStrength, 0f, 1f);
            Color c = settings.lightColor;
            c.r = Slider("R", c.r, 0f, 1f);
            c.g = Slider("G", c.g, 0f, 1f);
            c.b = Slider("B", c.b, 0f, 1f);
            settings.lightColor = c;
            settings.lightTurntable = GUILayout.Toggle(settings.lightTurntable, " Turntable (L)");
            if (settings.lightTurntable)
                settings.lightTurntableSpeed = Slider("Speed", settings.lightTurntableSpeed, -180f, 180f);

            GUILayout.Space(6f);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Frame all (F)")) FrameAll();
                if (GUILayout.Button("Reset (R)") && startSettings != null) settings = startSettings.Clone();
            }

            GUILayout.Space(6f);
            GUILayout.Label(
                "LMB drag - orbit camera\n" +
                "RMB / Shift+LMB drag - rotate light\n" +
                "MMB drag - pan pivot\n" +
                "Scroll - zoom\n" +
                "H - hide this panel");

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        static GUIStyle richLabel;
        static GUIStyle RichLabel => richLabel ??= new GUIStyle(GUI.skin.label) { richText = true };

        static float Slider(string label, float value, float min, float max)
        {
            GUILayout.Label($"{label}: {value:0.##}");
            return GUILayout.HorizontalSlider(value, min, max);
        }
    }
}
