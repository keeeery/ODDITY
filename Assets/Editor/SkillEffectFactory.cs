using System.IO;
using UnityEditor;
using UnityEngine;

namespace TeamOdd.EditorTools
{
    // 스킬 이펙트 프리팹을 코드로 만든다. 파티클 값을 손으로 하나씩 넣지 않아도 되게 하는 도구
    // 프리팹이 없으면 에디터가 켜질 때(코드가 다시 컴파일될 때) 알아서 만든다. 다시 만들고 싶으면 프리팹을 지우거나 메뉴를 누른다
    // 만든 프리팹은 SkillChip.tsv의 PrefabKey로 불러 쓴다 (순간 이동 칩: Skill/Effect/BlinkEffect, 두 번째 심장 칩: Skill/Effect/SecondHeartEffect,
    // 염동 충격파 칩: Skill/Effect/ShockwaveEffect)
    // 회피 피드백(Skill/Effect/ManualDodgeEffect, AutoDodgeEffect)은 칩 표에 없고 DodgeEffect가 회피 유형을 보고 직접 불러 쓴다
    [InitializeOnLoad]
    internal static class SkillEffectFactory
    {
        private const string BlinkPrefabPath = "Assets/Resources/Skill/Effect/BlinkEffect.prefab";
        private const string BlinkMaterialPath = "Assets/VFX/Skill/BlinkEffect.mat";
        private const string SecondHeartPrefabPath = "Assets/Resources/Skill/Effect/SecondHeartEffect.prefab";
        private const string SecondHeartMaterialPath = "Assets/VFX/Skill/SecondHeartEffect.mat";
        private const string BaseMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";

        // 웜홀 전체 크기 배율. 구체 크기, 빛줄기 범위·굵기·빠르기에 모두 곱한다. 전체를 키우거나 줄일 때는 이것만 바꾼다
        private const float BlinkScale = 2f;
        private const float SphereSize = 1.6f; // 웜홀 구체 지름 (m, 배율 곱하기 전)
        private const float PullRadius = 2f;   // 빛줄기가 빨려 들기 시작하는 거리 (m, 배율 곱하기 전)
        private const float PullLifetime = 0.25f;
        // 구체를 같은 자리에 몇 겹 겹쳐 그릴지. 겹칠수록 밝아지는 머티리얼(Additive)이라 겹이 많을수록 진하고 또렷해진다
        private const int SphereLayers = 3;

        private static readonly Color Violet = new Color(0.71f, 0.49f, 1f);     // #B47CFF
        private static readonly Color SkyBlue = new Color(0.5f, 0.72f, 1f);     // #7FB8FF
        private static readonly Color PaleViolet = new Color(0.79f, 0.65f, 1f); // #C9A6FF

        // 심장 박동: 두 번 뛰는 시각(초). 두 번째 박동 때 바닥 충격파가 퍼진다
        private const float FirstBeatTime = 0f;
        private const float SecondBeatTime = 0.35f;

        private static readonly Color HeartRed = new Color(1f, 0.2f, 0.33f);   // #FF3355
        private static readonly Color EmberOrange = new Color(1f, 0.54f, 0.24f); // #FF8A3D

        // 수동 회피: 몸이 빠져나간 자리에 남는 하얀 허물. 슬로모션 동안 하얗게 머물다 바스러진다
        private const string ManualDodgePrefabPath = "Assets/Resources/Skill/Effect/ManualDodgeEffect.prefab";
        private const string ManualDodgeMaterialPath = "Assets/VFX/Skill/ManualDodgeEffect.mat";
        private const string DodgeShellShaderName = "TeamOdd/Dodge Shell";
        private const float ShellHoldDuration = 0.5f;      // 하얗게 머무는 시간. 수동 회피 슬로모션 시간(DodgeEffect.SlowDuration)과 맞춘다 (실제 시간, 초)
        private const float ShellDissolveDuration = 0.35f; // 바스러져 사라지는 시간 (실제 시간, 초)

        // 자동 회피: 몸이 빠져나간 자리에 남는 파란 잔상. 수동 회피(청록)보다 짙은 파랑으로 나눈다
        private const string AutoDodgePrefabPath = "Assets/Resources/Skill/Effect/AutoDodgeEffect.prefab";
        private const string AutoDodgeMaterialPath = "Assets/VFX/Skill/AutoDodgeEffect.mat";
        private static readonly Color ReflexBlue = new Color(0.25f, 0.5f, 1f, 0.6f); // #4080FF, 처음 투명도 0.6
        private const float AfterimageDuration = 0.4f; // 잔상이 흐려져 사라지는 시간 (초)

        // 염동 충격파: 와우의 신비한 폭발처럼 플레이어 둘레로 보랏빛이 터져 퍼진다
        private const string ShockwavePrefabPath = "Assets/Resources/Skill/Effect/ShockwaveEffect.prefab";
        private const string ShockwaveMaterialPath = "Assets/VFX/Skill/ShockwaveEffect.mat";
        private const string ShockwaveDomeMaterialPath = "Assets/VFX/Skill/ShockwaveDome.mat";
        private const string ArcaneDomeShaderName = "TeamOdd/Arcane Dome";
        // 프리팹에 넣어 두는 기본 반경 (m). 게임에서는 ShockwaveSkill이 표의 Radius를 넘겨 반구와 가루 범위를 다시 맞춘다
        private const float ShockwaveRadius = 6f;

        private static readonly Color ArcanePurple = new Color(0.66f, 0.35f, 1f); // #A85AFF
        private static readonly Color ArcanePink = new Color(1f, 0.42f, 0.9f);    // #FF6BE6
        private static readonly Color ArcaneWhite = new Color(0.92f, 0.85f, 1f);  // #EBD9FF

        static SkillEffectFactory()
        {
            // 컴파일 직후에는 에셋을 만들 수 없어서 에디터가 한숨 돌린 다음에 만든다
            EditorApplication.delayCall += () =>
            {
                if (File.Exists(BlinkPrefabPath) == false)
                    CreateBlinkEffect();
                if (File.Exists(SecondHeartPrefabPath) == false)
                    CreateSecondHeartEffect();
                if (File.Exists(ManualDodgePrefabPath) == false)
                    CreateManualDodgeEffect();
                if (File.Exists(AutoDodgePrefabPath) == false)
                    CreateAutoDodgeEffect();
                if (File.Exists(ShockwavePrefabPath) == false)
                    CreateShockwaveEffect();
            };
        }

        // 염동 충격파 이펙트 (신비한 폭발). ShockwaveSkill이 시전한 자리에 띄우고, 플레이어를 따라다니지 않고 그 자리에 남는다
        // - Flash: 가슴 높이에서 보랏빛이 번쩍 터지고, 가운데에 하얀 빛(Core)이 겹친다
        // - Dome: 발밑에서 투명한 반구가 충격파 반경까지 확 부풀었다가 흐려진다. 파티클이 아니라 ShockwaveEffect가 키운다
        // - Sparks: 빛줄기가 사방으로 튀어 나가 범위 끝쯤에서 사라진다
        // - 맨 위(Motes): 범위 안 바닥에서 반짝이는 가루가 피어올라 가장 오래 남는다. 끝날 때 Stop Action으로 전체를 지운다
        [MenuItem("Tools/Team Odd/Create Shockwave Effect")]
        private static void CreateShockwaveEffect()
        {
            Material material = CreateAdditiveMaterial(ShockwaveMaterialPath);

            Material domeMaterial = new Material(Shader.Find(ArcaneDomeShaderName));
            Directory.CreateDirectory(Path.GetDirectoryName(ShockwaveDomeMaterialPath));
            AssetDatabase.DeleteAsset(ShockwaveDomeMaterialPath);
            AssetDatabase.CreateAsset(domeMaterial, ShockwaveDomeMaterialPath);

            GameObject root = new GameObject("ShockwaveEffect");
            SetUpMotes(root.AddComponent<ParticleSystem>(), material);

            // 기본 구를 만들어 충돌체만 뗀다. 크기는 0에서 시작해 ShockwaveEffect가 키운다
            GameObject dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = "Dome";
            Object.DestroyImmediate(dome.GetComponent<Collider>());
            dome.transform.SetParent(root.transform, false);
            dome.transform.localScale = Vector3.zero;
            MeshRenderer domeRenderer = dome.GetComponent<MeshRenderer>();
            domeRenderer.sharedMaterial = domeMaterial;
            domeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            SerializedObject effect = new SerializedObject(root.AddComponent<Superpower.ShockwaveEffect>());
            effect.FindProperty("dome").objectReferenceValue = domeRenderer;
            effect.ApplyModifiedPropertiesWithoutUndo();

            ParticleSystem flash = CreateChild(root.transform, "Flash");
            flash.transform.localPosition = new Vector3(0f, 1f, 0f); // 가슴 높이
            SetUpSingle(flash, material, 0.3f, 7f, ArcanePurple);
            SetSize(flash, new AnimationCurve(new Keyframe(0f, 0.3f, 4f, 4f), new Keyframe(1f, 1f, 0f, 0f)));

            ParticleSystem core = CreateChild(flash.transform, "Core");
            SetUpSingle(core, material, 0.2f, 2.5f, ArcaneWhite);
            SetSize(core, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.3f)));

            ParticleSystem sparks = CreateChild(root.transform, "Sparks");
            sparks.transform.localPosition = new Vector3(0f, 1f, 0f);
            SetUpArcaneSparks(sparks, material);

            SavePrefab(root, ShockwavePrefabPath);
        }

        // 빛줄기 80개가 가운데에서 수평으로 튀어 나간다. 빠르기 × 수명이 대략 범위(6m)가 되게 맞춘다
        private static void SetUpArcaneSparks(ParticleSystem system, Material material)
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.duration = 0.1f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(14f, 20f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
            main.startColor = new ParticleSystem.MinMaxGradient(ArcanePink, ArcaneWhite);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 80) });

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f;
            shape.rotation = new Vector3(90f, 0f, 0f); // 원을 바닥에 눕혀서 수평으로 퍼지게
            shape.randomDirectionAmount = 0.15f;      // 조금씩 위아래로 흩어지게

            SetFade(system, 1f);
            SetSize(system, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f)));
            SetStreakRenderer(system, material);
        }

        // 반짝이는 가루 60개쯤. 0.3초 동안 범위 안 바닥 아무 데서나 생겨 천천히 떠오른다
        private static void SetUpMotes(ParticleSystem system, Material material)
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(ArcanePurple, ArcanePink);
            main.gravityModifier = -0.3f; // 음수면 위로 떠오르며 점점 빨라진다
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy; // 가장 오래 남으므로 끝나면 전체를 지운다

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 200f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = ShockwaveRadius;
            shape.rotation = new Vector3(90f, 0f, 0f); // 원을 바닥에 눕힌다

            SetFade(system, 0f);
            SetSize(system, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

            system.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        }

        // 자동 회피 이펙트 (잔상). 파티클이 아니라 DodgeAfterimage가 그 순간 플레이어 자세를 메시로 굳혀 그린다
        // 여기서는 잔상에 쓸 머티리얼·색·시간만 정해 둔다
        [MenuItem("Tools/Team Odd/Create Auto Dodge Effect")]
        private static void CreateAutoDodgeEffect()
        {
            Material material = CreateAdditiveMaterial(AutoDodgeMaterialPath);

            GameObject root = new GameObject("AutoDodgeEffect");
            SerializedObject afterimage = new SerializedObject(root.AddComponent<Player.DodgeAfterimage>());
            afterimage.FindProperty("material").objectReferenceValue = material;
            afterimage.FindProperty("color").colorValue = ReflexBlue;
            afterimage.FindProperty("duration").floatValue = AfterimageDuration;
            afterimage.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, AutoDodgePrefabPath);
        }

        // 수동 회피 이펙트 (허물). 파티클이 아니라 DodgeShell이 그 순간 플레이어 자세를 하얀 메시로 굳혀 그린다
        // 몸에서 무언가 터져 나오면 맞은 것처럼 보여서, 몸은 빠져나가고 껍데기만 남았다가 바스러지는 모양으로 "피해 냈다"를 보여 준다
        [MenuItem("Tools/Team Odd/Create Manual Dodge Effect")]
        private static void CreateManualDodgeEffect()
        {
            Material material = new Material(Shader.Find(DodgeShellShaderName));
            Directory.CreateDirectory(Path.GetDirectoryName(ManualDodgeMaterialPath));
            AssetDatabase.DeleteAsset(ManualDodgeMaterialPath);
            AssetDatabase.CreateAsset(material, ManualDodgeMaterialPath);

            GameObject root = new GameObject("ManualDodgeEffect");
            SerializedObject shell = new SerializedObject(root.AddComponent<Player.DodgeShell>());
            shell.FindProperty("material").objectReferenceValue = material;
            shell.FindProperty("holdDuration").floatValue = ShellHoldDuration;
            shell.FindProperty("dissolveDuration").floatValue = ShellDissolveDuration;
            shell.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, ManualDodgePrefabPath);
        }

        // 두 번째 심장 이펙트 (심장 박동). 일어서는 순간 플레이어 자식으로 붙어 따라다닌다 (SecondHeartRevival)
        // - Beat: 가슴 높이의 붉은 빛이 쿵, 쿵 두 번 부풀었다 줄어든다
        // - Shockwave: 두 번째 박동 때 발밑에서 붉은 빛 원이 바닥을 따라 퍼진다
        // - 맨 위(Embers): 붉은·주황 불씨가 몸 주위에서 위로 솟아오른다. 가장 오래 남아서 끝날 때 Stop Action으로 전체를 지운다
        [MenuItem("Tools/Team Odd/Create Second Heart Effect")]
        private static void CreateSecondHeartEffect()
        {
            Material material = CreateAdditiveMaterial(SecondHeartMaterialPath);

            GameObject root = new GameObject("SecondHeartEffect");
            SetUpEmbers(root.AddComponent<ParticleSystem>(), material);

            ParticleSystem beat = CreateChild(root.transform, "Beat");
            beat.transform.localPosition = new Vector3(0f, 1.1f, 0f); // 가슴 높이
            SetUpHeartBeat(beat, material);

            ParticleSystem shockwave = CreateChild(root.transform, "Shockwave");
            shockwave.transform.localPosition = new Vector3(0f, 0.05f, 0f); // 바닥에 겹쳐 깜빡이지 않게 살짝 띄운다
            SetUpShockwave(shockwave, material);

            SavePrefab(root, SecondHeartPrefabPath);
        }

        // 불씨 40개쯤. 1초 동안 몸 둘레에서 생겨 위로 솟는다
        private static void SetUpEmbers(ParticleSystem system, Material material)
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // 재생 중에는 duration을 바꿀 수 없다

            ParticleSystem.MainModule main = system.main;
            main.duration = 1f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(HeartRed, EmberOrange);
            main.gravityModifier = -0.5f; // 음수면 위로 떠오르며 점점 빨라진다
            main.simulationSpace = ParticleSystemSimulationSpace.Local; // 플레이어가 움직여도 따라다니게
            main.stopAction = ParticleSystemStopAction.Destroy;         // 다 끝나면 스스로 지운다

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 40f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.6f;
            shape.rotation = new Vector3(90f, 0f, 0f); // 원을 바닥에 눕힌다

            SetFade(system, 0f);
            SetSize(system, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

            system.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        }

        // 쿵, 쿵. 박동마다 빛 두 겹이 확 부풀었다가 조금 줄며 사라진다
        private static void SetUpHeartBeat(ParticleSystem system, Material material)
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.duration = SecondBeatTime + 0.1f;
            main.loop = false;
            main.startLifetime = 0.3f;
            main.startSpeed = 0f;
            main.startSize = 1.6f;
            main.startColor = HeartRed;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(FirstBeatTime, 2), new ParticleSystem.Burst(SecondBeatTime, 2) });

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false; // 제자리에 겹친다

            SetFade(system, 1f);
            SetSize(system, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.8f)));

            system.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        }

        // 두 번째 박동 때 바닥에 눕힌 빛 원이 지름 4m까지 퍼진다
        private static void SetUpShockwave(ParticleSystem system, Material material)
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.duration = SecondBeatTime + 0.1f;
            main.loop = false;
            main.startLifetime = 0.5f;
            main.startSpeed = 0f;
            main.startSize = 4f;
            main.startColor = new Color(HeartRed.r, HeartRed.g, HeartRed.b, 0.7f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(SecondBeatTime, 1) });

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            SetFade(system, 1f);
            SetSize(system, new AnimationCurve(new Keyframe(0f, 0.1f, 3f, 3f), new Keyframe(1f, 1f, 0f, 0f)));

            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard; // 카메라가 아니라 바닥을 보게 눕힌다
            renderer.sharedMaterial = material;
        }

        // 순간 이동 이펙트 (웜홀). 프리팹 하나에 자식 둘을 넣고, BlinkSkill이 자식을 하나씩 꺼내 띄운다
        // - Enter: 출발 자리. 주변 빛줄기가 빨려 들어가며 구체가 작아져 사라진다
        // - Exit: 도착 자리. 구체가 커지며 나타나고 빛줄기가 사방으로 튀어 나간다
        // 자식마다 맨 위 파티클이 가장 오래 남게 해서, 그 파티클이 끝날 때 Stop Action으로 자식 전체가 지워진다
        [MenuItem("Tools/Team Odd/Create Blink Effect")]
        private static void CreateBlinkEffect()
        {
            Material material = CreateAdditiveMaterial(BlinkMaterialPath);

            GameObject root = new GameObject("BlinkEffect");

            ParticleSystem enter = CreateChild(root.transform, "Enter");
            SetUpShrinkingSphere(enter, material);
            SetUpPullStreaks(CreateChild(enter.transform, "Pull"), material);

            ParticleSystem exit = CreateChild(root.transform, "Exit");
            SetUpBurstStreaks(exit, material);
            SetUpGrowingSphere(CreateChild(exit.transform, "Sphere"), material);

            SavePrefab(root, BlinkPrefabPath);
        }

        // 출발: 0.45초 동안 점점 빠르게 작아져 사라지는 구체. 마지막 빛줄기(0.2초에 나와 PullLifetime만큼 삶)보다 늦게 끝나야 한다
        private static void SetUpShrinkingSphere(ParticleSystem system, Material material)
        {
            SetUpSingle(system, material, 0.45f, SphereSize * BlinkScale, PaleViolet);

            ParticleSystem.MainModule main = system.main;
            main.stopAction = ParticleSystemStopAction.Destroy; // Enter에서 가장 오래 남으므로 끝나면 Enter 전체를 지운다

            SetSize(system, new AnimationCurve(new Keyframe(0f, 1f, 0f, 0f), new Keyframe(1f, 0f, -2f, -2f)));
        }

        // 출발: 구체 겉면에서 생긴 빛줄기가 가운데로 빨려 들어간다
        private static void SetUpPullStreaks(ParticleSystem system, Material material)
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = PullLifetime;
            // 음수면 바깥이 아니라 가운데로 날아간다. 사라질 때쯤 딱 가운데에 닿도록 거리 ÷ 시간으로 맞춘다
            main.startSpeed = -PullRadius * BlinkScale / PullLifetime;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f * BlinkScale, 0.08f * BlinkScale);
            main.startColor = new ParticleSystem.MinMaxGradient(Violet, SkyBlue);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 150f; // 0.2초 동안 30개쯤

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = PullRadius * BlinkScale;
            shape.radiusThickness = 0f; // 구 안쪽이 아니라 겉면에서만

            SetFade(system, 0f);
            SetStreakRenderer(system, material);
        }

        // 도착: 가운데에서 빛줄기가 사방으로 튀어 나갔다가 금방 멈춘다
        private static void SetUpBurstStreaks(ParticleSystem system, Material material)
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.duration = 0.1f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f * BlinkScale, 6f * BlinkScale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f * BlinkScale, 0.1f * BlinkScale);
            main.startColor = new ParticleSystem.MinMaxGradient(Violet, SkyBlue);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy; // Exit에서 가장 오래 남으므로 끝나면 Exit 전체를 지운다

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f * BlinkScale;

            // 튀어 나간 속도를 빠르게 줄여 "확 퍼졌다가 멈추는" 느낌을 낸다
            ParticleSystem.LimitVelocityOverLifetimeModule limit = system.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 0.5f * BlinkScale;
            limit.dampen = 0.15f;

            SetFade(system, 1f);
            SetStreakRenderer(system, material);
        }

        // 도착: 0.35초 동안 커지며 나타났다가 흐려지는 구체
        private static void SetUpGrowingSphere(ParticleSystem system, Material material)
        {
            SetUpSingle(system, material, 0.35f, SphereSize * BlinkScale, PaleViolet);
            SetSize(system, new AnimationCurve(new Keyframe(0f, 0f, 3f, 3f), new Keyframe(1f, 1f, 0f, 0f)));
        }

        // 제자리에 큰 빛을 SphereLayers겹 띄우는 파티클 (구체 역할)
        private static void SetUpSingle(ParticleSystem system, Material material, float lifetime, float size, Color color)
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // 재생 중에는 duration을 바꿀 수 없다

            ParticleSystem.MainModule main = system.main;
            main.duration = 0.1f;
            main.loop = false;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = size;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // 그 자리에 남는다

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, SphereLayers) });

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false; // 모두 같은 자리에 겹친다

            SetFade(system, 1f);
            system.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        }

        // 날아가는 방향으로 늘어나 보이게 그린다 (빛줄기)
        private static void SetStreakRenderer(ParticleSystem system, Material material)
        {
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.05f;
            renderer.lengthScale = 2f;
            renderer.sharedMaterial = material;
        }

        private static ParticleSystem CreateChild(Transform parent, string name)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.AddComponent<ParticleSystem>();
        }

        // 처음 투명도(startAlpha)에서 시작해 금방 다 보였다가, 끝으로 갈수록 투명해진다
        private static void SetFade(ParticleSystem system, float startAlpha)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });

            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            color.color = gradient;
        }

        // 살아 있는 동안 크기 배율이 curve를 따라 바뀐다
        private static void SetSize(ParticleSystem system, AnimationCurve curve)
        {
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private static void SavePrefab(GameObject root, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            Debug.Log($"[SkillEffectFactory] {path}를 만들었습니다.");
        }

        // URP 기본 파티클 머티리얼을 복사해 겹칠수록 밝아지는(Additive) 방식으로 바꾼다. 빛나는 느낌이 난다
        private static Material CreateAdditiveMaterial(string path)
        {
            Material material = new Material(AssetDatabase.LoadAssetAtPath<Material>(BaseMaterialPath));
            material.SetFloat("_Blend", 2f);    // URP 파티클 셰이더의 Additive
            material.SetFloat("_SrcBlend", 5f); // SrcAlpha
            material.SetFloat("_DstBlend", 1f); // One

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
