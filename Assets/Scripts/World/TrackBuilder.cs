// TrackBuilder.cs — circuito cerrado de carreras en un pueblo estilizado
// Óvalo con rectas y curvas, muros visibles en los bordes (sin caídas),
// casas, árboles, nubes y monedas con boost. Todo construido por código.
// Materiales compartidos y sin sombras pesadas (rendimiento Android).
using UnityEngine;
using System.Collections.Generic;

namespace Geayi.World
{
    public class TrackBuilder : MonoBehaviour
    {
        [Header("Circuito")]
        public float straightHalf = 60f; // media recta (x de -60 a 60)
        public float curveRadius = 25f;  // radio de las curvas
        public float trackWidth = 12f;
        public int coinCount = 8;

        public List<Vector3> Waypoints { get; private set; }
        public bool Built { get; private set; }

        private Mesh cubeMesh;
        private Mesh cylMesh;
        private Mesh sphMesh;
        private static Dictionary<string, Material> matCache = new Dictionary<string, Material>();
        private static Shader uiShader;

        // Material por color (compartido: menos draw calls en Android)
        public static Material Mat(string hex)
        {
            if (string.IsNullOrEmpty(hex)) hex = "#ffffff";
            Material m;
            if (matCache.TryGetValue(hex, out m) && m != null) return m;
            if (uiShader == null) uiShader = Shader.Find("UI/Default");
            Color c = Color.white;
            ColorUtility.TryParseHtmlString(hex, out c);
            m = new Material(uiShader);
            m.color = c;
            matCache[hex] = m;
            return m;
        }

        void Awake() { Build(); }

        public void Build()
        {
            if (Built) return;
            Built = true;

            cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            cylMesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            sphMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");

            // Cielo y luz de día
            RenderSettings.skybox = null;
            Camera cam = Camera.main;
            if (cam != null)
                cam.backgroundColor = new Color(0.53f, 0.81f, 0.98f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.75f, 0.78f, 0.85f);

            BuildGround();
            List<Vector3> center = BuildCenterline();
            BuildRoad(center);
            BuildStartLine(center);
            BuildWalls();
            BuildIsland();
            BuildDecorations();
            SpawnCoins(center);

            // Waypoints para la IA: un punto sí, uno no
            Waypoints = new List<Vector3>();
            for (int i = 0; i < center.Count; i += 2)
                Waypoints.Add(center[i]);

            Debug.Log("[TrackBuilder] Pista lista. Waypoints: " + Waypoints.Count);
        }

        // Parrilla de salida: 2 filas x 2 columnas detrás de la meta, mirando +X
        public void GetStartSlot(int slot, out Vector3 pos, out float yawDeg)
        {
            int row = slot / 2, col = slot % 2;
            pos = new Vector3(-straightHalf - 9f - row * 6f, 0f,
                              -curveRadius + (col == 0 ? -3f : 3f));
            yawDeg = 90f;
        }

        // ---------- geometría ----------

        private List<Vector3> BuildCenterline()
        {
            var pts = new List<Vector3>();
            float L = straightHalf, R = curveRadius;
            // Recta 1: (-L,-R) -> (L,-R)
            for (float x = -L; x <= L; x += 4f) pts.Add(new Vector3(x, 0f, -R));
            // Curva 1: centro (L,0), de -90° a 90°
            int cs = Mathf.RoundToInt(Mathf.PI * R / 4f);
            for (int i = 1; i <= cs; i++)
            {
                float a = -Mathf.PI / 2f + Mathf.PI * i / cs;
                pts.Add(new Vector3(L + R * Mathf.Cos(a), 0f, R * Mathf.Sin(a)));
            }
            // Recta 2: (L,R) -> (-L,R)
            for (float x = L - 4f; x >= -L; x -= 4f) pts.Add(new Vector3(x, 0f, R));
            // Curva 2: centro (-L,0), de 90° a 270°
            for (int i = 1; i <= cs; i++)
            {
                float a = Mathf.PI / 2f + Mathf.PI * i / cs;
                pts.Add(new Vector3(-L + R * Mathf.Cos(a), 0f, R * Mathf.Sin(a)));
            }
            return pts;
        }

        private void BuildGround()
        {
            Box("Ground", new Vector3(0f, -0.15f, 0f),
                new Vector3(240f, 0.3f, 160f), 0f, Mat("#57b34e"), false);
        }

        private void BuildRoad(List<Vector3> c)
        {
            int n = c.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = c[i];
                Vector3 t = (c[(i + 1) % n] - c[(i - 1 + n) % n]).normalized;
                float yaw = Mathf.Atan2(t.x, t.z) * Mathf.Rad2Deg;
                // Borde blanco (más ancho, debajo) + asfalto encima
                Box("Edge", new Vector3(p.x, -0.03f, p.z),
                    new Vector3(trackWidth + 1f, 0.06f, 4.7f), yaw, Mat("#f2f2f2"), false);
                Box("Road", new Vector3(p.x, 0f, p.z),
                    new Vector3(trackWidth, 0.08f, 4.6f), yaw, Mat("#3a3d44"), false);
                // Línea central punteada amarilla
                if (i % 3 == 0)
                    Box("Dash", new Vector3(p.x, 0.06f, p.z),
                        new Vector3(0.35f, 0.02f, 2f), yaw, Mat("#ffd94a"), false);
            }
        }

        private void BuildStartLine(List<Vector3> c)
        {
            Vector3 p = c[0]; // (-60, 0, -25), tangente +X: la meta cruza en Z
            for (int i = 0; i < 6; i++)
            {
                float lateral = -trackWidth / 2f + 2f * i + 1f;
                Box("Start", new Vector3(p.x, 0.07f, p.z + lateral),
                    new Vector3(1.2f, 0.03f, 2f), 0f,
                    Mat(i % 2 == 0 ? "#111111" : "#ffffff"), false);
            }
        }

        private void BuildWalls()
        {
            float hx = 96f, hz = 40f, h = 1.1f;
            Wall(new Vector3(0f, h / 2f, -hz), new Vector3(hx * 2f + 2f, h, 1f));
            Wall(new Vector3(0f, h / 2f, hz), new Vector3(hx * 2f + 2f, h, 1f));
            Wall(new Vector3(-hx, h / 2f, 0f), new Vector3(1f, h, hz * 2f + 2f));
            Wall(new Vector3(hx, h / 2f, 0f), new Vector3(1f, h, hz * 2f + 2f));
        }

        private void Wall(Vector3 pos, Vector3 scale)
        {
            Box("Wall", pos, scale, 0f, Mat("#f2f2f2"), true);
            Box("WallTop", pos + new Vector3(0f, scale.y / 2f + 0.12f, 0f),
                new Vector3(scale.x, 0.24f, scale.z), 0f, Mat("#d43a2e"), false);
        }

        private void BuildIsland()
        {
            // Isla central con casas (los carros no pueden entrar: tiene collider)
            Box("Island", new Vector3(0f, 0.5f, 0f),
                new Vector3(96f, 1f, 32f), 0f, Mat("#4da64d"), true);
            string[] houseColors = { "#e8e0d0", "#f2c14e", "#7ac2e8", "#e88a8a" };
            float[] hx = { -32f, -11f, 11f, 32f };
            for (int i = 0; i < 4; i++)
                House(hx[i], 1f, 0f, houseColors[i]);
            Tree(-40f, 1f, 9f); Tree(-21f, 1f, -9f); Tree(0f, 1f, 9f);
            Tree(21f, 1f, -9f); Tree(41f, 1f, 9f);
        }

        private void House(float x, float yBase, float z, string colorHex)
        {
            Box("House", new Vector3(x, yBase + 2.2f, z),
                new Vector3(8f, 4.4f, 7f), 0f, Mat(colorHex), false);
            Box("Roof", new Vector3(x, yBase + 4.9f, z),
                new Vector3(9f, 0.5f, 8f), 0f, Mat("#d4692a"), false);
            Box("Door", new Vector3(x, yBase + 1.1f, z - 3.55f),
                new Vector3(1.4f, 2.2f, 0.15f), 0f, Mat("#5a3a22"), false);
            Box("Win", new Vector3(x - 2.4f, yBase + 2.8f, z - 3.55f),
                new Vector3(1.4f, 1.2f, 0.15f), 0f, Mat("#bfe3f2"), false);
            Box("Win", new Vector3(x + 2.4f, yBase + 2.8f, z - 3.55f),
                new Vector3(1.4f, 1.2f, 0.15f), 0f, Mat("#bfe3f2"), false);
        }

        private void BuildDecorations()
        {
            // Árboles fuera de la pista
            float[][] spots = {
                new float[]{ -75f, -36f }, new float[]{ -20f, 36f },
                new float[]{ 30f, -36f }, new float[]{ 80f, 34f },
                new float[]{ 88f, -20f }, new float[]{ -88f, 15f },
                new float[]{ 55f, 36f }, new float[]{ -55f, -37f },
            };
            foreach (var s in spots) Tree(s[0], 0f, s[1]);
            // Nubes
            for (int i = 0; i < 6; i++)
            {
                float x = -90f + i * 36f;
                float y = 26f + (i % 3) * 4f;
                float z = (i % 2 == 0) ? -55f : 55f;
                Sph("Cloud", new Vector3(x, y, z),
                    new Vector3(7f, 3f, 5f), Mat("#ffffff"), false);
            }
        }

        private void Tree(float x, float yBase, float z)
        {
            GameObject t = new GameObject("Tree");
            t.transform.SetParent(transform, false);
            t.transform.position = new Vector3(x, yBase, z);
            Cyl(t, "Trunk", new Vector3(0f, 1f, 0f),
                new Vector3(0.5f, 2f, 0.5f), Mat("#6b4a2a"), false);
            Sph(t, "Leaves", new Vector3(0f, 2.9f, 0f),
                new Vector3(2.6f, 2.4f, 2.6f), Mat("#2e8b3a"), false);
        }

        private void SpawnCoins(List<Vector3> c)
        {
            GameObject root = new GameObject("Coins");
            root.transform.SetParent(transform, false);
            for (int k = 0; k < coinCount; k++)
            {
                int idx = k * c.Count / coinCount;
                Vector3 p = c[idx];
                CoinPickup.Spawn(root.transform, new Vector3(p.x, 1f, p.z), 5);
            }
        }

        // ---------- piezas ----------

        private GameObject Box(string name, Vector3 pos, Vector3 scale,
                               float yawDeg, Material mat, bool solid)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = cubeMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            if (solid)
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.size = Vector3.one;
            }
            return go;
        }

        private void Cyl(GameObject parent, string name, Vector3 pos,
                         Vector3 scale, Material mat, bool solid)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = cylMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
        }

        private void Sph(string name, Vector3 pos, Vector3 scale, Material mat, bool solid)
        {
            Sph(gameObject, name, pos, scale, mat, solid);
        }

        private void Sph(GameObject parent, string name, Vector3 pos,
                         Vector3 scale, Material mat, bool solid)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            if (parent == gameObject) go.transform.position = pos;
            else go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = sphMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
        }
    }
}
