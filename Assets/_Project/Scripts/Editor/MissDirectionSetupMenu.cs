using System.IO;
using System.Collections.Generic;
using MissDirection.Fortunes;
using MissDirection.Networking;
using MissDirection.Player;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MissDirection.EditorTools
{
    /// <summary>
    /// Top menu "Miss Direction": builds the starter Fortune assets and a test playground scene,
    /// so a fresh clone can be played in a couple of clicks.
    /// </summary>
    public static class MissDirectionSetupMenu
    {
        const string FortuneFolder = "Assets/_Project/Data/Fortunes";
        const string DeckPath = FortuneFolder + "/StarterDeck.asset";
        const string PrefabFolder = "Assets/_Project/Prefabs";
        const string CardPrefabPath = PrefabFolder + "/FortuneCard.prefab";
        const string PlayerPrefabPath = PrefabFolder + "/Player.prefab";
        const string SceneFolder = "Assets/_Project/Scenes";
        const string PlaygroundPath = SceneFolder + "/Playground.unity";

        [MenuItem("Miss Direction/Create Starter Fortunes")]
        public static FortuneDeck CreateStarterFortunes()
        {
            EnsureFolder(FortuneFolder);

            var deck = AssetDatabase.LoadAssetAtPath<FortuneDeck>(DeckPath);
            if (deck == null)
            {
                deck = ScriptableObject.CreateInstance<FortuneDeck>();
                AssetDatabase.CreateAsset(deck, DeckPath);
            }
            deck.fortunes.Clear();

            // Movement
            Add(deck, "backwards_bob", "Backwards Bob", "Your controls are inverted.",
                FortuneKind.Bad, FortuneCategory.Movement, 0, (ModifierType.InvertMovement, 1f));
            Add(deck, "lagger", "Lagger", "Everything you do happens a moment too late.",
                FortuneKind.Bad, FortuneCategory.Movement, 0, (ModifierType.InputDelay, 0.75f));
            Add(deck, "zoolander", "Zoolander", "You can only turn right.",
                FortuneKind.Bad, FortuneCategory.Movement, 1, (ModifierType.TurnRightOnly, 1f));
            Add(deck, "runaway", "Runaway", "You can't stop running. Steer wisely.",
                FortuneKind.Bad, FortuneCategory.Movement, 1, (ModifierType.AutoRun, 1f));
            Add(deck, "hiccups", "Hiccups", "*hic* You jump when you least expect it.",
                FortuneKind.Bad, FortuneCategory.Movement, 0, (ModifierType.HiccupJumps, 1f));
            Add(deck, "moonwalker", "Moonwalker", "Gravity barely holds you.",
                FortuneKind.Bad, FortuneCategory.Movement, 0, (ModifierType.GravityScale, 0.4f));

            // Vision (capped: at most 1 per 5 players)
            Add(deck, "upside_down", "Upside Down", "Your world is upside down.",
                FortuneKind.Bad, FortuneCategory.Vision, 1, (ModifierType.FlipScreenVertical, 1f));
            Add(deck, "mirror", "Mirror", "Your left is everyone else's right.",
                FortuneKind.Bad, FortuneCategory.Vision, 1, (ModifierType.FlipScreenHorizontal, 1f));
            Add(deck, "blind", "Blind", "You see nothing. Listen, and hold someone's hand.",
                FortuneKind.Bad, FortuneCategory.Vision, 1, (ModifierType.Blind, 1f));

            // Voice (effect arrives with proximity voice chat)
            Add(deck, "mime", "Mime", "Your voice is gone. Point, ping and emote.",
                FortuneKind.Bad, FortuneCategory.Voice, 1);

            // Role (map and cart arrive later; for now the Navigator is just slow)
            Add(deck, "navigator", "Navigator", "Only you can see the map, but you can barely walk. Get pushed.",
                FortuneKind.Role, FortuneCategory.Role, 1, (ModifierType.MoveSpeedScale, 0.3f));

            // Good
            Add(deck, "featherweight", "Featherweight", "You can double jump.",
                FortuneKind.Good, FortuneCategory.Movement, 0, (ModifierType.ExtraJumps, 1f));

            EditorUtility.SetDirty(deck);
            AssetDatabase.SaveAssets();
            Debug.Log($"Miss Direction: starter deck has {deck.fortunes.Count} Fortunes at {DeckPath}");
            return deck;
        }

        const string MaterialFolder = "Assets/_Project/Art/Materials";

        // Carnival palette
        static readonly Color Purple = new(0.24f, 0.12f, 0.32f);
        static readonly Color CarnivalRed = new(0.85f, 0.12f, 0.18f);
        static readonly Color Cream = new(0.97f, 0.92f, 0.82f);
        static readonly Color Gold = new(1f, 0.78f, 0.2f);
        static readonly Color Teal = new(0.1f, 0.75f, 0.75f);
        static readonly Color Orange = new(1f, 0.5f, 0.1f);
        static readonly Color Pink = new(1f, 0.4f, 0.7f);
        static readonly Color SkyBlue = new(0.25f, 0.55f, 1f);
        static readonly Color Lime = new(0.45f, 0.9f, 0.25f);
        static readonly Color Ink = new(0.12f, 0.06f, 0.12f);

        [MenuItem("Miss Direction/Build Test Playground Scene")]
        public static void BuildPlayground()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(SceneFolder);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Create the assets AFTER opening the new scene: opening a scene unloads assets nothing
            // references yet, which used to leave the machine with an empty deck and no card.
            CreateStarterFortunes();
            CreateCardPrefab();
            CreatePlayerPrefab();
            AssetDatabase.SaveAssets();
            var deck = AssetDatabase.LoadAssetAtPath<FortuneDeck>(DeckPath);
            var cardPrefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            var cardPrefab = cardPrefabRoot != null ? cardPrefabRoot.GetComponent<FortuneCard>() : null;
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (deck == null || cardPrefab == null || playerPrefab == null)
                Debug.LogError("Miss Direction: couldn't load the starter deck, card or player prefab. Try the menu again.");

            var defaultCamera = Camera.main;
            if (defaultCamera != null) Object.DestroyImmediate(defaultCamera.gameObject);

            var sun = Object.FindAnyObjectByType<Light>();
            if (sun != null) sun.color = new Color(1f, 0.93f, 0.85f);

            BuildGround();
            BuildDecorations();
            BuildPlayArea();
            BuildMachine(deck, cardPrefab);
            BuildSpawnArea(new Vector3(0f, 0.05f, -4f));
            BuildLobbyCamera();
            BuildNetworkManager(playerPrefab);

            EditorSceneManager.SaveScene(scene, PlaygroundPath);
            AddSceneToBuild(PlaygroundPath);

            // Reopen and save once more so every networked object gets its saved network ID
            // (Netcode can only generate it for objects in a scene that already exists on disk).
            var reopened = EditorSceneManager.OpenScene(PlaygroundPath, OpenSceneMode.Single);
            EditorSceneManager.MarkSceneDirty(reopened);
            EditorSceneManager.SaveScene(reopened);
            AssetDatabase.SaveAssets();
            Debug.Log("Miss Direction: Playground built. Press Play, then HOST A GAME (or JOIN one).");
        }

        static void BuildGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(5f, 1f, 5f);
            Paint(ground, Mat("Ground", Purple));

            // Red-and-white striped walkway from spawn to the machine (no colliders, so no bumps)
            for (int i = 0; i < 6; i++)
            {
                var stripe = Deco(PrimitiveType.Cube, $"Walkway {i + 1}", new Vector3(0f, 0.005f, -4.5f + i),
                    new Vector3(1.6f, 0.01f, 1f), i % 2 == 0 ? Mat("CarnivalRed", CarnivalRed) : Mat("Cream", Cream));
                stripe.transform.SetParent(ground.transform, true);
            }

            var spawnPad = Deco(PrimitiveType.Cylinder, "Spawn Pad", new Vector3(0f, 0.012f, -4f),
                new Vector3(1.4f, 0.01f, 1.4f), Mat("Teal", Teal));
            spawnPad.transform.SetParent(ground.transform, true);
            Label("START", new Vector3(0f, 0.03f, -4.9f), 0.02f, Teal).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        static void BuildDecorations()
        {
            var root = new GameObject("Decorations").transform;
            var balloonColors = new[] { CarnivalRed, Gold, SkyBlue, Lime, Pink, Orange };

            // A ring of striped carnival poles with balloons on top
            const int poles = 14;
            for (int p = 0; p < poles; p++)
            {
                float angle = p * Mathf.PI * 2f / poles;
                var basePos = new Vector3(Mathf.Sin(angle) * 13f, 0f, Mathf.Cos(angle) * 13f);
                for (int s = 0; s < 4; s++)
                {
                    var segment = Deco(PrimitiveType.Cylinder, $"Pole {p + 1}", basePos + Vector3.up * (0.5f + s),
                        new Vector3(0.3f, 0.5f, 0.3f), s % 2 == 0 ? Mat("CarnivalRed", CarnivalRed) : Mat("Cream", Cream));
                    segment.transform.SetParent(root, true);
                }

                var color = balloonColors[p % balloonColors.Length];
                var balloon = Deco(PrimitiveType.Sphere, $"Balloon {p + 1}", basePos + Vector3.up * 4.5f,
                    new Vector3(0.7f, 0.85f, 0.7f), Mat($"Balloon{p % balloonColors.Length}", color, 0.4f));
                balloon.transform.SetParent(root, true);
            }
        }

        static void BuildPlayArea()
        {
            // Ledge to practice jumping
            var ledge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ledge.name = "Ledge";
            ledge.transform.position = new Vector3(5f, 0.6f, -1f);
            ledge.transform.localScale = new Vector3(3f, 1.2f, 3f);
            Paint(ledge, Mat("Orange", Orange));
            Label("JUMP UP HERE", new Vector3(5f, 2f, -2.6f), 0.02f, Orange);

            // Crates to practice grabbing
            var crateColors = new[] { CarnivalRed, Gold, SkyBlue, Lime };
            for (int i = 0; i < 4; i++)
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = $"Crate {i + 1}";
                box.transform.position = new Vector3(-5f + i * 0.9f, 0.3f, -1f);
                box.transform.localScale = Vector3.one * 0.6f;
                var rb = box.AddComponent<Rigidbody>();
                rb.mass = 5f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                Paint(box, Mat($"Crate{i}", crateColors[i]));

                // Networked: whoever grabs it gets control of it
                box.AddComponent<NetworkObject>();
                var nt = box.AddComponent<NetworkTransform>();
                nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
                nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
                box.AddComponent<NetworkRigidbody>();
                box.AddComponent<NetworkGrabbable>();
            }
            Label("GRAB & CARRY\n(hold left or right click)", new Vector3(-3.65f, 1.6f, -1f), 0.015f, Gold);
        }

        static void BuildMachine(FortuneDeck deck, FortuneCard cardPrefab)
        {
            var root = new GameObject("FortuneMachine");
            root.transform.position = new Vector3(0f, 0f, 2f);
            root.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // front faces the player spawn

            var body = Mat("CarnivalRed", CarnivalRed);
            var gold = Mat("Gold", Gold, 0.3f);
            var light = Mat("MachineLight", Lime, 2f);

            Part(root, PrimitiveType.Cube, "Cabinet", new Vector3(0f, 0.6f, 0f), new Vector3(1.2f, 1.2f, 0.8f), body);
            Part(root, PrimitiveType.Cube, "Gold Trim", new Vector3(0f, 1.22f, 0f), new Vector3(1.3f, 0.06f, 0.9f), gold);
            Part(root, PrimitiveType.Cube, "Booth", new Vector3(0f, 1.75f, -0.1f), new Vector3(1.2f, 1f, 0.6f), Mat("Purple", Purple));
            Part(root, PrimitiveType.Cube, "Roof", new Vector3(0f, 2.32f, 0f), new Vector3(1.4f, 0.14f, 1f), gold);

            // Madame Direction's head and glowing eyes
            Part(root, PrimitiveType.Sphere, "Madame Direction", new Vector3(0f, 1.75f, 0.15f), new Vector3(0.45f, 0.55f, 0.45f), Mat("Cream", Cream));
            var leftEye = Part(root, PrimitiveType.Sphere, "Eye L", new Vector3(-0.09f, 1.82f, 0.36f), Vector3.one * 0.08f, light);
            var rightEye = Part(root, PrimitiveType.Sphere, "Eye R", new Vector3(0.09f, 1.82f, 0.36f), Vector3.one * 0.08f, light);
            Part(root, PrimitiveType.Sphere, "Crystal Ball", new Vector3(0f, 1.38f, 0.3f), Vector3.one * 0.22f, Mat("CrystalBall", SkyBlue, 0.8f));

            // The big button
            var button = Part(root, PrimitiveType.Cylinder, "Button", new Vector3(0f, 1.0f, 0.42f), new Vector3(0.3f, 0.05f, 0.3f), light, keepCollider: false);
            button.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            button.AddComponent<BoxCollider>();
            Part(root, PrimitiveType.Cube, "Card Slot", new Vector3(0f, 0.7f, 0.41f), new Vector3(0.4f, 0.04f, 0.02f), Mat("Ink", Ink));

            var slot = new GameObject("CardSlot").transform;
            slot.SetParent(root.transform, false);
            slot.localPosition = new Vector3(0f, 0.95f, 0.5f);

            // "Stand here" ring in front of the machine
            Deco(PrimitiveType.Cylinder, "Stand Here", new Vector3(0f, 0.015f, 0.6f), new Vector3(1.2f, 0.01f, 1.2f), Mat("Gold", Gold, 0.3f))
                .transform.SetParent(root.transform, true);

            // Sign and lighting
            Label("MADAME DIRECTION'S\nFORTUNE MACHINE", new Vector3(0f, 3.1f, 2.4f), 0.035f, Gold);
            var lamp = new GameObject("Machine Lamp", typeof(Light)).GetComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = Pink;
            lamp.range = 7f;
            lamp.intensity = 4f;
            lamp.transform.position = new Vector3(0f, 3f, 1f);

            root.AddComponent<NetworkObject>();
            var machine = root.AddComponent<FortuneMachine>();
            var machineSo = new SerializedObject(machine);
            machineSo.FindProperty("deck").objectReferenceValue = deck;
            machineSo.FindProperty("cardPrefab").objectReferenceValue = cardPrefab;
            machineSo.FindProperty("cardSlot").objectReferenceValue = slot;
            machineSo.ApplyModifiedPropertiesWithoutUndo();

            var pressable = button.AddComponent<FortuneMachineButton>();
            var buttonSo = new SerializedObject(pressable);
            buttonSo.FindProperty("machine").objectReferenceValue = machine;
            buttonSo.ApplyModifiedPropertiesWithoutUndo();

            var lights = root.AddComponent<FortuneMachineLights>();
            var lightsSo = new SerializedObject(lights);
            lightsSo.FindProperty("machine").objectReferenceValue = machine;
            var lightList = lightsSo.FindProperty("lights");
            lightList.arraySize = 3;
            lightList.GetArrayElementAtIndex(0).objectReferenceValue = leftEye.GetComponent<Renderer>();
            lightList.GetArrayElementAtIndex(1).objectReferenceValue = rightEye.GetComponent<Renderer>();
            lightList.GetArrayElementAtIndex(2).objectReferenceValue = button.GetComponent<Renderer>();
            lightsSo.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildSpawnArea(Vector3 position)
        {
            var spawn = new GameObject("Player Spawn Area");
            spawn.transform.position = position;
            spawn.AddComponent<PlayerSpawnArea>();
        }

        static void BuildLobbyCamera()
        {
            var cam = new GameObject("Lobby Camera", typeof(Camera), typeof(AudioListener));
            cam.tag = "MainCamera";
            cam.transform.position = new Vector3(0f, 4.5f, -10f);
            cam.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
            cam.AddComponent<LobbyCamera>();
        }

        static void BuildNetworkManager(GameObject playerPrefab)
        {
            var go = new GameObject("NetworkManager");
            var manager = go.AddComponent<NetworkManager>();
            var transport = go.AddComponent<UnityTransport>();
            if (manager.NetworkConfig == null) manager.NetworkConfig = new NetworkConfig();

            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.PlayerPrefab = playerPrefab;
            manager.NetworkConfig.ConnectionApproval = true;
            transport.SetConnectionData("127.0.0.1", 7777);

            go.AddComponent<NetworkLauncher>();
            EditorUtility.SetDirty(manager);
        }

        static void AddSceneToBuild(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>
        /// The networked player. Netcode spawns one of these for everyone who joins.
        /// Rebuilt every time so it always matches the latest scripts.
        /// </summary>
        static void CreatePlayerPrefab()
        {
            EnsureFolder(PrefabFolder);

            var player = new GameObject("Player");

            // Networking first: everything below talks to it
            player.AddComponent<NetworkObject>();

            var capsule = player.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 1f, 0f);
            capsule.height = 2f;
            capsule.radius = 0.4f;

            var body = player.AddComponent<Rigidbody>();
            body.mass = 70f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;

            // Each player moves their own body; others see it smoothly (yaw only, no scale)
            var nt = player.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            nt.SyncRotAngleX = false;
            nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            player.AddComponent<NetworkRigidbody>();

            var modifiers = player.AddComponent<PlayerModifiers>();
            var input = player.AddComponent<PlayerInputReader>();
            var fortunes = player.AddComponent<PlayerFortunes>();

            // Visible body (others see it; you only see its shadow)
            var visual = new GameObject("Visual").transform;
            visual.SetParent(player.transform, false);

            var bodyMesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bodyMesh.name = "Body";
            Object.DestroyImmediate(bodyMesh.GetComponent<Collider>());
            bodyMesh.transform.SetParent(visual, false);
            bodyMesh.transform.localPosition = new Vector3(0f, 1f, 0f);
            bodyMesh.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            Paint(bodyMesh, Mat("Teal", Teal));

            var visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visor.name = "Visor (shows which way they face)";
            Object.DestroyImmediate(visor.GetComponent<Collider>());
            visor.transform.SetParent(visual, false);
            visor.transform.localPosition = new Vector3(0f, 1.6f, 0.33f);
            visor.transform.localScale = new Vector3(0.5f, 0.16f, 0.14f);
            Paint(visor, Mat("Ink", Ink));

            var nameLabel = CreateTextMesh("Name Tag", "Player", 0.012f, Color.white);
            nameLabel.transform.SetParent(player.transform, false);
            nameLabel.transform.localPosition = new Vector3(0f, 2.35f, 0f);

            // Owner-only camera rig
            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(player.transform, false);
            pivot.localPosition = new Vector3(0f, 1.65f, 0f);

            var cameraObject = new GameObject("PlayerCamera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(pivot, false);
            cameraObject.GetComponent<Camera>().nearClipPlane = 0.05f;
            var screenEffects = cameraObject.AddComponent<PlayerScreenEffects>();
            var effectsSo = new SerializedObject(screenEffects);
            effectsSo.FindProperty("modifiers").objectReferenceValue = modifiers;
            effectsSo.ApplyModifiedPropertiesWithoutUndo();

            var controller = player.AddComponent<PlayerController>();
            var controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("cameraPivot").objectReferenceValue = pivot;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            var grab = player.AddComponent<PlayerGrab>();
            var grabSo = new SerializedObject(grab);
            grabSo.FindProperty("viewCamera").objectReferenceValue = cameraObject.GetComponent<Camera>();
            grabSo.ApplyModifiedPropertiesWithoutUndo();

            var hud = player.AddComponent<MissDirection.UI.PlaygroundHud>();
            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("player").objectReferenceValue = fortunes;
            hudSo.FindProperty("viewCamera").objectReferenceValue = cameraObject.GetComponent<Camera>();
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            var network = player.AddComponent<PlayerNetwork>();
            var networkSo = new SerializedObject(network);
            networkSo.FindProperty("localOnlyRoot").objectReferenceValue = cameraObject;
            SetArray(networkSo.FindProperty("localOnlyBehaviours"), controller, grab, input, hud);
            var bodyRenderer = bodyMesh.GetComponent<Renderer>();
            var visorRenderer = visor.GetComponent<Renderer>();
            SetArray(networkSo.FindProperty("bodyRenderers"), bodyRenderer, visorRenderer);
            SetArray(networkSo.FindProperty("tintedRenderers"), bodyRenderer);
            networkSo.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            networkSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
            Object.DestroyImmediate(player);

            // Load and re-save so Netcode stamps the prefab with its network ID
            AssetDatabase.ForceReserializeAssets(new[] { PlayerPrefabPath });
        }

        static void SetArray(SerializedProperty property, params Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        /// <summary>Rebuilt every time so older versions of the card get replaced.</summary>
        static FortuneCard CreateCardPrefab()
        {
            EnsureFolder(PrefabFolder);

            var card = new GameObject("FortuneCard");
            var collider = card.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.5f, 0.62f, 0.04f);
            var rb = card.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var face = GameObject.CreatePrimitive(PrimitiveType.Cube);
            face.name = "Face";
            Object.DestroyImmediate(face.GetComponent<Collider>());
            face.transform.SetParent(card.transform, false);
            face.transform.localScale = new Vector3(0.5f, 0.62f, 0.03f);
            Paint(face, Mat("CardFace", Cream, 0.5f));

            var border = GameObject.CreatePrimitive(PrimitiveType.Cube);
            border.name = "Gold Border";
            Object.DestroyImmediate(border.GetComponent<Collider>());
            border.transform.SetParent(card.transform, false);
            border.transform.localScale = new Vector3(0.54f, 0.66f, 0.02f);
            Paint(border, Mat("Gold", Gold, 0.3f));

            var label = CreateTextMesh("Label", "FORTUNE", 0.006f, Ink);
            label.transform.SetParent(card.transform, false);
            label.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            label.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // readable from the machine's front

            var fortuneCard = card.AddComponent<FortuneCard>();
            var cardSo = new SerializedObject(fortuneCard);
            cardSo.FindProperty("label").objectReferenceValue = label;
            cardSo.FindProperty("face").objectReferenceValue = face.GetComponent<Renderer>();
            cardSo.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(card, CardPrefabPath);
            Object.DestroyImmediate(card);
            return prefab.GetComponent<FortuneCard>();
        }

        // ---------- helpers ----------

        static Material Mat(string materialName, Color color, float emission = 0f)
        {
            EnsureFolder(MaterialFolder);
            string path = $"{MaterialFolder}/M_{materialName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);

            if (emission > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor("_EmissionColor", color * emission);
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void Paint(GameObject go, Material mat)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = mat;
        }

        /// <summary>A purely visual primitive with no collider.</summary>
        static GameObject Deco(PrimitiveType type, string objectName, Vector3 position, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = objectName;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.position = position;
            go.transform.localScale = scale;
            Paint(go, mat);
            return go;
        }

        static GameObject Part(GameObject parent, PrimitiveType type, string objectName, Vector3 localPosition,
            Vector3 localScale, Material mat, bool keepCollider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = objectName;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            Paint(go, mat);
            return go;
        }

        /// <summary>A world-space sign, readable from the spawn side (looking toward +Z).</summary>
        static GameObject Label(string text, Vector3 position, float characterSize, Color color)
        {
            var mesh = CreateTextMesh("Sign: " + text.Split('\n')[0], text, characterSize, color);
            mesh.transform.position = position;
            return mesh.gameObject;
        }

        static TextMesh CreateTextMesh(string objectName, string text, float characterSize, Color color)
        {
            var go = new GameObject(objectName);
            var mesh = go.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.font = font;
            mesh.text = text;
            mesh.fontSize = 100;
            mesh.characterSize = characterSize;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontStyle = FontStyle.Bold;
            mesh.color = color;
            if (font != null) go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return mesh;
        }

        static void Add(FortuneDeck deck, string id, string displayName, string rule,
            FortuneKind kind, FortuneCategory category, int capPerFivePlayers,
            params (ModifierType type, float value)[] modifiers)
        {
            string fileName = displayName.Replace(" ", "").Replace("'", "");
            string path = $"{FortuneFolder}/Fortune_{fileName}.asset";

            var existing = AssetDatabase.LoadAssetAtPath<FortuneDefinition>(path);
            if (existing != null)
            {
                deck.fortunes.Add(existing); // never overwrite a designer's edits
                return;
            }

            var fortune = ScriptableObject.CreateInstance<FortuneDefinition>();
            fortune.id = id;
            fortune.displayName = displayName;
            fortune.cardRule = rule;
            fortune.kind = kind;
            fortune.category = category;
            fortune.capPerFivePlayers = capPerFivePlayers;
            AssetDatabase.CreateAsset(fortune, path);

            foreach (var (type, value) in modifiers)
            {
                var effect = ScriptableObject.CreateInstance<ModifierEffect>();
                effect.name = $"{type} {value}";
                effect.type = type;
                effect.value = value;
                AssetDatabase.AddObjectToAsset(effect, fortune);
                fortune.effects.Add(effect);
            }

            EditorUtility.SetDirty(fortune);
            deck.fortunes.Add(fortune);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
