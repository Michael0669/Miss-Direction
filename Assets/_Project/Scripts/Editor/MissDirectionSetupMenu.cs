using System.IO;
using MissDirection.Fortunes;
using MissDirection.Player;
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

        [MenuItem("Miss Direction/Build Test Playground Scene")]
        public static void BuildPlayground()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var deck = CreateStarterFortunes();
            var cardPrefab = CreateCardPrefab();

            EnsureFolder(SceneFolder);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var defaultCamera = Camera.main;
            if (defaultCamera != null) Object.DestroyImmediate(defaultCamera.gameObject);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(5f, 1f, 5f);

            var ledge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ledge.name = "Ledge (try jumping)";
            ledge.transform.position = new Vector3(5f, 0.6f, 2f);
            ledge.transform.localScale = new Vector3(3f, 1.2f, 3f);

            for (int i = 0; i < 4; i++)
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = $"Crate {i + 1} (grab me)";
                box.transform.position = new Vector3(-3f + i * 0.8f, 0.5f, 0f);
                box.transform.localScale = Vector3.one * 0.6f;
                box.AddComponent<Rigidbody>().mass = 5f;
            }

            BuildMachine(deck, cardPrefab);
            BuildPlayer(new Vector3(0f, 0.05f, -4f));

            EditorSceneManager.SaveScene(scene, PlaygroundPath);
            Debug.Log("Miss Direction: Playground built. Press Play, walk to the machine and grab the button (left mouse).");
        }

        static void BuildMachine(FortuneDeck deck, FortuneCard cardPrefab)
        {
            var root = new GameObject("FortuneMachine");
            root.transform.position = new Vector3(0f, 0f, 2f);
            root.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // front faces the player spawn

            var cabinet = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cabinet.name = "Cabinet";
            cabinet.transform.SetParent(root.transform, false);
            cabinet.transform.localPosition = new Vector3(0f, 1f, 0f);
            cabinet.transform.localScale = new Vector3(1.2f, 2f, 0.8f);

            var button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = "Button (grab to press)";
            button.transform.SetParent(root.transform, false);
            button.transform.localPosition = new Vector3(0f, 1.3f, 0.45f);
            button.transform.localScale = new Vector3(0.3f, 0.3f, 0.15f);

            var slot = new GameObject("CardSlot").transform;
            slot.SetParent(root.transform, false);
            slot.localPosition = new Vector3(0f, 0.9f, 0.7f);

            var machine = root.AddComponent<FortuneMachine>();
            var machineSo = new SerializedObject(machine);
            machineSo.FindProperty("deck").objectReferenceValue = deck;
            machineSo.FindProperty("cardPrefab").objectReferenceValue = cardPrefab;
            machineSo.FindProperty("cardSlot").objectReferenceValue = slot;
            machineSo.FindProperty("teamSize").intValue = 1;
            machineSo.ApplyModifiedPropertiesWithoutUndo();

            var pressable = button.AddComponent<FortuneMachineButton>();
            var buttonSo = new SerializedObject(pressable);
            buttonSo.FindProperty("machine").objectReferenceValue = machine;
            buttonSo.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildPlayer(Vector3 position)
        {
            var player = new GameObject("Player");
            player.transform.position = position;

            var capsule = player.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 1f, 0f);
            capsule.height = 2f;
            capsule.radius = 0.4f;

            var body = player.AddComponent<Rigidbody>();
            body.mass = 70f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;

            player.AddComponent<PlayerModifiers>();
            player.AddComponent<PlayerInputReader>();
            player.AddComponent<PlayerFortunes>();

            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Body";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(player.transform, false);
            visual.transform.localPosition = new Vector3(0f, 1f, 0f);
            visual.transform.localScale = new Vector3(0.8f, 1f, 0.8f);

            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(player.transform, false);
            pivot.localPosition = new Vector3(0f, 1.65f, 0f);

            var cameraObject = new GameObject("PlayerCamera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(pivot, false);
            cameraObject.AddComponent<PlayerScreenEffects>();

            var controller = player.AddComponent<PlayerController>();
            var controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("cameraPivot").objectReferenceValue = pivot;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            player.AddComponent<PlayerGrab>();
        }

        static FortuneCard CreateCardPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<FortuneCard>(CardPrefabPath);
            if (existing != null) return existing;

            EnsureFolder(PrefabFolder);
            var card = GameObject.CreatePrimitive(PrimitiveType.Cube);
            card.name = "FortuneCard";
            card.transform.localScale = new Vector3(0.25f, 0.02f, 0.4f);
            card.AddComponent<Rigidbody>().mass = 0.1f;
            card.AddComponent<FortuneCard>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(card, CardPrefabPath);
            Object.DestroyImmediate(card);
            return prefab.GetComponent<FortuneCard>();
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
