using System.Collections.Generic;
using DuelProtocol.Content;
using UnityEngine;

namespace DuelProtocol.Presentation
{
    public static class DuelWorldArtDirector
    {
        private static readonly Dictionary<int, Material> Materials = new Dictionary<int, Material>();

        public static void EnhanceArena(GameObject floor)
        {
            if (floor == null || floor.transform.Find("Production Art") != null) return;
            var root = new GameObject("Production Art").transform;
            root.SetParent(floor.transform.parent, false);

            StyleRenderer(floor.GetComponent<Renderer>(), new Color32(15, 26, 38, 255), Color.black, .25f);

            CreatePart("Inner Platform", PrimitiveType.Cylinder, root,
                new Vector3(0, .06f, 0), new Vector3(12.8f, .12f, 12.8f),
                new Color32(22, 37, 51, 255), new Color32(6, 70, 84, 255));
            CreatePart("Core Dais", PrimitiveType.Cylinder, root,
                new Vector3(0, .16f, 0), new Vector3(3.4f, .18f, 3.4f),
                new Color32(42, 38, 25, 255), new Color32(255, 166, 28, 255));
            CreatePart("Core Halo", PrimitiveType.Cylinder, root,
                new Vector3(0, .28f, 0), new Vector3(2.4f, .035f, 2.4f),
                new Color32(81, 54, 18, 255), new Color32(255, 190, 54, 255));

            for (var i = -3; i <= 3; i++)
            {
                if (i == 0) continue;
                CreatePart("Grid X " + i, PrimitiveType.Cube, root,
                    new Vector3(i * 2.2f, .31f, 0), new Vector3(.035f, .018f, 15.6f),
                    new Color32(22, 65, 79, 255), new Color32(28, 164, 191, 255));
                CreatePart("Grid Z " + i, PrimitiveType.Cube, root,
                    new Vector3(0, .31f, i * 2.2f), new Vector3(15.6f, .018f, .035f),
                    new Color32(62, 35, 42, 255), new Color32(170, 43, 63, 255));
            }

            CreateTeamLane(root, -5.8f, new Color32(28, 220, 255, 255));
            CreateTeamLane(root, 5.8f, new Color32(255, 63, 86, 255));
            CreateCornerArchitecture(root);
            CreateArenaLight("Cyan Arena Light", root, new Vector3(-5.5f, 4.5f, -1.5f), DuelPalette.Cyan, 4.2f, 11f);
            CreateArenaLight("Crimson Arena Light", root, new Vector3(5.5f, 4.5f, -1.5f), DuelPalette.Red, 4.2f, 11f);
            CreateArenaLight("Core Arena Light", root, new Vector3(0f, 5.2f, 1.5f), DuelPalette.Gold, 3.4f, 10f);
        }

        public static void StyleBoundary(GameObject boundary)
        {
            if (boundary == null) return;
            StyleRenderer(boundary.GetComponent<Renderer>(), new Color32(20, 34, 47, 255), new Color32(20, 102, 126, 255), .8f);
            var cap = CreatePart("Light Rail", PrimitiveType.Cube, boundary.transform,
                new Vector3(0, .56f, 0), new Vector3(1.02f, .06f, 1.02f),
                new Color32(35, 69, 86, 255), new Color32(28, 220, 255, 255));
            cap.transform.localRotation = Quaternion.identity;
        }

        public static void StylePlayer(GameObject player, Color teamColor, bool localSkin)
        {
            if (player == null || player.transform.Find("Robot Rig") != null) return;
            foreach (var renderer in player.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            var rig = new GameObject("Robot Rig").transform;
            rig.SetParent(player.transform, false);
            rig.localScale = Vector3.one * 1.28f;
            var skin = localSkin ? DuelCosmeticCatalog.Equipped : DuelCosmeticCatalog.Skins[0];
            var armor = skin.Armor;
            var dark = Color.Lerp(armor, Color.black, .48f);

            var shadow = CreatePart("Contact Shadow", PrimitiveType.Cylinder, rig,
                new Vector3(0, -.68f, 0), new Vector3(1.25f, .018f, 1.25f),
                new Color(0, 0, 0, .58f), Color.black);
            shadow.transform.localRotation = Quaternion.identity;
            CreatePart("Torso", PrimitiveType.Cube, rig, new Vector3(0, .2f, 0), new Vector3(.82f, .7f, .54f), armor, teamColor);
            CreatePart("Chest Core", PrimitiveType.Sphere, rig, new Vector3(0, .23f, .29f), new Vector3(.26f, .2f, .1f), teamColor, teamColor);
            CreatePart("Head", PrimitiveType.Cube, rig, new Vector3(0, .78f, .02f), new Vector3(.58f, .38f, .48f), dark, Color.black);
            CreatePart("Visor", PrimitiveType.Cube, rig, new Vector3(0, .81f, .275f), new Vector3(.43f, .11f, .035f), teamColor, teamColor);
            CreatePart("Shoulder L", PrimitiveType.Sphere, rig, new Vector3(-.56f, .34f, 0), new Vector3(.34f, .34f, .42f), armor, teamColor);
            CreatePart("Shoulder R", PrimitiveType.Sphere, rig, new Vector3(.56f, .34f, 0), new Vector3(.34f, .34f, .42f), armor, teamColor);
            CreatePart("Arm L", PrimitiveType.Capsule, rig, new Vector3(-.62f, -.03f, .03f), new Vector3(.2f, .34f, .2f), dark, teamColor);
            CreatePart("Arm R", PrimitiveType.Capsule, rig, new Vector3(.62f, -.03f, .03f), new Vector3(.2f, .34f, .2f), dark, teamColor);
            CreatePart("Leg L", PrimitiveType.Capsule, rig, new Vector3(-.24f, -.35f, 0), new Vector3(.25f, .38f, .25f), dark, teamColor);
            CreatePart("Leg R", PrimitiveType.Capsule, rig, new Vector3(.24f, -.35f, 0), new Vector3(.25f, .38f, .25f), dark, teamColor);
            CreatePart("Heel L", PrimitiveType.Cube, rig, new Vector3(-.24f, -.67f, .09f), new Vector3(.34f, .14f, .48f), armor, teamColor);
            CreatePart("Heel R", PrimitiveType.Cube, rig, new Vector3(.24f, -.67f, .09f), new Vector3(.34f, .14f, .48f), armor, teamColor);
            rig.gameObject.AddComponent<DuelRobotMotion>();
        }

        public static void StyleCore(GameObject core)
        {
            if (core == null || core.transform.Find("Core Art") != null) return;
            foreach (var renderer in core.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            var root = new GameObject("Core Art").transform;
            root.SetParent(core.transform, false);
            CreatePart("Inner Energy", PrimitiveType.Sphere, root, Vector3.zero, Vector3.one * .72f,
                new Color32(255, 192, 45, 255), new Color32(255, 190, 54, 255));
            CreatePart("Outer Shell", PrimitiveType.Sphere, root, Vector3.zero, Vector3.one * .95f,
                new Color32(112, 69, 17, 255), new Color32(255, 144, 15, 255));
            var ringA = CreatePart("Orbit A", PrimitiveType.Cylinder, root, Vector3.zero, new Vector3(1.28f, .035f, 1.28f),
                new Color32(128, 81, 20, 255), new Color32(255, 190, 54, 255));
            ringA.transform.localRotation = Quaternion.Euler(65, 0, 0);
            var ringB = CreatePart("Orbit B", PrimitiveType.Cylinder, root, Vector3.zero, new Vector3(1.12f, .03f, 1.12f),
                new Color32(128, 81, 20, 255), new Color32(255, 220, 100, 255));
            ringB.transform.localRotation = Quaternion.Euler(25, 0, 65);
            root.gameObject.AddComponent<DuelCoreMotion>();
            var light = root.gameObject.AddComponent<Light>();
            light.type = LightType.Point; light.color = DuelPalette.Gold; light.range = 4.2f; light.intensity = 2.2f;
        }

        public static void EnhanceLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color32(24, 43, 61, 255);
            RenderSettings.ambientEquatorColor = new Color32(10, 22, 34, 255);
            RenderSettings.ambientGroundColor = new Color32(4, 7, 12, 255);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color32(5, 13, 22, 255);
            RenderSettings.fogDensity = .012f;
        }

        private static void CreateTeamLane(Transform root, float x, Color color)
        {
            CreatePart("Team Lane", PrimitiveType.Cube, root, new Vector3(x, .33f, 0), new Vector3(.12f, .025f, 12f), color, color);
            CreatePart("Team Pad", PrimitiveType.Cylinder, root, new Vector3(x, .34f, 0), new Vector3(2.2f, .035f, 2.2f), Color.Lerp(color, Color.black, .7f), color);
        }

        private static void CreateCornerArchitecture(Transform root)
        {
            var positions = new[] { new Vector3(-10.6f, 1.8f, -10.6f), new Vector3(10.6f, 1.8f, -10.6f),
                new Vector3(-10.6f, 1.8f, 10.6f), new Vector3(10.6f, 1.8f, 10.6f) };
            for (var i = 0; i < positions.Length; i++)
            {
                CreatePart("Arena Pylon " + (i + 1), PrimitiveType.Cube, root, positions[i], new Vector3(1.4f, 3.6f, 1.4f),
                    new Color32(12, 25, 38, 255), i % 2 == 0 ? new Color32(28, 129, 154, 255) : new Color32(153, 38, 55, 255));
                CreatePart("Pylon Beacon " + (i + 1), PrimitiveType.Sphere, root, positions[i] + Vector3.up * 2.2f, Vector3.one * .32f,
                    i % 2 == 0 ? DuelPalette.Cyan : DuelPalette.Red, i % 2 == 0 ? DuelPalette.Cyan : DuelPalette.Red);
            }
        }

        private static void CreateArenaLight(string name, Transform parent, Vector3 position, Color color, float intensity, float range)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = position;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        private static GameObject CreatePart(string name, PrimitiveType type, Transform parent,
            Vector3 localPosition, Vector3 localScale, Color color, Color emission)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            var collider = part.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
            StyleRenderer(part.GetComponent<Renderer>(), color, emission, 1.15f);
            return part;
        }

        private static void StyleRenderer(Renderer renderer, Color color, Color emission, float emissionStrength)
        {
            if (renderer == null) return;
            var key = color.GetHashCode() * 397 ^ emission.GetHashCode();
            if (!Materials.TryGetValue(key, out var material))
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = "Duel Art Material", color = color, hideFlags = HideFlags.HideAndDontSave };
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                if (emission.maxColorComponent > .02f)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", emission * emissionStrength);
                }
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .58f);
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", .68f);
                Materials[key] = material;
            }
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }

    public sealed class DuelRobotMotion : MonoBehaviour
    {
        private Vector3 _start;
        private float _phase;
        private void Awake()
        {
            _start = transform.localPosition;
            _phase = transform.root.name.Length * .73f;
        }
        private void Update()
        {
            if (DuelPlayerPreferences.Current.ReducedMotion) { transform.localPosition = _start; return; }
            transform.localPosition = _start + Vector3.up * (Mathf.Sin(Time.time * 4.5f + _phase) * .035f);
        }
    }

    public sealed class DuelCoreMotion : MonoBehaviour
    {
        private void Update()
        {
            if (!DuelPlayerPreferences.Current.ReducedMotion) transform.Rotate(28f * Time.deltaTime, 52f * Time.deltaTime, 17f * Time.deltaTime, Space.Self);
        }
    }
}
