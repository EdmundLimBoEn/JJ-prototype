using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds Assets/Scenes/Main.unity from LevelData (generated from the excalidraw
// "Full run" drawing). Run from the menu (Tools > Build Level) or batchmode:
//   Unity -batchmode -projectPath <project> -executeMethod LevelBuilder.Build -quit
public static class LevelBuilder
{
    const int LayerWorld = 0;    // Default — solid geometry
    const int LayerTrigger = 1;  // TransparentFX — triggers, invisible to ground checks
    const int LayerPlayer = 2;   // Ignore Raycast

    static readonly Color Sky = new Color(0.745f, 0.890f, 0.941f);
    static readonly Color Ink = new Color(0.227f, 0.2f, 0.251f);

    [MenuItem("Tools/Build Level")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // --- Camera ---
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6.5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Sky;
        camGo.AddComponent<AudioListener>();
        var follow = camGo.AddComponent<CameraFollow>();

        var msgGo = new GameObject("Message");
        msgGo.transform.SetParent(camGo.transform, false);
        msgGo.transform.localPosition = new Vector3(0f, 3.4f, 10f);
        var msg = msgGo.AddComponent<TextMesh>();
        msg.font = font;
        msg.fontSize = 64;
        msg.characterSize = 0.09f;
        msg.anchor = TextAnchor.MiddleCenter;
        msg.alignment = TextAlignment.Center;
        msg.text = "";
        msgGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        msgGo.GetComponent<MeshRenderer>().sortingOrder = 100;

        // --- Level geometry ---
        // All solid boxes merge into one CompositeCollider2D outline so seams
        // between adjacent platforms can never catch the player or the ball.
        var level = new GameObject("Level").transform;
        var levelBody = level.gameObject.AddComponent<Rigidbody2D>();
        levelBody.bodyType = RigidbodyType2D.Static;
        var composite = level.gameObject.AddComponent<CompositeCollider2D>();
        composite.geometryType = CompositeCollider2D.GeometryType.Outlines;
        level.gameObject.layer = LayerWorld;

        // The ball is meant to be a near-frictionless slider (see
        // PlayerController): Unity's default 0.4 surface friction mixes in as
        // sqrt(0.4 * 0.02) ~ 0.09 and bleeds ~20% of the chute launch speed,
        // so the world itself is slick too. Paper and plane already carry
        // zero-friction materials, so this only affects the ball.
        const string matPath = "Assets/LevelPhysics.physicsMaterial2D";
        var levelMat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(matPath);
        if (levelMat == null)
        {
            levelMat = new PhysicsMaterial2D("level");
            AssetDatabase.CreateAsset(levelMat, matPath);
        }
        levelMat.friction = 0.02f;
        levelMat.bounciness = 0f;
        composite.sharedMaterial = levelMat;

        Sprite groundTop = S("ground_top"), ground = S("ground");
        foreach (var p in LevelData.Platforms)
        {
            var go = new GameObject("Platform");
            go.transform.SetParent(level, false);
            go.transform.position = new Vector3(p.x, p.y, 0f);
            go.transform.rotation = Quaternion.Euler(0, 0, p.rot);
            go.layer = LayerWorld;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(p.w, p.h);
            col.compositeOperation = Collider2D.CompositeOperation.Merge;

            if (p.h <= 1.2f)
            {
                AddTiledSprite(go.transform, groundTop, p.w, p.h, Vector2.zero, 0);
            }
            else
            {
                AddTiledSprite(go.transform, groundTop, p.w, 1f, new Vector2(0f, (p.h - 1f) / 2f), 0);
                AddTiledSprite(go.transform, ground, p.w, p.h - 1f, new Vector2(0f, -0.5f), 0);
            }
        }

        // --- Kill zones ---
        Sprite kill = S("kill");
        foreach (var z in LevelData.KillZones)
        {
            var go = new GameObject("KillZone");
            go.transform.SetParent(level, false);
            go.transform.position = new Vector3(z.x, z.y, 0f);
            go.layer = LayerTrigger;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(z.w, z.h);
            col.isTrigger = true;
            go.AddComponent<KillZone>().message = z.msg;
            AddTiledSprite(go.transform, kill, z.w, z.h, Vector2.zero, 1);
        }

        // --- Checkpoints ---
        Sprite flagOff = S("flag_off"), flagOn = S("flag_on");
        foreach (var c in LevelData.Checkpoints)
        {
            var go = new GameObject(c.goal ? "Goal" : "Checkpoint");
            go.transform.SetParent(level, false);
            go.transform.position = new Vector3(c.x, c.y + 1f, 0f); // flag sprite is 2 units tall, pivot center
            go.layer = LayerTrigger;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = flagOff;
            sr.sortingOrder = 2;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.4f, 2.4f);
            col.isTrigger = true;
            var cp = go.AddComponent<Checkpoint>();
            cp.offSprite = flagOff;
            cp.onSprite = flagOn;
            cp.unlocksDoubleJump = c.doubleJump;
            cp.isGoal = c.goal;
        }

        // --- Tutorial signs ---
        Sprite sign = S("sign");
        foreach (var s in LevelData.Signs)
        {
            var go = new GameObject("Sign");
            go.transform.SetParent(level, false);
            go.transform.position = new Vector3(s.x, s.y + 0.5f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sign;
            sr.sortingOrder = 1;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            var label = labelGo.AddComponent<TextMesh>();
            label.font = font;
            label.fontSize = 48;
            label.characterSize = 0.045f;
            label.anchor = TextAnchor.LowerCenter;
            label.alignment = TextAlignment.Center;
            label.text = s.text;
            label.color = Ink;
            labelGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            labelGo.GetComponent<MeshRenderer>().sortingOrder = 10;

            go.AddComponent<TutorialSign>().label = label;
        }

        // --- Decorative clouds ---
        Sprite cloud = S("cloud");
        var rng = new System.Random(7);
        var clouds = new GameObject("Clouds").transform;
        clouds.SetParent(level, false);
        for (int i = 0; i < 18; i++)
        {
            var go = new GameObject("Cloud");
            go.transform.SetParent(clouds, false);
            float x = (float)rng.NextDouble() * 400f;
            float y = 6f + (float)rng.NextDouble() * 20f;
            float sc = 1.5f + (float)rng.NextDouble() * 2f;
            go.transform.position = new Vector3(x, y, 0f);
            go.transform.localScale = new Vector3(sc, sc, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = cloud;
            sr.sortingOrder = -10;
            sr.color = new Color(1f, 1f, 1f, 0.75f);
        }

        // --- Player ---
        var player = new GameObject("Player");
        player.transform.position = new Vector3(LevelData.PlayerStartX, LevelData.PlayerStartY, 0f);
        player.layer = LayerPlayer;
        var body = player.AddComponent<Rigidbody2D>();
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.freezeRotation = true;

        var paperCol = player.AddComponent<BoxCollider2D>();
        paperCol.size = new Vector2(0.75f, 1.4f);
        var ballCol = player.AddComponent<CircleCollider2D>();
        ballCol.radius = 0.34f;
        var planeCol = player.AddComponent<BoxCollider2D>();
        planeCol.size = new Vector2(1.4f, 0.5f);

        var visual = new GameObject("Visual");
        visual.transform.SetParent(player.transform, false);
        var vsr = visual.AddComponent<SpriteRenderer>();
        vsr.sprite = S("paper_idle_0");
        vsr.sortingOrder = 5;

        var pc = player.AddComponent<PlayerController>();
        pc.paperIdle = new[] { S("paper_idle_0"), S("paper_idle_1") };
        pc.paperWalk = new[] { S("paper_walk_0"), S("paper_walk_1") };
        pc.paperFold = new[] { S("paper_fold_0"), S("paper_fold_1") };
        pc.ballFrames = new[] { S("ball_0"), S("ball_1"), S("ball_2"), S("ball_3") };
        pc.planeFrames = new[] { S("plane_0"), S("plane_1") };

        follow.target = player.transform;
        camGo.transform.position = player.transform.position + new Vector3(0, 1.5f, -10f);

        // --- Game manager ---
        var gmGo = new GameObject("GameManager");
        var gm = gmGo.AddComponent<GameManager>();
        gm.player = pc;
        gm.messageText = msg;
        gm.globalKillY = LevelData.GlobalKillY;

        // --- Save ---
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Main.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Main.unity", true) };
        AssetDatabase.SaveAssets();
        Debug.Log("LevelBuilder: scene saved to Assets/Scenes/Main.unity");
    }

    static Sprite S(string name)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/{name}.png");
        if (sprite == null) Debug.LogError($"LevelBuilder: missing sprite '{name}'");
        return sprite;
    }

    static void AddTiledSprite(Transform parent, Sprite sprite, float w, float h, Vector2 localPos, int order)
    {
        var go = new GameObject("Sprite");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = new Vector2(w, h);
        sr.sortingOrder = order;
    }
}
