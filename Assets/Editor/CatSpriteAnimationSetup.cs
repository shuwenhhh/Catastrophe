#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class CatSpriteAnimationSetup
{
    private const string Folder = "Assets/Art/Cat";
    private const string SmoothFolder = "Assets/Art/TuxedoKitten/GeneratedSmoothSprites";
    private const string SideTexturePath = SmoothFolder + "/Smooth_Walk_Side_6Frames.png";
    private const string FrontTexturePath = SmoothFolder + "/Smooth_Walk_TowardCamera_6Frames.png";
    private const string BackTexturePath = SmoothFolder + "/Smooth_Walk_Away_Back_6Frames.png";
    private const string SideJumpTexturePath = SmoothFolder + "/Smooth_Jump_Side_6Frames.png";
    private const string FrontJumpTexturePath = SmoothFolder + "/Smooth_Jump_TowardCamera_6Frames.png";
    private const string BackJumpTexturePath = SmoothFolder + "/Smooth_Jump_Away_Back_6Frames.png";
    private const string PushTexturePath = SmoothFolder + "/Smooth_GentleLeftPawPush_4Frames.png";
    private const string IdleClipPath = Folder + "/Cat_Idle.anim";
    private const string WalkClipPath = Folder + "/Cat_Walk.anim";
    private const string FrontIdleClipPath = Folder + "/Cat_Idle_Front.anim";
    private const string FrontWalkClipPath = Folder + "/Cat_Walk_Front.anim";
    private const string BackIdleClipPath = Folder + "/Cat_Idle_Back.anim";
    private const string BackWalkClipPath = Folder + "/Cat_Walk_Back.anim";
    private const string SideJumpClipPath = Folder + "/Cat_Jump_Side.anim";
    private const string FrontJumpClipPath = Folder + "/Cat_Jump_Front.anim";
    private const string BackJumpClipPath = Folder + "/Cat_Jump_Back.anim";
    private const string PushClipPath = Folder + "/Cat_Push_Side.anim";
    private const string ControllerPath = Folder + "/CatSprite.controller";
    private const string PrefabFolder = "Assets/Resources/Cat";
    private const string PrefabPath = PrefabFolder + "/CatSprite.prefab";
    private const int Columns = 6;
    private const int Rows = 1;
    private const int WalkFrameWidth = 362;
    private const int JumpFrameWidth = 362;
    private const int PushColumns = 4;
    private const int PushFrameWidth = 543;
    private const int ExpectedFrameHeight = 724;
    private const float WalkFramesPerSecond = 8f;
    private const float JumpFramesPerSecond = 5f;

    [MenuItem("Catastrophe/Build Cat Sprite Animation")]
    public static void Build()
    {
        Sprite[] sideFrames = LoadFrames(SideTexturePath, "Walk", "Side", WalkFrameWidth, Columns);
        Sprite[] frontFrames = LoadFrames(FrontTexturePath, "Walk", "Front", WalkFrameWidth, Columns);
        Sprite[] backFrames = LoadFrames(BackTexturePath, "Walk", "Back", WalkFrameWidth, Columns);
        Sprite[] sideJumpFrames = LoadFrames(SideJumpTexturePath, "Jump", "Side", JumpFrameWidth, Columns);
        Sprite[] frontJumpFrames = LoadFrames(FrontJumpTexturePath, "Jump", "Front", JumpFrameWidth, Columns);
        Sprite[] backJumpFrames = LoadFrames(BackJumpTexturePath, "Jump", "Back", JumpFrameWidth, Columns);
        Sprite[] pushFrames = LoadFrames(PushTexturePath, "Push", "Side", PushFrameWidth, PushColumns);

        // Contact poses make a stable four-paw idle; frame zero is a stride
        // pose with one paw raised and looked frozen when movement stopped.
        AnimationClip sideIdle = CreateIdleClip(sideFrames[1], IdleClipPath, "Cat_Idle");
        AnimationClip sideWalk = CreateWalkClip(sideFrames, WalkClipPath, "Cat_Walk");
        AnimationClip frontIdle = CreateIdleClip(frontFrames[3], FrontIdleClipPath, "Cat_Idle_Front");
        AnimationClip frontWalk = CreateWalkClip(frontFrames, FrontWalkClipPath, "Cat_Walk_Front");
        AnimationClip backIdle = CreateIdleClip(backFrames[4], BackIdleClipPath, "Cat_Idle_Back");
        AnimationClip backWalk = CreateWalkClip(backFrames, BackWalkClipPath, "Cat_Walk_Back");
        AnimationClip sideJump = CreateActionClip(sideJumpFrames, SideJumpClipPath, "Cat_Jump_Side");
        AnimationClip frontJump = CreateActionClip(frontJumpFrames, FrontJumpClipPath, "Cat_Jump_Front");
        AnimationClip backJump = CreateActionClip(backJumpFrames, BackJumpClipPath, "Cat_Jump_Back");
        AnimationClip push = CreateActionClip(pushFrames, PushClipPath, "Cat_Push_Side");
        AnimatorController controller = CreateController(
            sideIdle, sideWalk, frontIdle, frontWalk, backIdle, backWalk,
            sideJump, frontJump, backJump, push);
        CreatePrefab(sideFrames[1], controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"CAT_SPRITE_SETUP_COMPLETE grid={Columns}x{Rows} height={ExpectedFrameHeight} " +
                  $"walkFps={WalkFramesPerSecond} jumpFps={JumpFramesPerSecond} directions=3 " +
                  $"walkFrames={sideFrames.Length + frontFrames.Length + backFrames.Length} " +
                  $"jumpFrames={sideJumpFrames.Length + frontJumpFrames.Length + backJumpFrames.Length} " +
                  $"prefab={PrefabPath}");
    }

    private static Sprite[] LoadFrames(string texturePath, string animationName,
        string directionName, int expectedFrameWidth, int columns)
    {
        if (!File.Exists(texturePath))
            throw new FileNotFoundException(
                $"Cat {animationName} {directionName} sprite sheet is missing.", texturePath);

        ConfigureAndSliceTexture(texturePath, animationName, directionName, expectedFrameWidth, columns);
        Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(texturePath)
            .OfType<Sprite>()
            .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
            .ToArray();
        if (frames.Length != columns * Rows)
            throw new InvalidOperationException(
                $"Expected {columns * Rows} {directionName} frames but imported {frames.Length}.");
        return frames;
    }

    private static void ConfigureAndSliceTexture(string texturePath, string animationName,
        string directionName, int expectedFrameWidth, int columns)
    {
        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
        if (AssetImporter.GetAtPath(texturePath) is not TextureImporter importer)
            throw new InvalidOperationException("Could not load cat sprite TextureImporter.");

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 362f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096;
        ConfigurePlatform(importer, "Standalone");
        ConfigurePlatform(importer, "WebGL");

        SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
        factories.Init();
        ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        if (provider == null)
            throw new InvalidOperationException("Could not create the Sprite Editor data provider.");
        provider.InitSpriteEditorDataProvider();
        importer.GetSourceTextureWidthAndHeight(out int textureWidth, out int textureHeight);
        if (textureWidth != expectedFrameWidth * columns || textureHeight != ExpectedFrameHeight * Rows)
            throw new InvalidOperationException(
                $"Unexpected sprite sheet size {textureWidth}x{textureHeight}; expected " +
                $"{expectedFrameWidth * columns}x{ExpectedFrameHeight}.");
        int frameWidth = textureWidth / columns;
        int frameHeight = textureHeight / Rows;
        SpriteRect[] rects = new SpriteRect[columns * Rows];
        int index = 0;
        for (int rowFromTop = 0; rowFromTop < Rows; rowFromTop++)
        {
            int unityRow = Rows - 1 - rowFromTop;
            for (int column = 0; column < columns; column++)
            {
                rects[index] = new SpriteRect
                {
                    name = $"Cat_{animationName}_{directionName}_{index:00}",
                    rect = new Rect(column * frameWidth, unityRow * frameHeight, frameWidth, frameHeight),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    border = Vector4.zero,
                    spriteID = GUID.Generate()
                };
                index++;
            }
        }

        provider.SetSpriteRects(rects);
        provider.Apply();
        importer.SaveAndReimport();
    }

    private static void ConfigurePlatform(TextureImporter importer, string platformName)
    {
        TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platformName);
        settings.name = platformName;
        settings.overridden = true;
        settings.maxTextureSize = 4096;
        settings.format = TextureImporterFormat.RGBA32;
        settings.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SetPlatformTextureSettings(settings);
    }

    private static AnimationClip CreateIdleClip(Sprite firstFrame, string path, string clipName)
    {
        AssetDatabase.DeleteAsset(path);
        AnimationClip clip = new AnimationClip { name = clipName, frameRate = WalkFramesPerSecond };
        EditorCurveBinding binding = SpriteBinding();
        AnimationUtility.SetObjectReferenceCurve(clip, binding,
            new[] { new ObjectReferenceKeyframe { time = 0f, value = firstFrame } });
        SetLoop(clip, true);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static AnimationClip CreateWalkClip(Sprite[] frames, string path, string clipName)
    {
        AssetDatabase.DeleteAsset(path);
        AnimationClip clip = new AnimationClip { name = clipName, frameRate = WalkFramesPerSecond };
        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[frames.Length + 1];
        for (int index = 0; index < frames.Length; index++)
            keys[index] = new ObjectReferenceKeyframe { time = index / WalkFramesPerSecond, value = frames[index] };
        keys[^1] = new ObjectReferenceKeyframe { time = frames.Length / WalkFramesPerSecond, value = frames[0] };
        AnimationUtility.SetObjectReferenceCurve(clip, SpriteBinding(), keys);
        SetLoop(clip, true);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static AnimationClip CreateActionClip(Sprite[] frames, string path, string clipName)
    {
        AssetDatabase.DeleteAsset(path);
        AnimationClip clip = new AnimationClip { name = clipName, frameRate = JumpFramesPerSecond };
        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[frames.Length + 1];
        for (int index = 0; index < frames.Length; index++)
            keys[index] = new ObjectReferenceKeyframe
                { time = index / JumpFramesPerSecond, value = frames[index] };
        keys[^1] = new ObjectReferenceKeyframe
            { time = frames.Length / JumpFramesPerSecond, value = frames[^1] };
        AnimationUtility.SetObjectReferenceCurve(clip, SpriteBinding(), keys);
        SetLoop(clip, false);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static AnimatorController CreateController(
        AnimationClip sideIdle, AnimationClip sideWalk,
        AnimationClip frontIdle, AnimationClip frontWalk,
        AnimationClip backIdle, AnimationClip backWalk,
        AnimationClip sideJump, AnimationClip frontJump, AnimationClip backJump,
        AnimationClip push)
    {
        AssetDatabase.DeleteAsset(ControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("DirectionX", AnimatorControllerParameterType.Float);
        controller.AddParameter("DirectionY", AnimatorControllerParameterType.Float);
        controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Push", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idleSideState = AddState(machine, "Idle_Side", sideIdle);
        AddState(machine, "Walk_Side", sideWalk);
        AddState(machine, "Idle_Front", frontIdle);
        AddState(machine, "Walk_Front", frontWalk);
        AddState(machine, "Idle_Back", backIdle);
        AddState(machine, "Walk_Back", backWalk);
        AddState(machine, "Jump_Side", sideJump);
        AddState(machine, "Jump_Front", frontJump);
        AddState(machine, "Jump_Back", backJump);
        AddState(machine, "Push_Side", push);
        machine.defaultState = idleSideState;
        return controller;
    }

    private static AnimatorState AddState(AnimatorStateMachine machine, string name, Motion motion)
    {
        AnimatorState state = machine.AddState(name);
        state.motion = motion;
        return state;
    }

    private static void CreatePrefab(Sprite firstFrame, RuntimeAnimatorController controller)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets/Resources", "Cat");

        GameObject root = new GameObject("CatSprite");
        root.AddComponent<BillboardSprite>();
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = firstFrame;
        renderer.sortingOrder = 20;
        Animator animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
    }

    private static EditorCurveBinding SpriteBinding()
    {
        return new EditorCurveBinding
        {
            path = string.Empty,
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite"
        };
    }

    private static void SetLoop(AnimationClip clip, bool loop)
    {
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
    }
}
#endif
