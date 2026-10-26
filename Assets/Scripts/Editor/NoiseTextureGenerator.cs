using System.IO;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public class NoiseTextureGenerator : EditorWindow
    {
        static readonly int[] SizeOptions = { 128, 256, 512, 1024, 2048 };
        static readonly string[] SizeLabels = { "128", "256", "512", "1024", "2048" };
        const int PreviewSize = 128;

        int sizeIndex = 2;          // 512
        float scale = 8f;           // ile "komórek" szumu na całą teksturę
        int octaves = 4;            // ile warstw szczegółu (fBm)
        int seed;
        string folderPath = "Assets/Textures";
        string fileName = "Noise";

        Texture2D preview;

        [MenuItem("Tools/Open Noise Generator")]
        static void Open()
        {
            var window = GetWindow<NoiseTextureGenerator>("Noise Generator");
            window.minSize = new Vector2(300, 460);
        }

        void OnEnable() => RegeneratePreview();

        void OnDisable()
        {
            if (preview != null)
                DestroyImmediate(preview);
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Noise", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            sizeIndex = EditorGUILayout.Popup("Size", sizeIndex, SizeLabels);
            scale = EditorGUILayout.Slider("Scale", scale, 1f, 64f);
            octaves = EditorGUILayout.IntSlider("Octaves", octaves, 1, 8);

            bool randomized = false;
            using (new EditorGUILayout.HorizontalScope())
            {
                seed = EditorGUILayout.IntField("Seed", seed);
                if (GUILayout.Button("Random", GUILayout.Width(70)))
                {
                    seed = Random.Range(0, 100000);
                    randomized = true;
                    GUI.FocusControl(null); // żeby pole Seed odświeżyło wartość
                }
            }

            if (EditorGUI.EndChangeCheck() || randomized)
                RegeneratePreview();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            folderPath = EditorGUILayout.TextField("Folder", folderPath);
            fileName = EditorGUILayout.TextField("File Name", fileName);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            if (preview != null)
            {
                Rect rect = GUILayoutUtility.GetAspectRect(1f, GUILayout.MaxWidth(256), GUILayout.MaxHeight(256));
                EditorGUI.DrawPreviewTexture(rect, preview);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Generate", GUILayout.Height(30)))
                SaveTexture();
        }

        void RegeneratePreview()
        {
            if (preview == null)
            {
                preview = new Texture2D(PreviewSize, PreviewSize, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            Fill(preview, PreviewSize);
        }

        // Próbkujemy w znormalizowanych UV (0–1), więc podgląd ma ten sam wzór
        // co finalna tekstura, niezależnie od rozdzielczości.
        void Fill(Texture2D texture, int size)
        {
            var rng = new System.Random(seed);
            var offset = new Vector2((float)rng.NextDouble() * 1000f, (float)rng.NextDouble() * 1000f);
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;
                    float n = SampleFbm(u * scale + offset.x, v * scale + offset.y);
                    pixels[y * size + x] = new Color(n, n, n, 1f);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
        }

        float SampleFbm(float x, float y)
        {
            float sum = 0f, amplitude = 1f, frequency = 1f, max = 0f;

            for (int i = 0; i < octaves; i++)
            {
                sum += Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude;
                max += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }

            return sum / max; // normalizacja do 0–1
        }

        void SaveTexture()
        {
            if (!folderPath.StartsWith("Assets"))
            {
                EditorUtility.DisplayDialog("Noise Generator", "Folder musi być wewnątrz Assets.", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                EditorUtility.DisplayDialog("Noise Generator", "Podaj nazwę pliku.", "OK");
                return;
            }

            string path = $"{folderPath.TrimEnd('/')}/{fileName}.png";

            if (File.Exists(path) &&
                !EditorUtility.DisplayDialog("Noise Generator", $"{path} już istnieje. Nadpisać?", "Nadpisz", "Anuluj"))
            {
                return;
            }

            int size = SizeOptions[sizeIndex];
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Fill(texture, size);

            Directory.CreateDirectory(folderPath);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            DestroyImmediate(texture);

            // Ustawienia importu: noise to dane, nie kolor
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();

            var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            Debug.Log($"Noise texture saved to {path}");
        }
    }
}