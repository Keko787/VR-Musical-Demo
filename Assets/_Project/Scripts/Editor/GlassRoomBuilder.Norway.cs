using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VRShootingGallery.GlassRoom;
using VRShootingGallery.Visualizer;
using static VRShootingGallery.EditorTools.StringInstrumentBuilder;

namespace VRShootingGallery.EditorTools
{
    /// <summary>
    /// The glass room's Norway group — nine views after photographs, each a world of its own — and the
    /// world menu on the front wall that picks between every world outside.
    /// </summary>
    /// <remarks>
    /// The Norway worlds share one way of being built: a <see cref="SurfaceProfile.Fjordland"/>
    /// ground of mountains, valleys, fjords, peaks and tarns, painted by height and slope; water at
    /// fjord or sea level; and whatever stands in the view — villages, fish farms, a ship,
    /// waterfalls, forest, boulders, grass, birds. A <see cref="NorwayView"/> holds the numbers that
    /// make each one different. The music plays on all of them the same few ways: the clouds open
    /// with the loudness and their shadows slide over the land, beat rings ripple across the water,
    /// waterfalls run with the bass, and the grass sways with the mids.
    /// </remarks>
    public static partial class GlassRoomBuilder
    {
        // ================================================================== Norway

        /// <summary>Everything that differs between the Norway views. Heights are metres from the room's floor.</summary>
        class NorwayView
        {
            public string Name, Title;
            public int Seed = 1;

            // The ground.
            public bool HasWater = true;
            public float Water = -500f;
            public float Base = -500f, Rim = 1000f, Ridges = 2000f, Plateau, SeaFloor = -80f;
            public Valley[] Valleys = { };
            public Vector4[] Peaks = { }, Tarns = { };
            public Vector4 Coast, Bay;
            public Vector2 Summit, Horizon;
            public float SeaHorizon;
            public float ViewRadius = 600f, ViewSlope = -0.05f, Level = 6f;

            /// <summary>
            /// Metres the room floats above the ground, as the drone the photograph was taken from did. From
            /// eye height on a broad summit its own shoulder hides everything below; from above, the fjord
            /// opens up under the glass.
            /// </summary>
            public float Hover;

            // Its paint.
            public Color Forest = new(0.13f, 0.27f, 0.13f), Heath = new(0.42f, 0.5f, 0.24f), Rock = new(0.46f, 0.46f, 0.47f);
            public Color Fields = new(0.48f, 0.7f, 0.24f, 1f), Shore = new(0.6f, 0.58f, 0.53f, 1f), SnowColour = new(0.92f, 0.95f, 1f);
            public float TreeLine = 500f, FieldLine = 100f, ShoreHeight = 4f, SnowLine = 10000f, Snow = 1f, SnowGlint, RockPatches = 0.6f;
            public float Surf, SurfHeight = 2.5f;
            public Color WaterColour = new(0.16f, 0.48f, 0.52f);
            public float Reflectivity = 0.5f;

            // The sky.
            public Color Zenith = new(0.3f, 0.52f, 0.86f), SkyHorizon = new(0.78f, 0.86f, 0.94f), Below = new(0.5f, 0.6f, 0.66f);
            public Vector3 Sun = new(-0.5f, 0.7f, 0.4f);
            public Color SunColour = new(1.15f, 1.1f, 1f), Shadow = new(0.42f, 0.48f, 0.64f), CloudColour = Color.white;
            public float Cirrus = 0.15f, Cover, Darkness = 0.25f, Open = 0.45f, CloudShadows = 0.55f;
            public Vector2 CloudSize = new(1600f, 2400f), Wind = new(5f, -2f);
            public float FogDistance = 4500f, FogAmount = 0.75f, FogSunGlow = 0.25f;
            public Vector2 Mist;
            public float MistDensity;

            // The room.
            public Color RoomAmbient = new(0.6f, 0.62f, 0.66f), RoomLight = new(1f, 0.97f, 0.92f);
            public float RoomLightIntensity = 1.1f, Lamps = 0.35f;

            // The music.
            public Vector3 RingOrigin;
            public bool Rings = true;
            public Color RingColour = new(0.55f, 0.8f, 0.85f);
            public float RingSpeed = 160f;
            public bool Lightning;

            // What stands in it.
            public Vector4[] Villages = { };            // x, z, half-extent, houses
            public Vector4[] FishFarms = { };           // x, z, ring diameter, yaw
            public Vector4[] Ships = { };               // x, z, length, yaw
            public Vector3[] Falls = { };               // x, z of the source, width
            public PuffCluster[] Spruces = { }, Trees = { }, Boulders = { };
            public FloraPatch[] Grass = { };
            public int Gulls;
            public Vector2 GullDistance = new(60f, 300f), GullElevation = new(-20f, 5f), GullCircle = new(15f, 50f);
        }

        /// <summary>The materials every Norway view shares.</summary>
        class NorwayKit
        {
            public Shader Land, Water, Fall;
            public Material Props, Trees, Grass, Boulders, Tarn, Falls, Gull;
        }

        static IEnumerable<SimWorld> BuildNorway(Transform outside, Simulation simulation, Palette p)
        {
            var kit = NorwayMaterials(p);
            if (kit == null)
                yield break;

            foreach (var view in new[]
                     {
                         Lustrafjord(), HilltopFjord(), ArcticBay(), GlacierPeak(), Olden(),
                         BirdCliffs(), StormGorge(), Dronningruta(), Senja(),
                     })
                yield return BuildView(outside, simulation, view, kit);
        }

        static NorwayKit NorwayMaterials(Palette p)
        {
            var kit = new NorwayKit
            {
                Land = LoadShader(k_Shaders, "SimLand.shader"),
                Water = LoadShader(k_Shaders, "SimWater.shader"),
                Fall = LoadShader(k_Shaders, "SimFall.shader"),
                Gull = p.Gull,
            };
            if (kit.Land == null || kit.Water == null || kit.Fall == null)
            {
                Debug.LogError($"[{k_Tag}] Missing SimLand, SimWater or SimFall in '{k_Shaders}'; the Norway worlds are left out.");
                return null;
            }

            kit.Props = ShaderMaterial("Norway_Props", kit.Land, m =>
            {
                m.SetFloat("_Strata", 0f);
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Detail", 0f);
                m.SetFloat("_CelSoft", 0.15f);
                m.SetFloat("_Rim", 0.2f);
                m.SetColor("_GlowColor", new Color(1.4f, 1.1f, 0.7f));
                m.SetFloat("_BandGlow", 1.6f);
                m.SetFloat("_Rings", 0f);
                m.SetFloat("_CloudShade", 1f);
            });
            kit.Trees = ShaderMaterial("Norway_Trees", kit.Land, m =>
            {
                m.SetFloat("_Strata", 0f);
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Detail", 0.15f);
                m.SetFloat("_DetailScale", 2.5f);
                m.SetFloat("_CelSoft", 0.2f);
                m.SetFloat("_Rim", 0.3f);
                m.SetFloat("_BandGlow", 0f);
                m.SetFloat("_Rings", 0f);
                m.SetFloat("_CloudShade", 1f);
            });
            kit.Boulders = ShaderMaterial("Norway_Boulders", kit.Land, m =>
            {
                m.SetFloat("_Strata", 0f);
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Detail", 0.35f);
                m.SetFloat("_DetailScale", 1.2f);
                m.SetFloat("_CelSoft", 0.18f);
                m.SetFloat("_Rim", 0.25f);
                m.SetFloat("_BandGlow", 0f);
                m.SetFloat("_Rings", 0f);
                m.SetFloat("_CloudShade", 1f);
            });
            kit.Grass = Flora("Norway_Grass", kit.Land, -1, new Color(0.46f, 0.62f, 0.26f), 0.22f, 0, 0.3f, m =>
            {
                m.SetVector("_SwayWind", new Vector4(0.6f, 0f, -0.8f, 0f));
                m.SetFloat("_Detail", 0.2f);
                m.SetFloat("_DetailScale", 1.5f);
                m.SetFloat("_CloudShade", 1f);
            });
            kit.Tarn = ShaderMaterial("Norway_Tarn", kit.Water, m =>
            {
                m.SetColor("_DeepColor", new Color(0.03f, 0.05f, 0.07f));
                m.SetFloat("_Reflectivity", 0.85f);
                m.SetFloat("_Ripple", 0.25f);
                m.SetFloat("_RippleScale", 4f);
                m.SetFloat("_Glint", 1.2f);
                m.SetFloat("_Rings", 0.5f);
                m.SetFloat("_RingRipple", 1.2f);
            });
            kit.Falls = ShaderMaterial("Norway_Waterfall", kit.Fall, _ => { });
            return kit;
        }

        /// <summary>One Norway view, from its numbers: the ground, the water, what stands in it, and its sky.</summary>
        static SimWorld BuildView(Transform outside, Simulation simulation, NorwayView v, NorwayKit kit)
        {
            var world = NewChild(outside, $"World_{v.Name}");

            var landMaterial = ShaderMaterial($"Norway_{v.Name}_Land", kit.Land, m =>
            {
                m.SetFloat("_Strata", 0f);
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Biome", 1f);
                m.SetFloat("_WaterLevel", v.Water);
                m.SetColor("_ShoreColor", v.HasWater ? v.Shore : Color.clear);
                m.SetFloat("_ShoreWidth", v.ShoreHeight);
                m.SetColor("_FieldColor", v.Fields);
                m.SetFloat("_FieldLine", v.FieldLine);
                m.SetColor("_ForestColor", v.Forest);
                m.SetFloat("_TreeLine", v.TreeLine);
                m.SetColor("_HeathColor", v.Heath);
                m.SetColor("_RockColor", v.Rock);
                m.SetFloat("_RockPatches", v.RockPatches);
                m.SetFloat("_Surf", v.HasWater ? v.Surf : 0f);
                m.SetFloat("_SurfHeight", v.SurfHeight);
                m.SetFloat("_CloudShade", v.Cover > 0f ? 1f : 0f);
                m.SetFloat("_Snow", v.Snow);
                m.SetFloat("_SnowLine", v.SnowLine);
                m.SetColor("_SnowColor", v.SnowColour);
                m.SetFloat("_SnowGlint", v.SnowGlint);
                m.SetFloat("_Detail", 0.25f);
                m.SetFloat("_DetailScale", 30f);
                m.SetFloat("_CelSoft", 0.22f);
                m.SetFloat("_Rim", 0.2f);
                m.SetFloat("_BandGlow", 0f);
                m.SetFloat("_StrataGlow", 0f);
                m.SetFloat("_Rings", 0f);
            });

            // The ground, and the tarns' water on it.
            var landObject = MeshObject(world, "Land", Vector3.zero, Quaternion.identity, landMaterial);
            var tarns = MeshObject(landObject.transform, "Tarns", Vector3.zero, Quaternion.identity, kit.Tarn);
            Quiet(tarns.GetComponent<Renderer>());
            var land = Undo.AddComponent<SimSurface>(landObject);
            var so = new SerializedObject(land);
            SetEnum(k_Tag, so, "m_Profile", (int)SurfaceProfile.Fjordland);
            SetFloat(k_Tag, so, "m_InnerRadius", 2f);
            SetFloat(k_Tag, so, "m_OuterRadius", 8200f);
            SetInt(k_Tag, so, "m_Rings", 240);
            SetInt(k_Tag, so, "m_Segments", 460);
            SetInt(k_Tag, so, "m_Seed", v.Seed);
            SetFloat(k_Tag, so, "m_Base", v.Base);
            SetFloat(k_Tag, so, "m_Rim", v.Rim);
            SetFloat(k_Tag, so, "m_RimWidth", v.Ridges);
            SetFloat(k_Tag, so, "m_LevelAround", v.Level);
            SetValleys(so, v.Valleys);
            SetVector4s(so, "m_Peaks", v.Peaks);
            SetVector4s(so, "m_Tarns", v.Tarns);
            SetRef(k_Tag, so, "m_TarnWater", tarns.GetComponent<MeshFilter>());
            SetVector4(so, "m_Coast", v.Coast);
            SetVector4(so, "m_Bay", v.Bay);
            SetFloat(k_Tag, so, "m_SeaFloor", v.SeaFloor);
            SetFloat(k_Tag, so, "m_Plateau", v.Plateau);
            SetFloat(k_Tag, so, "m_Pad", -0.05f - v.Hover);
            SetVector2(so, "m_Summit", v.Summit);
            SetFloat(k_Tag, so, "m_ViewRadius", v.ViewRadius);
            SetFloat(k_Tag, so, "m_ViewSlope", v.ViewSlope);
            SetVector2(so, "m_Horizon", v.Horizon);
            so.ApplyModifiedProperties();

            // The fjord, the lake or the sea. Where it reaches the open sea it curves up toward the
            // horizon in the far distance, so from a summit the sea runs all the way out.
            if (v.HasWater)
            {
                var waterMaterial = ShaderMaterial($"Norway_{v.Name}_Water", kit.Water, m =>
                {
                    m.SetColor("_DeepColor", v.WaterColour);
                    m.SetFloat("_Reflectivity", v.Reflectivity);
                    m.SetFloat("_Ripple", 0.3f);
                    m.SetFloat("_RippleScale", 9f);
                    m.SetFloat("_Glint", 1.4f);
                    m.SetFloat("_Rings", v.Rings ? 0.6f : 0f);
                    m.SetFloat("_RingRipple", v.Rings ? 1f : 0f);
                });
                var waterObject = MeshObject(world, "Water", new Vector3(0f, v.Water, 0f), Quaternion.identity, waterMaterial);
                Quiet(waterObject.GetComponent<Renderer>());
                var water = Undo.AddComponent<SimSurface>(waterObject);
                so = new SerializedObject(water);
                SetEnum(k_Tag, so, "m_Profile", (int)SurfaceProfile.Flat);
                SetFloat(k_Tag, so, "m_InnerRadius", 1f);
                SetFloat(k_Tag, so, "m_OuterRadius", 8200f);
                SetInt(k_Tag, so, "m_Rings", 140);
                SetInt(k_Tag, so, "m_Segments", 360);
                if (v.SeaHorizon > 0f)
                    SetVector2(so, "m_Horizon", new Vector2(v.SeaHorizon, -v.Water * 0.96f));
                so.ApplyModifiedProperties();
            }

            int seed = v.Seed * 10;
            foreach (var village in v.Villages)
            {
                var go = MeshObject(world, "Village", Vector3.zero, Quaternion.identity, kit.Props);
                so = new SerializedObject(Undo.AddComponent<SimProps>(go));
                SetEnum(k_Tag, so, "m_Kind", (int)PropKind.Houses);
                SetVector3(k_Tag, so, "m_Centre", new Vector3(village.x, 0f, village.y));
                SetVector2(so, "m_Spread", new Vector2(village.z, village.z * 0.6f));
                SetInt(k_Tag, so, "m_Count", (int)village.w);
                SetFloat(k_Tag, so, "m_Size", 10f);
                SetFloat(k_Tag, so, "m_WaterLevel", v.HasWater ? v.Water : float.MinValue);
                SetInt(k_Tag, so, "m_Seed", ++seed);
                SetRef(k_Tag, so, "m_Ground", land);
                so.ApplyModifiedProperties();
            }

            foreach (var farm in v.FishFarms)
            {
                var go = MeshObject(world, "Fish_Farm", Vector3.zero, Quaternion.identity, kit.Props);
                so = new SerializedObject(Undo.AddComponent<SimProps>(go));
                SetEnum(k_Tag, so, "m_Kind", (int)PropKind.FishFarm);
                SetVector3(k_Tag, so, "m_Centre", new Vector3(farm.x, v.Water + 0.3f, farm.y));
                SetVector2(so, "m_Spread", new Vector2(2f, 4f));
                SetFloat(k_Tag, so, "m_Size", farm.z);
                SetFloat(k_Tag, so, "m_Yaw", farm.w);
                so.ApplyModifiedProperties();
            }

            foreach (var ship in v.Ships)
            {
                var go = MeshObject(world, "Ship", Vector3.zero, Quaternion.identity, kit.Props);
                so = new SerializedObject(Undo.AddComponent<SimProps>(go));
                SetEnum(k_Tag, so, "m_Kind", (int)PropKind.Ship);
                SetVector3(k_Tag, so, "m_Centre", new Vector3(ship.x, v.Water, ship.y));
                SetFloat(k_Tag, so, "m_Size", ship.z);
                SetFloat(k_Tag, so, "m_Yaw", ship.w);
                so.ApplyModifiedProperties();
            }

            foreach (var source in v.Falls)
            {
                var go = MeshObject(world, "Waterfall", Vector3.zero, Quaternion.identity, kit.Falls);
                Quiet(go.GetComponent<Renderer>());
                so = new SerializedObject(Undo.AddComponent<SimWaterfall>(go));
                SetRef(k_Tag, so, "m_Ground", land);
                SetVector2(so, "m_Source", new Vector2(source.x, source.y));
                SetFloat(k_Tag, so, "m_Width", source.z);
                SetFloat(k_Tag, so, "m_StopAt", v.HasWater ? v.Water + 1f : float.MinValue);
                so.ApplyModifiedProperties();
            }

            Puffs(world, "Spruces", kit.Trees, PuffKind.Spruces, v.Spruces, land, ++seed);
            Puffs(world, "Trees", kit.Trees, PuffKind.Trees, v.Trees, land, ++seed);
            Puffs(world, "Boulders", kit.Boulders, PuffKind.Boulders, v.Boulders, land, ++seed);
            if (v.Grass.Length > 0)
                Grow(world, "Grass", kit.Grass, FloraKind.Grass, land, ++seed, v.Grass);

            if (v.Gulls > 0)
            {
                var birds = MeshObject(world, "Birds", Vector3.zero, Quaternion.identity, kit.Gull);
                so = new SerializedObject(Undo.AddComponent<SimGulls>(birds));
                SetRef(k_Tag, so, "m_Simulation", simulation);
                SetInt(k_Tag, so, "m_Count", v.Gulls);
                SetVector2(so, "m_Distance", v.GullDistance);
                SetVector2(so, "m_Elevation", v.GullElevation);
                SetVector2(so, "m_Circle", v.GullCircle);
                SetFloat(k_Tag, so, "m_Spread", 120f);
                SetFloat(k_Tag, so, "m_Speed", 9f);
                SetFloat(k_Tag, so, "m_Span", 1.3f);
                SetFloat(k_Tag, so, "m_Climb", 6f);
                so.ApplyModifiedProperties();
            }

            if (v.Lightning)
            {
                so = new SerializedObject(Undo.AddComponent<SimLightning>(world.gameObject));
                SetRef(k_Tag, so, "m_Simulation", simulation);
                so.ApplyModifiedProperties();
            }

            Transform rings = null;
            if (v.Rings)
            {
                rings = NewChild(world, "Ring_Origin");
                rings.localPosition = v.RingOrigin;
            }

            var sim = Undo.AddComponent<SimWorld>(world.gameObject);
            so = new SerializedObject(sim);
            SetString(k_Tag, so, "m_Title", v.Title);
            SetString(k_Tag, so, "m_Group", "Norway");
            SetColor(k_Tag, so, "m_Zenith", v.Zenith);
            SetColor(k_Tag, so, "m_Horizon", v.SkyHorizon);
            SetColor(k_Tag, so, "m_Ground", v.Below);
            SetVector3(k_Tag, so, "m_SunDirection", v.Sun);
            SetColor(k_Tag, so, "m_SunColour", v.SunColour);
            SetFloat(k_Tag, so, "m_SunDegrees", 1.1f);
            SetColor(k_Tag, so, "m_ShadowColour", v.Shadow);
            SetFloat(k_Tag, so, "m_Clouds", v.Cirrus);
            SetColor(k_Tag, so, "m_CloudColour", v.CloudColour);
            SetFloat(k_Tag, so, "m_CloudCover", v.Cover);
            SetFloat(k_Tag, so, "m_CloudDarkness", v.Darkness);
            SetVector2(so, "m_CloudSize", v.CloudSize);
            SetVector2(so, "m_CloudWind", v.Wind);
            SetFloat(k_Tag, so, "m_CloudsOpen", v.Open);
            SetFloat(k_Tag, so, "m_CloudShadows", v.CloudShadows);
            SetVector2(so, "m_MistBand", v.Mist);
            SetFloat(k_Tag, so, "m_MistDensity", v.MistDensity);
            SetFloat(k_Tag, so, "m_Stars", 0f);
            SetFloat(k_Tag, so, "m_FogDistance", v.FogDistance);
            SetFloat(k_Tag, so, "m_FogAmount", v.FogAmount);
            SetFloat(k_Tag, so, "m_FogSunGlow", v.FogSunGlow);
            SetRef(k_Tag, so, "m_RingOrigin", rings);
            SetColor(k_Tag, so, "m_RingColour", v.RingColour);
            SetFloat(k_Tag, so, "m_RingSpeed", v.RingSpeed);
            SetFloat(k_Tag, so, "m_RingDelay", 0.05f);
            SetColor(k_Tag, so, "m_RoomAmbient", v.RoomAmbient);
            SetColor(k_Tag, so, "m_RoomLight", v.RoomLight);
            SetFloat(k_Tag, so, "m_RoomLightIntensity", v.RoomLightIntensity);
            SetFloat(k_Tag, so, "m_LampLevel", v.Lamps);
            so.ApplyModifiedProperties();
            return sim;
        }

        static void Puffs(Transform world, string name, Material material, PuffKind kind, PuffCluster[] clusters, SimSurface ground, int seed)
        {
            if (clusters.Length == 0)
                return;

            var go = MeshObject(world, name, Vector3.zero, Quaternion.identity, material);
            var so = new SerializedObject(Undo.AddComponent<SimPuffs>(go));
            SetEnum(k_Tag, so, "m_Kind", (int)kind);
            SetClusters(so, clusters);
            SetInt(k_Tag, so, "m_Seed", seed);
            SetRef(k_Tag, so, "m_Ground", ground);
            so.ApplyModifiedProperties();
        }

        /// <summary>A valley or fjord: half its width, the share of that which is floor, how steep its walls are, and its path.</summary>
        static Valley Cut(float width, float flat, float steep, params Vector3[] path) =>
            new() { Path = path, Width = width, Flat = flat, Steep = steep };

        /// <summary>A point on a valley's path: across, the floor's height there, ahead.</summary>
        static Vector3 P(float x, float floor, float z) => new(x, floor, z);

        static PuffCluster Cluster(float x, float z, Vector2 spread, int count, float size, Color colour) =>
            new() { Centre = new Vector3(x, 0f, z), Spread = spread, Count = count, Size = size, Colour = colour, OnGround = true };

        static void SetValleys(SerializedObject so, Valley[] valleys)
        {
            var property = so.FindProperty("m_Valleys");
            property.arraySize = valleys.Length;
            for (int i = 0; i < valleys.Length; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                var path = element.FindPropertyRelative("Path");
                path.arraySize = valleys[i].Path.Length;
                for (int j = 0; j < valleys[i].Path.Length; j++)
                    path.GetArrayElementAtIndex(j).vector3Value = valleys[i].Path[j];
                element.FindPropertyRelative("Width").floatValue = valleys[i].Width;
                element.FindPropertyRelative("Flat").floatValue = valleys[i].Flat;
                element.FindPropertyRelative("Steep").floatValue = valleys[i].Steep;
            }
        }

        static void SetVector4s(SerializedObject so, string path, Vector4[] values)
        {
            var property = so.FindProperty(path);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).vector4Value = values[i];
        }

        // ================================================================== the views

        /// <summary>
        /// A bare summit high over the Lustrafjord, after a photograph from Molden: rock and heath with dark tarns at
        /// the room's feet, the turquoise fjord a thousand metres down, one arm curling away behind the mountain on the
        /// left and the other running off to the right past farms on a green shelf, ridge after ridge going blue into
        /// the haze and snow on the farthest.
        /// </summary>
        static NorwayView Lustrafjord() => new()
        {
            Name = "Lustrafjord", Title = "Lustrafjord", Seed = 11, Water = -1050f, Base = -900f, Rim = 1150f, Ridges = 2600f,
            Valleys = new[]
            {
                Cut(1000f, 0.3f, 1.6f,
                    P(-3600f, -1250f, 200f), P(-1300f, -1250f, 1100f), P(500f, -1250f, 2300f),
                    P(2600f, -1250f, 2700f), P(4600f, -1250f, 2100f), P(6200f, -1250f, 900f)),
                Cut(850f, 0.3f, 1.6f, P(200f, -1250f, 2200f), P(-700f, -1250f, 3900f), P(-1500f, -1250f, 6400f)),
                Cut(900f, 0.55f, 0.8f, P(1600f, -560f, 300f), P(2700f, -900f, 1500f)),
            },
            Peaks = new Vector4[] { new(1300f, 3700f, 250f, 1800f), new(-2600f, 3200f, 180f, 1500f), new(3800f, 4300f, 350f, 1900f) },
            Tarns = new Vector4[] { new(-55f, 75f, 24f, 3f), new(48f, 140f, 30f, 3f), new(-25f, 235f, 42f, 4f), new(120f, 45f, 16f, 2f) },
            Summit = new(220f, 40f), Horizon = new(4500f, 1300f), ViewRadius = 2600f, ViewSlope = -0.8f, Hover = 120f,
            Heath = new(0.44f, 0.48f, 0.26f), Rock = new(0.52f, 0.52f, 0.5f), TreeLine = 650f, FieldLine = 250f, SnowLine = 420f,
            RockPatches = 1.1f, Zenith = new(0.36f, 0.58f, 0.86f), SkyHorizon = new(0.8f, 0.87f, 0.93f), Below = new(0.5f, 0.62f, 0.7f),
            Sun = new(-0.45f, 0.72f, 0.5f), Cirrus = 0.2f, Cover = 0.12f, CloudSize = new(1400f, 2600f), FogDistance = 3800f,
            FogAmount = 0.8f, FogSunGlow = 0.35f, RingOrigin = new(500f, -1050f, 2300f), RingSpeed = 220f,
            Villages = new Vector4[] { new(-1750f, 2550f, 260f, 26f), new(2350f, 1150f, 380f, 30f) },
            Falls = new Vector3[] { new(1150f, 3250f, 22f), new(-2200f, 2300f, 18f) },
            Boulders = new[]
            {
                Cluster(30f, 60f, new(90f, 120f), 50, 1.6f, new(0.55f, 0.55f, 0.55f)),
                Cluster(-90f, 160f, new(60f, 90f), 30, 1.2f, new(0.55f, 0.55f, 0.55f)),
            },
            Grass = new[] { Patch(new(0f, 0f, 40f), new(60f, 50f), 300, new(0.25f, 0.5f)) },
        };

        /// <summary>
        /// A grassy knoll above a fjord on a bright day of heaped cloud: the green falling away under the room, a lone
        /// birch or two, the turquoise water below running out from the left past a pyramid of a mountain on the right,
        /// and an arm of it reaching away between the mountains into the blue distance.
        /// </summary>
        static NorwayView HilltopFjord() => new()
        {
            Name = "Hilltop_Fjord", Title = "Hilltop Fjord", Seed = 23, Water = -560f, Base = -650f, Rim = 1150f, Ridges = 2200f,
            Valleys = new[]
            {
                Cut(950f, 0.4f, 1.6f,
                    P(-4200f, -720f, 2000f), P(-1300f, -720f, 1250f), P(900f, -720f, 1050f),
                    P(3600f, -720f, 1700f), P(6400f, -720f, 3000f)),
                Cut(750f, 0.35f, 1.6f, P(-500f, -720f, 1300f), P(-1400f, -720f, 4500f), P(-900f, -720f, 7000f)),
            },
            Peaks = new Vector4[] { new(2100f, 2900f, 760f, 1800f), new(-2700f, 650f, 330f, 1500f) },
            Summit = new(100f, 15f), Horizon = new(4200f, 900f), ViewRadius = 1400f, ViewSlope = -0.6f, Hover = 35f,
            Heath = new(0.36f, 0.56f, 0.2f), TreeLine = 420f, FieldLine = 80f, RockPatches = 0.4f, Zenith = new(0.22f, 0.48f, 0.9f),
            SkyHorizon = new(0.72f, 0.84f, 0.96f), Sun = new(0.35f, 0.75f, 0.35f), Cirrus = 0.1f, Cover = 0.45f, Open = 0.5f,
            CloudSize = new(1500f, 2200f), FogDistance = 5000f, FogAmount = 0.7f, RingOrigin = new(-200f, -560f, 1200f),
            Spruces = new[] { Cluster(-600f, 450f, new(300f, 150f), 60, 14f, new(0.16f, 0.3f, 0.17f)) },
            Trees = new[]
            {
                Cluster(-70f, 40f, new(12f, 10f), 2, 3.2f, new(0.32f, 0.5f, 0.2f)),
                Cluster(85f, 70f, new(14f, 10f), 2, 2.8f, new(0.32f, 0.5f, 0.2f)),
            },
            Grass = new[]
            {
                Patch(new(0f, 0f, 30f), new(70f, 50f), 900, new(0.3f, 0.6f)),
                Patch(new(-50f, 0f, 10f), new(30f, 20f), 200, new(0.3f, 0.55f)),
            },
        };

        /// <summary>
        /// High on a green mountainside in the far north, after a photograph from Andøya: a valley leads down from the
        /// room to a bay with a beach of white sand curving round it, cliffs on the headland to the left, the open
        /// Arctic Ocean out to the right with the sun on it, and a bank of low cloud along the horizon.
        /// </summary>
        static NorwayView ArcticBay() => new()
        {
            Name = "Arctic_Bay", Title = "Arctic Bay", Seed = 31, Water = -480f, Base = -520f, Rim = 760f, Ridges = 1700f,
            SeaFloor = -560f,
            Valleys = new[] { Cut(620f, 0.5f, 1.2f, P(0f, -380f, 600f), P(100f, -462f, 1150f)) },
            Peaks = new Vector4[] { new(-950f, 1800f, -80f, 750f), new(-1400f, 1100f, 60f, 900f) },
            Coast = new(0f, 1900f, 35f, 500f), Bay = new(150f, 1700f, 650f, 260f), Summit = new(150f, 30f), Horizon = new(4000f, 600f),
            SeaHorizon = 3000f, ViewRadius = 1200f, ViewSlope = -0.5f, Hover = 100f, Heath = new(0.42f, 0.58f, 0.22f),
            Shore = new(0.92f, 0.9f, 0.82f, 1f), TreeLine = -200f, FieldLine = 40f, ShoreHeight = 7f, RockPatches = 0.8f, Surf = 1f,
            SurfHeight = 1.8f, WaterColour = new(0.05f, 0.2f, 0.36f), Reflectivity = 0.6f, Zenith = new(0.42f, 0.58f, 0.82f),
            SkyHorizon = new(0.84f, 0.88f, 0.92f), Below = new(0.45f, 0.58f, 0.7f), Sun = new(-0.25f, 0.45f, 0.86f), Cirrus = 0.3f,
            Cover = 0.5f, Darkness = 0.15f, Open = 0.5f, CloudSize = new(2400f, 1800f), FogDistance = 6000f, FogAmount = 0.65f,
            FogSunGlow = 0.5f, RingOrigin = new(150f, -480f, 1750f),
            Villages = new Vector4[] { new(-230f, 1150f, 60f, 3f) },
            Trees = new[] { Cluster(-280f, 1200f, new(40f, 25f), 12, 6f, new(0.16f, 0.3f, 0.15f)) },
            Grass = new[] { Patch(new(0f, 0f, 30f), new(60f, 40f), 500, new(0.25f, 0.5f)) },
            Gulls = 14, GullDistance = new(80f, 400f), GullElevation = new(-25f, -4f),
        };

        /// <summary>
        /// A summit ridge over a glacier, after a photograph from the mountains of the north: the snowfield right below
        /// the room between a wall of rock on the left and a great grey fin on the right, and far beneath them a long
        /// blue lake in a green valley, with ranges of grey peaks streaked with snow beyond.
        /// </summary>
        static NorwayView GlacierPeak() => new()
        {
            Name = "Glacier_Peak", Title = "Glacier Peak", Seed = 41, Water = -1300f, Base = -1300f, Rim = 1550f, Ridges = 2600f,
            Valleys = new[]
            {
                Cut(420f, 0.5f, 1.5f, P(0f, -110f, 90f), P(60f, -200f, 350f), P(130f, -700f, 800f), P(200f, -1250f, 1250f)),
                Cut(1300f, 0.4f, 1.6f,
                    P(-4200f, -1260f, 1500f), P(-1500f, -1340f, 1700f), P(1200f, -1340f, 2000f),
                    P(4200f, -1260f, 2800f)),
            },
            Peaks = new Vector4[]
            {
                new(330f, 400f, 180f, 220f),
                new(-430f, 320f, 90f, 220f),
                new(-1600f, 5200f, 750f, 1800f),
                new(2500f, 5800f, 950f, 2000f),
                new(600f, 4300f, 300f, 1500f),
            },
            Summit = new(70f, 35f), Horizon = new(4500f, 1100f), ViewRadius = 1600f, ViewSlope = -0.35f, Hover = 60f,
            Heath = new(0.36f, 0.5f, 0.26f), Rock = new(0.5f, 0.5f, 0.5f), SnowColour = new(0.94f, 0.96f, 1f), TreeLine = -850f,
            FieldLine = -1250f, SnowLine = -250f, Snow = 0.9f, SnowGlint = 2f, RockPatches = 1f, WaterColour = new(0.12f, 0.34f, 0.45f),
            Reflectivity = 0.55f, Zenith = new(0.22f, 0.45f, 0.85f), SkyHorizon = new(0.72f, 0.82f, 0.92f), Sun = new(0.4f, 0.75f, 0.3f),
            Cirrus = 0.05f, Cover = 0.22f, CloudSize = new(1200f, 2600f), FogDistance = 6000f, FogAmount = 0.65f,
            RingOrigin = new(0f, -1300f, 1850f),
            Villages = new Vector4[] { new(1900f, 2100f, 200f, 12f) },
            Falls = new Vector3[] { new(-700f, 1250f, 16f) },
            Boulders = new[] { Cluster(0f, 30f, new(40f, 40f), 40, 1.4f, new(0.5f, 0.48f, 0.46f)) },
        };

        /// <summary>
        /// Above the head of a fjord at Olden: a cruise ship at anchor below, the village where the fjord ends, a
        /// valley of fields and farms running on between sheer green walls to a lake, waterfalls down the cliffs, and
        /// snow on the peaks above.
        /// </summary>
        static NorwayView Olden() => new()
        {
            Name = "Olden", Title = "Olden", Seed = 53, Water = -300f, Base = -320f, Rim = 1500f, Ridges = 2300f,
            Valleys = new[]
            {
                Cut(1300f, 0.36f, 2.2f,
                    P(-2400f, -360f, -1100f), P(-500f, -360f, 600f), P(-100f, -296f, 1500f),
                    P(250f, -272f, 3300f), P(450f, -266f, 5100f), P(600f, -262f, 6800f)),
            },
            Peaks = new Vector4[] { new(-2300f, 2600f, 1300f, 1700f), new(-900f, 5600f, 1200f, 1500f), new(1900f, 4800f, 1100f, 1600f) },
            Tarns = new Vector4[] { new(470f, 4750f, 520f, 12f) },
            Horizon = new(5000f, 900f), ViewRadius = 900f, ViewSlope = -0.35f, Hover = 40f, Forest = new(0.14f, 0.32f, 0.14f),
            Heath = new(0.38f, 0.52f, 0.22f), TreeLine = 800f, FieldLine = 140f, SnowLine = 700f, RockPatches = 0.5f,
            WaterColour = new(0.14f, 0.42f, 0.48f), Reflectivity = 0.6f, Zenith = new(0.28f, 0.55f, 0.9f),
            SkyHorizon = new(0.78f, 0.87f, 0.95f), Sun = new(0.55f, 0.7f, 0.3f), Cirrus = 0.1f, Cover = 0.35f,
            CloudSize = new(1300f, 2000f), FogDistance = 5000f, FogAmount = 0.7f, RingOrigin = new(-700f, -300f, 450f),
            Villages = new Vector4[] { new(-100f, 1650f, 300f, 60f), new(250f, 3000f, 300f, 30f), new(420f, 4000f, 250f, 14f) },
            Ships = new Vector4[] { new(-900f, 250f, 230f, 42f) },
            Falls = new Vector3[] { new(-1150f, 2200f, 20f), new(-900f, 3600f, 16f), new(1150f, 2600f, 18f) },
            Spruces = new[] { Cluster(150f, 150f, new(120f, 120f), 50, 16f, new(0.16f, 0.3f, 0.17f)) },
        };

        /// <summary>
        /// The grassy edge of a sea cliff four hundred metres up, after a photograph from Bleik: the sea straight down
        /// with the surf white along the rocks, a ridge of jagged peaks running out into it ahead, a lone stack
        /// offshore, open ocean to the horizon, and gulls wheeling below the edge.
        /// </summary>
        static NorwayView BirdCliffs() => new()
        {
            Name = "Bird_Cliffs", Title = "Bird Cliffs", Seed = 61, Water = -420f, Base = -200f, Rim = 520f, Ridges = 1200f,
            SeaFloor = -480f,
            Peaks = new Vector4[] { new(420f, 720f, -90f, 480f), new(560f, 980f, -170f, 320f), new(-220f, 3200f, -330f, 170f) },
            Coast = new(0f, 3f, -15f, 30f), Horizon = new(3000f, 300f), SeaHorizon = 2500f, ViewRadius = 350f, ViewSlope = 0.05f,
            Level = 1.2f, Hover = 2f, Heath = new(0.42f, 0.6f, 0.22f), Rock = new(0.5f, 0.5f, 0.47f), TreeLine = -1000f, ShoreHeight = 5f,
            RockPatches = 1.2f, Surf = 1f, SurfHeight = 3f, WaterColour = new(0.05f, 0.22f, 0.38f), Reflectivity = 0.55f,
            Zenith = new(0.45f, 0.58f, 0.78f), SkyHorizon = new(0.85f, 0.88f, 0.92f), Below = new(0.45f, 0.58f, 0.7f),
            Sun = new(0.3f, 0.55f, 0.78f), Cirrus = 0.3f, Cover = 0.68f, Open = 0.5f, CloudSize = new(2500f, 1500f), FogDistance = 7000f,
            FogAmount = 0.6f, RingOrigin = new(200f, -420f, 600f),
            Boulders = new[] { Cluster(-40f, 40f, new(25f, 20f), 14, 1.8f, new(0.5f, 0.5f, 0.46f)) },
            Grass = new[] { Patch(new(0f, 0f, 20f), new(60f, 30f), 1200, new(0.35f, 0.7f)) },
            Gulls = 24, GullDistance = new(40f, 420f), GullElevation = new(-30f, 6f), GullCircle = new(15f, 60f),
        };

        /// <summary>
        /// A meadow on a valley floor under a storm: a row of trees across the fields, and beyond them black walls of
        /// cliff six hundred metres high split by a narrow gorge, with snow lying in its gullies, cloud pouring over
        /// the cliff tops, and a warm light coming through the gap. Lightning flickers in the cloud on the strongest
        /// beats.
        /// </summary>
        static NorwayView StormGorge() => new()
        {
            Name = "Storm_Gorge", Title = "Storm Gorge", Seed = 71, HasWater = false, Water = -50f, Base = 320f, Rim = 900f,
            Ridges = 1700f, Plateau = 650f,
            Valleys = new[]
            {
                Cut(2300f, 0.5f, 2.5f, P(-6000f, 0f, -400f), P(0f, 0f, 0f), P(6000f, 0f, 400f)),
                Cut(190f, 0.05f, 4f, P(-150f, 60f, 1100f), P(-250f, 250f, 2100f), P(-300f, 450f, 3600f)),
            },
            ViewRadius = 0f, Forest = new(0.1f, 0.22f, 0.1f), Heath = new(0.28f, 0.44f, 0.17f), Rock = new(0.26f, 0.27f, 0.26f),
            Fields = new(0.4f, 0.55f, 0.2f, 1f), TreeLine = 300f, FieldLine = 120f, SnowLine = 230f, Snow = 0.6f, RockPatches = 0.8f,
            Zenith = new(0.16f, 0.16f, 0.18f), SkyHorizon = new(0.72f, 0.6f, 0.5f), Below = new(0.25f, 0.25f, 0.25f),
            Sun = new(-0.1f, 0.16f, 0.98f), SunColour = new(1.2f, 0.95f, 0.7f), Shadow = new(0.22f, 0.24f, 0.3f),
            CloudColour = new(0.56f, 0.52f, 0.5f), Cirrus = 0f, Cover = 0.9f, Darkness = 0.85f, Open = 0.35f, CloudShadows = 0.8f,
            CloudSize = new(1400f, 900f), Wind = new(9f, 2f), FogDistance = 4000f, FogAmount = 0.6f, FogSunGlow = 0.6f,
            Mist = new(520f, 820f), MistDensity = 6f, RoomAmbient = new(0.3f, 0.3f, 0.33f), RoomLight = new(0.9f, 0.75f, 0.6f),
            RoomLightIntensity = 0.6f, Lamps = 0.9f, Rings = false, Lightning = true,
            Falls = new Vector3[] { new(-300f, 3100f, 16f), new(1100f, 1900f, 14f) },
            Spruces = new[] { Cluster(300f, 170f, new(400f, 40f), 30, 16f, new(0.12f, 0.24f, 0.12f)) },
            Trees = new[]
            {
                Cluster(0f, 140f, new(650f, 30f), 70, 6f, new(0.17f, 0.32f, 0.14f)),
                Cluster(-420f, 80f, new(110f, 30f), 10, 5f, new(0.2f, 0.36f, 0.15f)),
            },
            Grass = new[] { Patch(new(0f, 0f, 40f), new(90f, 60f), 1400, new(0.3f, 0.6f)) },
        };

        /// <summary>
        /// Looking down into a green bowl of the Vesterålen mountains from the Queen's Route: two dark tarns in the
        /// bottom of it among slabs of rock, heath all round, a row of black jagged peaks behind on the left, and a
        /// glimpse of the sea far off on the right under a heavy sky with the light low.
        /// </summary>
        static NorwayView Dronningruta() => new()
        {
            Name = "Dronningruta", Title = "Dronningruta", Seed = 83, Water = -400f, Base = -360f, Rim = 820f, Ridges = 1600f,
            SeaFloor = -460f,
            Valleys = new[] { Cut(1400f, 0.3f, 1f, P(0f, -280f, 820f)) },
            Peaks = new Vector4[]
            {
                new(-1400f, 2600f, 360f, 900f),
                new(-650f, 2900f, 310f, 700f),
                new(-1050f, 2350f, 250f, 600f),
                new(1600f, 2700f, 120f, 1500f),
            },
            Tarns = new Vector4[] { new(-360f, 640f, 260f, 15f), new(300f, 920f, 380f, 20f) },
            Coast = new(5200f, 3000f, 70f, 700f), Summit = new(100f, 20f), Horizon = new(3600f, 500f), ViewRadius = 900f,
            ViewSlope = -0.4f, Hover = 60f, Heath = new(0.36f, 0.5f, 0.2f), Rock = new(0.36f, 0.36f, 0.36f), TreeLine = -1000f,
            RockPatches = 1f, WaterColour = new(0.08f, 0.16f, 0.24f), Reflectivity = 0.6f, Zenith = new(0.4f, 0.45f, 0.55f),
            SkyHorizon = new(0.86f, 0.8f, 0.72f), Below = new(0.4f, 0.45f, 0.5f), Sun = new(-0.8f, 0.25f, 0.3f),
            SunColour = new(1.1f, 0.95f, 0.8f), Shadow = new(0.32f, 0.36f, 0.48f), CloudColour = new(0.82f, 0.8f, 0.8f), Cirrus = 0.2f,
            Cover = 0.75f, Darkness = 0.45f, Open = 0.4f, CloudSize = new(2200f, 2000f), FogAmount = 0.7f, FogSunGlow = 0.5f,
            RoomAmbient = new(0.45f, 0.46f, 0.5f), RoomLightIntensity = 0.85f, Lamps = 0.6f, RingOrigin = new(300f, -280f, 920f),
            Boulders = new[]
            {
                Cluster(20f, 60f, new(70f, 60f), 40, 2.2f, new(0.5f, 0.5f, 0.48f)),
                Cluster(-200f, 500f, new(200f, 200f), 60, 5f, new(0.45f, 0.45f, 0.43f)),
            },
            Grass = new[] { Patch(new(0f, 0f, 30f), new(60f, 40f), 600, new(0.25f, 0.5f)) },
        };

        /// <summary>
        /// Straight down a fjord on Senja from slabs of grey rock above it: steep green and grey walls either side, a
        /// fish farm's rings out on the still water, a village along the right shore, and at the far end a crown of
        /// jagged peaks against heaped cloud where the fjord opens to the sea.
        /// </summary>
        static NorwayView Senja() => new()
        {
            Name = "Senja", Title = "Senja", Seed = 97, Water = -260f, Base = -300f, Rim = 800f, Ridges = 1400f,
            Valleys = new[]
            {
                Cut(1500f, 0.4f, 1.8f,
                    P(0f, -330f, 1250f), P(-100f, -330f, 2500f), P(200f, -330f, 5500f),
                    P(600f, -330f, 8600f)),
            },
            Peaks = new Vector4[]
            {
                new(-1500f, 1300f, 420f, 900f),
                new(1500f, 1600f, 480f, 1000f),
                new(-900f, 5200f, 520f, 700f),
                new(-300f, 5700f, 430f, 500f),
                new(-1500f, 6100f, 600f, 800f),
                new(1400f, 6200f, 380f, 900f),
            },
            Summit = new(80f, 15f), Horizon = new(5000f, 700f), SeaHorizon = 6000f, ViewRadius = 700f, ViewSlope = -0.5f, Hover = 30f,
            Forest = new(0.15f, 0.3f, 0.14f), Heath = new(0.38f, 0.52f, 0.22f), Rock = new(0.5f, 0.5f, 0.5f), TreeLine = 250f,
            FieldLine = 40f, RockPatches = 1f, WaterColour = new(0.1f, 0.24f, 0.3f), Reflectivity = 0.7f, Zenith = new(0.3f, 0.5f, 0.82f),
            SkyHorizon = new(0.8f, 0.85f, 0.9f), Sun = new(0.6f, 0.55f, 0.3f), Cirrus = 0.1f, Cover = 0.5f, Darkness = 0.3f,
            CloudSize = new(1500f, 1800f), FogDistance = 6000f, FogAmount = 0.65f, RingOrigin = new(-60f, -260f, 1900f),
            Villages = new Vector4[] { new(680f, 3200f, 260f, 40f) },
            FishFarms = new Vector4[] { new(260f, 2300f, 40f, 10f) },
            Boulders = new[]
            {
                Cluster(0f, 50f, new(80f, 60f), 30, 3.5f, new(0.55f, 0.55f, 0.53f)),
                Cluster(-60f, 30f, new(40f, 30f), 20, 1.5f, new(0.5f, 0.5f, 0.48f)),
            },
            Grass = new[] { Patch(new(0f, 0f, 30f), new(70f, 40f), 700, new(0.3f, 0.55f)) },
        };

        // ================================================================== the world menu

        /// <summary>Height of the menu's middle on the front wall: above the desk, a little below the eye.</summary>
        const float k_MenuHeight = 1.5f;

        /// <summary>Height of the top row and the step down to each next, in the menu's own space.</summary>
        static readonly Vector2 k_MenuRows = new(0.36f, 0.082f);

        const int k_MenuRowCount = 11;

        /// <summary>
        /// The world menu, on the front wall just inside the glass: a dark glass panel, a bar behind
        /// the selected row, the title and the rows, the wand's ray and a cursor where it meets the
        /// panel. Hidden until the world button opens it.
        /// </summary>
        static WorldMenu BuildMenu(Transform root, Simulation simulation)
        {
            var front = CaveScreens.Front;
            var holder = NewChild(root, "World_Menu");
            holder.localPosition = front.Point(0.5f, k_MenuHeight / Height, k_Inset + 0.03f);
            holder.localRotation = front.FacingRotation;

            var content = NewChild(holder, "Content");
            var halfSize = new Vector2(0.75f, 0.56f);
            var panel = Quad(content, "Panel", Vector3.zero, halfSize * 2f, MenuMaterial("Menu_Panel", new Color(0.04f, 0.06f, 0.1f, 0.78f), 2980));
            Quiet(panel.GetComponent<Renderer>());
            var edge = Quad(content, "Edge", new Vector3(0f, halfSize.y - 0.004f, -0.001f), new Vector2(halfSize.x * 2f, 0.008f),
                MenuMaterial("Menu_Edge", new Color(0.75f, 0.9f, 1f, 0.9f), 2985));
            Quiet(edge.GetComponent<Renderer>());
            var highlight = Quad(content, "Highlight", new Vector3(0f, k_MenuRows.x, -0.002f), new Vector2(halfSize.x * 1.8f, k_MenuRows.y * 0.92f),
                MenuMaterial("Menu_Highlight", new Color(0.45f, 0.7f, 1f, 0.35f), 2990));
            Quiet(highlight.GetComponent<Renderer>());

            var title = Text(k_Tag, content, "Title", new Vector3(0f, 0.475f, -0.004f), 0.7f, "WORLDS", new Color(0.75f, 0.9f, 1f),
                new Vector2(halfSize.x * 1.8f, 0.1f));
            var rows = new List<TMPro.TMP_Text>();
            for (int i = 0; i < k_MenuRowCount; i++)
                rows.Add(Text(k_Tag, content, $"Row_{i}", new Vector3(0f, k_MenuRows.x - i * k_MenuRows.y, -0.004f), 0.52f, "",
                    new Color(0.86f, 0.9f, 0.96f), new Vector2(halfSize.x * 1.8f, k_MenuRows.y)));

            var cursor = Quad(content, "Cursor", new Vector3(0f, 0f, -0.006f), Vector2.one * 0.025f,
                MenuMaterial("Menu_Cursor", new Color(0.9f, 0.97f, 1f, 0.95f), 3010));
            Quiet(cursor.GetComponent<Renderer>());

            var rayObject = NewChild(holder, "Wand_Ray");
            var ray = Undo.AddComponent<LineRenderer>(rayObject.gameObject);
            ray.useWorldSpace = true;
            ray.positionCount = 2;
            ray.widthMultiplier = 0.005f;
            ray.numCapVertices = 2;
            ray.shadowCastingMode = ShadowCastingMode.Off;
            ray.receiveShadows = false;
            ray.sharedMaterial = MenuMaterial("Menu_Ray", new Color(0.7f, 0.9f, 1f, 0.55f), 3005);
            ray.enabled = false;

            var menu = Undo.AddComponent<WorldMenu>(holder.gameObject);
            var so = new SerializedObject(menu);
            SetRef(k_Tag, so, "m_Simulation", simulation);
            SetRef(k_Tag, so, "m_Content", content);
            SetRef(k_Tag, so, "m_Highlight", highlight);
            SetRef(k_Tag, so, "m_Title", title);
            SetArray(k_Tag, so, "m_Rows", rows);
            SetVector2(so, "m_RowLayout", k_MenuRows);
            SetVector2(so, "m_HalfSize", halfSize);
            SetRef(k_Tag, so, "m_Ray", ray);
            SetRef(k_Tag, so, "m_Cursor", cursor);
            so.ApplyModifiedProperties();

            // Closed until the world button opens it, in the editor too: it would cover the front window.
            content.gameObject.SetActive(false);
            cursor.gameObject.SetActive(false);
            return menu;
        }

        /// <summary>URP Unlit, transparent, drawn at <paramref name="queue"/> so the panel, bar, text and cursor stack in order.</summary>
        static Material MenuMaterial(string name, Color colour, int queue)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            return ShaderMaterial(name, shader, m =>
            {
                m.SetColor("_BaseColor", colour);
                m.SetTexture("_BaseMap", Texture2D.whiteTexture);
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_Cull", (float)CullMode.Off);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = queue;
            });
        }
    }
}
