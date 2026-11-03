# 2. Final 씬 사용 파일 목록

대상 씬: `Assets/Scenes/10. Build/2. Final/*.unity` (01. Title, 02. Lobby, 03. Easy Boss, 04. Normal Boss, 05. Hard Boss)

수집 방법: 씬 → 프리팹/머티리얼/컨트롤러/에셋 등의 GUID 참조를 재귀 추적 (.meta 포함). 총 **1686개** 파일.

> 제외/주의: 코드에서 `Resources.Load`·경로 문자열로 로드하는 파일, 스크립트 간 코드 참조(직접 씬/프리팹에 붙지 않은 .cs)는 GUID로 추적되지 않아 빠져 있을 수 있음. 해석되지 않은 GUID(빠진 참조 등) 111개.

## 요약

| 상위 폴더 | 파일 수 |
|---|---|
| Assets/Animations | 381 |
| Assets/Audios | 1 |
| Assets/ExplosiveLLC | 15 |
| Assets/Font | 10 |
| Assets/Heavy Station Kit | 241 |
| Assets/Knife | 25 |
| Assets/Materials | 99 |
| Assets/Models | 256 |
| Assets/Physics Materials | 1 |
| Assets/Plugins | 2 |
| Assets/Prefabs | 212 |
| Assets/Resources | 1 |
| Assets/Scenes | 14 |
| Assets/Sci-Fi Base | 213 |
| Assets/Scripts | 150 |
| Assets/Settings | 1 |
| Assets/Shaders | 6 |
| Assets/VFX | 7 |
| Packages/com.unity.ai.navigation | 2 |
| Packages/com.unity.importer.usd | 5 |
| Packages/com.unity.inputsystem | 4 |
| Packages/com.unity.render-pipelines.core | 2 |
| Packages/com.unity.render-pipelines.universal | 15 |
| Packages/com.unity.shadergraph | 2 |
| Packages/com.unity.ugui | 21 |

## Assets/Animations

### Assets/Animations/1. Player (3)

- Player Animator.controller
- Psycokinesis Avatar Mask.mask
- RPG-Character@Armed-Shield-Death1.FBX

### Assets/Animations/1. Player/0. Basic (2)

- Anim_Idle.fbx
- RPG-Character.FBX

### Assets/Animations/1. Player/1. Movement (7)

- Anim_M_Walking.fbx
- Locomotion--Run_N.anim.fbx
- RPG-Character@Unarmed-DiveRoll-Forward1-ToIdle.FBX
- RPG-Character@Unarmed-Getup-Forward.FBX
- RPG-Character@Unarmed-Knockdown-Backward.FBX
- RPG-Character@Unarmed-Knockdown-Forward.FBX
- RPG-Character@Unarmed-Revive1.FBX

### Assets/Animations/1. Player/5. Death (1)

- RPG-Character@2Hand-Axe-Death1.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/0_Controller (1)

- Controller_M_Big_Sword.controller

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/1_Movements/0__Intro (1)

- M_Big_Sword@Intro.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/1_Movements/10__Move_to_Idle/Run_to_Idle (6)

- M_Big_Sword@Run_Fast_to_Idle_ver_A.FBX
- M_Big_Sword@Run_Fast_to_Idle_ver_A_Root.FBX
- M_Big_Sword@Run_Fast_to_Idle_ver_B.FBX
- M_Big_Sword@Run_Fast_to_Idle_ver_B_Root.FBX
- M_Big_Sword@Run_to_Idle_ver_A.FBX
- M_Big_Sword@Run_to_Idle_ver_B.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/1_Movements/10__Move_to_Idle/Walk_to_Idle (4)

- M_Big_Sword@Walk_To_Idle_ver_A.FBX
- M_Big_Sword@Walk_To_Idle_ver_A_Root.FBX
- M_Big_Sword@Walk_To_Idle_ver_B.FBX
- M_Big_Sword@Walk_To_Idle_ver_B_Root.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/1_Movements/11__Run_To_Fast_Run (2)

- M_Big_Sword@Run_to_Fast_Run_ver_A.FBX
- M_Big_Sword@Run_to_Fast_Run_ver_B.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/1_Movements/12__Turn_in_place/Inplace (56)

- M_Big_Sword@Idle_To_Jogging_A_Turn_L90.FBX
- M_Big_Sword@Idle_To_Jogging_A_Turn_R90.FBX
- M_Big_Sword@Idle_To_Jogging_B_Turn_L90.FBX
- M_Big_Sword@Idle_To_Jogging_B_Turn_R90.FBX
- M_Big_Sword@Idle_To_Run_A_Turn_L90.FBX
- M_Big_Sword@Idle_To_Run_A_Turn_R90.FBX
- M_Big_Sword@Idle_To_Run_B_Turn_L90.FBX
- M_Big_Sword@Idle_To_Run_B_Turn_R90.FBX
- M_Big_Sword@Idle_To_Walk_A_Turn_L90.FBX
- M_Big_Sword@Idle_To_Walk_A_Turn_R90.FBX
- M_Big_Sword@Idle_To_Walk_B_Turn_L90.FBX
- M_Big_Sword@Idle_To_Walk_B_Turn_R90.FBX
- M_Big_Sword@Idle_Turn_L90.FBX
- M_Big_Sword@Idle_Turn_R90.FBX
- M_Big_Sword@Jogging_A_To_Jogging_A_Turn_L90.FBX
- M_Big_Sword@Jogging_A_To_Jogging_A_Turn_R90.FBX
- M_Big_Sword@Jogging_B_To_Jogging_B_Turn_L90.FBX
- M_Big_Sword@Jogging_B_To_Jogging_B_Turn_R90.FBX
- M_Big_Sword@Turn_in_place_Idle_to_Jog_A.FBX
- M_Big_Sword@Turn_in_place_Idle_to_Jog_B.FBX
- M_Big_Sword@Turn_in_place_Idle_to_Run_A.FBX
- M_Big_Sword@Turn_in_place_Idle_to_Run_B.FBX
- M_Big_Sword@Turn_in_place_Idle_to_Walk_A.FBX
- M_Big_Sword@Turn_in_place_Idle_to_Walk_B.FBX
- M_Big_Sword@Turn_in_place_Jog_A_to_Jog_A.FBX
- M_Big_Sword@Turn_in_place_Jog_A_to_Jog_B.FBX
- M_Big_Sword@Turn_in_place_Jog_A_to_Run_A.FBX
- M_Big_Sword@Turn_in_place_Jog_A_to_Run_B.FBX
- M_Big_Sword@Turn_in_place_Jog_B_to_Jog_A.FBX
- M_Big_Sword@Turn_in_place_Jog_B_to_Jog_B.FBX
- M_Big_Sword@Turn_in_place_Jog_B_to_Run_A.FBX
- M_Big_Sword@Turn_in_place_Jog_B_to_Run_B.FBX
- M_Big_Sword@Turn_in_place_Run_A_to_Run_A.FBX
- M_Big_Sword@Turn_in_place_Run_A_to_Run_B.FBX
- M_Big_Sword@Turn_in_place_Run_B_to_Run_A.FBX
- M_Big_Sword@Turn_in_place_Run_B_to_Run_B.FBX
- M_Big_Sword@Turn_in_place_Walk_A_to_Jog_A.FBX
- M_Big_Sword@Turn_in_place_Walk_A_to_Jog_B.FBX
- M_Big_Sword@Turn_in_place_Walk_A_to_Run_A.FBX
- M_Big_Sword@Turn_in_place_Walk_A_to_Run_B.FBX
- M_Big_Sword@Turn_in_place_Walk_A_to_Walk_A.FBX
- M_Big_Sword@Turn_in_place_Walk_A_to_Walk_B.FBX
- M_Big_Sword@Turn_in_place_Walk_B_to_Jog_A.FBX
- M_Big_Sword@Turn_in_place_Walk_B_to_Jog_B.FBX
- M_Big_Sword@Turn_in_place_Walk_B_to_Run_A.FBX
- M_Big_Sword@Turn_in_place_Walk_B_to_Run_B.FBX
- M_Big_Sword@Turn_in_place_Walk_B_to_Walk_A.FBX
- M_Big_Sword@Turn_in_place_Walk_B_to_Walk_B.FBX
- M_Big_Sword@Walk_A_To_Idle_Turn_L90.FBX
- M_Big_Sword@Walk_A_To_Idle_Turn_R90.FBX
- M_Big_Sword@Walk_A_To_Walk_A_Turn_L90.FBX
- M_Big_Sword@Walk_A_To_Walk_A_Turn_R90.FBX
- M_Big_Sword@Walk_B_To_Idle_Turn_L90.FBX
- M_Big_Sword@Walk_B_To_Idle_Turn_R90.FBX
- M_Big_Sword@Walk_B_To_Walk_B_Turn_L90.FBX
- M_Big_Sword@Walk_B_To_Walk_B_Turn_R90.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/1_Movements/12__Turn_in_place/RootMotion (26)

- M_Big_Sword@Idle_To_Jogging_A_Turn_L90_Root.FBX
- M_Big_Sword@Idle_To_Jogging_A_Turn_R90_Root.FBX
- M_Big_Sword@Idle_To_Jogging_B_Turn_L90_Root.FBX
- M_Big_Sword@Idle_To_Jogging_B_Turn_R90_Root.FBX
- M_Big_Sword@Idle_To_Run_A_Turn_L90_Root.FBX
- M_Big_Sword@Idle_To_Run_A_Turn_R90_Root.FBX
- M_Big_Sword@Idle_To_Run_B_Turn_L90_Root.FBX
- M_Big_Sword@Idle_To_Run_B_Turn_R90_Root.FBX
- M_Big_Sword@Idle_To_Walk_A_Turn_L90_Root.FBX
- M_Big_Sword@Idle_To_Walk_A_Turn_R90_Root.FBX
- M_Big_Sword@Idle_To_Walk_B_Turn_L90_Root.FBX
- M_Big_Sword@Idle_To_Walk_B_Turn_R90_Root.FBX
- M_Big_Sword@Idle_Turn_L90_Root.FBX
- M_Big_Sword@Idle_Turn_R90_Root.FBX
- M_Big_Sword@Turn_in_place_Idle_to_Run_A_Root.FBX
- M_Big_Sword@Turn_in_place_Idle_to_Run_B_Root.FBX
- M_Big_Sword@Turn_in_place_Idle_to_Walk_A_Root.FBX
- M_Big_Sword@Turn_in_place_Idle_to_Walk_B_Root.FBX
- M_Big_Sword@Walk_A_To_Idle_Turn_L90_Root.FBX
- M_Big_Sword@Walk_A_To_Idle_Turn_R90_Root.FBX
- M_Big_Sword@Walk_A_To_Walk_A_Turn_L90_Root.FBX
- M_Big_Sword@Walk_A_To_Walk_A_Turn_R90_Root.FBX
- M_Big_Sword@Walk_B_To_Idle_Turn_L90_Root.FBX
- M_Big_Sword@Walk_B_To_Idle_Turn_R90_Root.FBX
- M_Big_Sword@Walk_B_To_Walk_B_Turn_L90_Root.FBX
- M_Big_Sword@Walk_B_To_Walk_B_Turn_R90_Root.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/1_Movements/17__Idle_To_8Way_Walk_ALL (40)

- M_Big_Sword@Idle_A_To_Walk_BL45.FBX
- M_Big_Sword@Idle_A_To_Walk_BL45_Root.FBX
- M_Big_Sword@Idle_A_To_Walk_BL90.FBX
- M_Big_Sword@Idle_A_To_Walk_BL90_Root.FBX
- M_Big_Sword@Idle_A_To_Walk_BR45.FBX
- M_Big_Sword@Idle_A_To_Walk_BR45_Root.FBX
- M_Big_Sword@Idle_A_To_Walk_BR90.FBX
- M_Big_Sword@Idle_A_To_Walk_BR90_Root.FBX
- M_Big_Sword@Idle_A_To_Walk_Back.FBX
- M_Big_Sword@Idle_A_To_Walk_Back_Root.FBX
- M_Big_Sword@Idle_A_To_Walk_FL45.FBX
- M_Big_Sword@Idle_A_To_Walk_FL45_Root.FBX
- M_Big_Sword@Idle_A_To_Walk_FL90.FBX
- M_Big_Sword@Idle_A_To_Walk_FL90_Root.FBX
- M_Big_Sword@Idle_A_To_Walk_FR45.FBX
- M_Big_Sword@Idle_A_To_Walk_FR45_Root.FBX
- M_Big_Sword@Idle_A_To_Walk_FR90.FBX
- M_Big_Sword@Idle_A_To_Walk_FR90_Root.FBX
- M_Big_Sword@Idle_A_To_Walk_Front.FBX
- M_Big_Sword@Idle_A_To_Walk_Front_Root.FBX
- M_Big_Sword@Idle_B_To_Walk_BL45.FBX
- M_Big_Sword@Idle_B_To_Walk_BL45_Root.FBX
- M_Big_Sword@Idle_B_To_Walk_BL90.FBX
- M_Big_Sword@Idle_B_To_Walk_BL90_Root.FBX
- M_Big_Sword@Idle_B_To_Walk_BR45.FBX
- M_Big_Sword@Idle_B_To_Walk_BR45_Root.FBX
- M_Big_Sword@Idle_B_To_Walk_BR90.FBX
- M_Big_Sword@Idle_B_To_Walk_BR90_Root.FBX
- M_Big_Sword@Idle_B_To_Walk_Back.FBX
- M_Big_Sword@Idle_B_To_Walk_Back_Root.FBX
- M_Big_Sword@Idle_B_To_Walk_FL45.FBX
- M_Big_Sword@Idle_B_To_Walk_FL45_Root.FBX
- M_Big_Sword@Idle_B_To_Walk_FL90.FBX
- M_Big_Sword@Idle_B_To_Walk_FL90_Root.FBX
- M_Big_Sword@Idle_B_To_Walk_FR45.FBX
- M_Big_Sword@Idle_B_To_Walk_FR45_Root.FBX
- M_Big_Sword@Idle_B_To_Walk_FR90.FBX
- M_Big_Sword@Idle_B_To_Walk_FR90_Root.FBX
- M_Big_Sword@Idle_B_To_Walk_Front.FBX
- M_Big_Sword@Idle_B_To_Walk_Front_Root.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/1_Movements/1__Idle (3)

- M_Big_Sword@Idle.FBX
- M_Big_Sword@Idle_Turn.FBX
- M_Big_Sword@Idle_Turn_Root.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/1_Movements/2__Walk/A (24)

- M_Big_Sword@Walk_ver_A_Back.FBX
- M_Big_Sword@Walk_ver_A_Back_L45.FBX
- M_Big_Sword@Walk_ver_A_Back_L45_Root.FBX
- M_Big_Sword@Walk_ver_A_Back_L90.FBX
- M_Big_Sword@Walk_ver_A_Back_L90_Root.FBX
- M_Big_Sword@Walk_ver_A_Back_R45.FBX
- M_Big_Sword@Walk_ver_A_Back_R45_Root.FBX
- M_Big_Sword@Walk_ver_A_Back_R90.FBX
- M_Big_Sword@Walk_ver_A_Back_R90_Root.FBX
- M_Big_Sword@Walk_ver_A_Back_Root.FBX
- M_Big_Sword@Walk_ver_A_Front.FBX
- M_Big_Sword@Walk_ver_A_Front_L45.FBX
- M_Big_Sword@Walk_ver_A_Front_L45_Root.FBX
- M_Big_Sword@Walk_ver_A_Front_L90.FBX
- M_Big_Sword@Walk_ver_A_Front_L90_Root.FBX
- M_Big_Sword@Walk_ver_A_Front_R45.FBX
- M_Big_Sword@Walk_ver_A_Front_R45_Root.FBX
- M_Big_Sword@Walk_ver_A_Front_R90.FBX
- M_Big_Sword@Walk_ver_A_Front_R90_Root.FBX
- M_Big_Sword@Walk_ver_A_Front_Root.FBX
- M_Big_Sword@Walk_ver_A_L90.FBX
- M_Big_Sword@Walk_ver_A_L90_Root.FBX
- M_Big_Sword@Walk_ver_A_R90.FBX
- M_Big_Sword@Walk_ver_A_R90_Root.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/1_Movements/2__Walk/B (24)

- M_Big_Sword@Walk_ver_B_Back.FBX
- M_Big_Sword@Walk_ver_B_Back_L45.FBX
- M_Big_Sword@Walk_ver_B_Back_L45_Root.FBX
- M_Big_Sword@Walk_ver_B_Back_L90.FBX
- M_Big_Sword@Walk_ver_B_Back_L90_Root.FBX
- M_Big_Sword@Walk_ver_B_Back_R45.FBX
- M_Big_Sword@Walk_ver_B_Back_R45_Root.FBX
- M_Big_Sword@Walk_ver_B_Back_R90.FBX
- M_Big_Sword@Walk_ver_B_Back_R90_Root.FBX
- M_Big_Sword@Walk_ver_B_Back_Root.FBX
- M_Big_Sword@Walk_ver_B_Front.FBX
- M_Big_Sword@Walk_ver_B_Front_L45.FBX
- M_Big_Sword@Walk_ver_B_Front_L45_Root.FBX
- M_Big_Sword@Walk_ver_B_Front_L90.FBX
- M_Big_Sword@Walk_ver_B_Front_L90_Root.FBX
- M_Big_Sword@Walk_ver_B_Front_R45.FBX
- M_Big_Sword@Walk_ver_B_Front_R45_Root.FBX
- M_Big_Sword@Walk_ver_B_Front_R90.FBX
- M_Big_Sword@Walk_ver_B_Front_R90_Root.FBX
- M_Big_Sword@Walk_ver_B_Front_Root.FBX
- M_Big_Sword@Walk_ver_B_L90.FBX
- M_Big_Sword@Walk_ver_B_L90_Root.FBX
- M_Big_Sword@Walk_ver_B_R90.FBX
- M_Big_Sword@Walk_ver_B_R90_Root.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/1_Movements/3__Jogging/A (19)

- M_Big_Sword@Jogging_8Way_verA_B.FBX
- M_Big_Sword@Jogging_8Way_verA_BL45.FBX
- M_Big_Sword@Jogging_8Way_verA_BL45_Root.FBX
- M_Big_Sword@Jogging_8Way_verA_BR45.FBX
- M_Big_Sword@Jogging_8Way_verA_B_Root.FBX
- M_Big_Sword@Jogging_8Way_verA_F.FBX
- M_Big_Sword@Jogging_8Way_verA_FL45.FBX
- M_Big_Sword@Jogging_8Way_verA_FL45_Root.FBX
- M_Big_Sword@Jogging_8Way_verA_FR45.FBX
- M_Big_Sword@Jogging_8Way_verA_FR45_Root.FBX
- M_Big_Sword@Jogging_8Way_verA_F_Root.FBX
- M_Big_Sword@Jogging_8Way_verA_L90.FBX
- M_Big_Sword@Jogging_8Way_verA_L90_Root.FBX
- M_Big_Sword@Jogging_8Way_verA_L90_Root_vol2.FBX
- M_Big_Sword@Jogging_8Way_verA_L90_vol2.FBX
- M_Big_Sword@Jogging_8Way_verA_R90.FBX
- M_Big_Sword@Jogging_8Way_verA_R90_Root.FBX
- M_Big_Sword@Jogging_8Way_verA_R90_Root_vol2.FBX
- M_Big_Sword@Jogging_8Way_verA_R90_vol2.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/1_Movements/3__Jogging/B (20)

- M_Big_Sword@Jogging_8Way_verB_B.FBX
- M_Big_Sword@Jogging_8Way_verB_BL45.FBX
- M_Big_Sword@Jogging_8Way_verB_BL45_Root.FBX
- M_Big_Sword@Jogging_8Way_verB_BR45.FBX
- M_Big_Sword@Jogging_8Way_verB_BR45_Root.FBX
- M_Big_Sword@Jogging_8Way_verB_B_Root.FBX
- M_Big_Sword@Jogging_8Way_verB_F.FBX
- M_Big_Sword@Jogging_8Way_verB_FL45.FBX
- M_Big_Sword@Jogging_8Way_verB_FL45_Root.FBX
- M_Big_Sword@Jogging_8Way_verB_FR45.FBX
- M_Big_Sword@Jogging_8Way_verB_FR45_Root.FBX
- M_Big_Sword@Jogging_8Way_verB_F_Root.FBX
- M_Big_Sword@Jogging_8Way_verB_L90.FBX
- M_Big_Sword@Jogging_8Way_verB_L90_Root.FBX
- M_Big_Sword@Jogging_8Way_verB_L90_Root_vol2.FBX
- M_Big_Sword@Jogging_8Way_verB_L90_vol2.FBX
- M_Big_Sword@Jogging_8Way_verB_R90.FBX
- M_Big_Sword@Jogging_8Way_verB_R90_Root.FBX
- M_Big_Sword@Jogging_8Way_verB_R90_Root_vol2.FBX
- M_Big_Sword@Jogging_8Way_verB_R90_vol2.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/1_Movements/4__Run (8)

- M_Big_Sword@Run_Fast_ver_A.FBX
- M_Big_Sword@Run_Fast_ver_A_Root.FBX
- M_Big_Sword@Run_Fast_ver_B.FBX
- M_Big_Sword@Run_Fast_ver_B_Root.FBX
- M_Big_Sword@Run_ver_A.FBX
- M_Big_Sword@Run_ver_A_Root.FBX
- M_Big_Sword@Run_ver_B.FBX
- M_Big_Sword@Run_ver_B_Root.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/1_Movements/6__Jump (6)

- M_Big_Sword@Jump_ALL.FBX
- M_Big_Sword@Jump_ALL_ZeroHeight.FBX
- M_Big_Sword@Jump_End.FBX
- M_Big_Sword@Jump_Loop.FBX
- M_Big_Sword@Jump_Loop_ZeroHeight.FBX
- M_Big_Sword@Jump_Start.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/1_Movements/8__Dodge (4)

- M_Big_Sword@Dodge_Back.FBX
- M_Big_Sword@Dodge_Front.FBX
- M_Big_Sword@Dodge_Left.FBX
- M_Big_Sword@Dodge_Right.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/1_Movements/9__Idle_To_Move (12)

- M_Big_Sword@Idle_To_Jogging_ver_A.FBX
- M_Big_Sword@Idle_To_Jogging_ver_A_Root.FBX
- M_Big_Sword@Idle_To_Jogging_ver_B.FBX
- M_Big_Sword@Idle_To_Jogging_ver_B_Root.FBX
- M_Big_Sword@Idle_To_Run_ver_A.FBX
- M_Big_Sword@Idle_To_Run_ver_A_Root.FBX
- M_Big_Sword@Idle_To_Run_ver_B.FBX
- M_Big_Sword@Idle_To_Run_ver_B_Root.FBX
- M_Big_Sword@Idle_To_Walk_ver_A.FBX
- M_Big_Sword@Idle_To_Walk_ver_A_Root.FBX
- M_Big_Sword@Idle_To_Walk_ver_B.FBX
- M_Big_Sword@Idle_To_Walk_ver_B_Root.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/2_Attacks/0__3Combos (8)

- M_Big_Sword@Attack_3Combo_1.FBX
- M_Big_Sword@Attack_3Combo_1_Inplace.FBX
- M_Big_Sword@Attack_3Combo_2.FBX
- M_Big_Sword@Attack_3Combo_2_Inplace.FBX
- M_Big_Sword@Attack_3Combo_3.FBX
- M_Big_Sword@Attack_3Combo_3_Inplace.FBX
- M_Big_Sword@Attack_3Combo_ALL.FBX
- M_Big_Sword@Attack_3Combo_ALL_Inplace.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/2_Attacks/1__4Combos (14)

- M_Big_Sword@Attack_4Combo_1A.FBX
- M_Big_Sword@Attack_4Combo_1A_Inplace.FBX
- M_Big_Sword@Attack_4Combo_1B.FBX
- M_Big_Sword@Attack_4Combo_1B_Inplace.FBX
- M_Big_Sword@Attack_4Combo_2.FBX
- M_Big_Sword@Attack_4Combo_2_Inplace.FBX
- M_Big_Sword@Attack_4Combo_3.FBX
- M_Big_Sword@Attack_4Combo_3_Inplace.FBX
- M_Big_Sword@Attack_4Combo_4.FBX
- M_Big_Sword@Attack_4Combo_4_Inplace.FBX
- M_Big_Sword@Attack_4Combo_A_ALL.FBX
- M_Big_Sword@Attack_4Combo_A_ALL_Inplace.FBX
- M_Big_Sword@Attack_4Combo_B_ALL.FBX
- M_Big_Sword@Attack_4Combo_B_ALL_Inplace.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/2_Attacks/2__7Combos (16)

- M_Big_Sword@Attack_7Combo_1.FBX
- M_Big_Sword@Attack_7Combo_1_Inplace.FBX
- M_Big_Sword@Attack_7Combo_2.FBX
- M_Big_Sword@Attack_7Combo_2_Inplace.FBX
- M_Big_Sword@Attack_7Combo_3.FBX
- M_Big_Sword@Attack_7Combo_3_Inplace.FBX
- M_Big_Sword@Attack_7Combo_4.FBX
- M_Big_Sword@Attack_7Combo_4_Inplace.FBX
- M_Big_Sword@Attack_7Combo_5.FBX
- M_Big_Sword@Attack_7Combo_5_Inplace.FBX
- M_Big_Sword@Attack_7Combo_6.FBX
- M_Big_Sword@Attack_7Combo_6_Inplace.FBX
- M_Big_Sword@Attack_7Combo_7.FBX
- M_Big_Sword@Attack_7Combo_7_Inplace.FBX
- M_Big_Sword@Attack_7Combo_ALL.FBX
- M_Big_Sword@Attack_7Combo_ALL_Inplace.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/2_Attacks/3__Dash_Attack (2)

- M_Big_Sword@Dash_Attack_ver_A.FBX
- M_Big_Sword@Dash_Attack_ver_B.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/2_Attacks/4__Jump_Attack (8)

- M_Big_Sword@Jump_Attack_Combo_1.FBX
- M_Big_Sword@Jump_Attack_Combo_1_ZeroHeight.FBX
- M_Big_Sword@Jump_Attack_Combo_2.FBX
- M_Big_Sword@Jump_Attack_Combo_2_ZeroHeight.FBX
- M_Big_Sword@Jump_Attack_Combo_3.FBX
- M_Big_Sword@Jump_Attack_Combo_3_ZeroHeight.FBX
- M_Big_Sword@Jump_Attack_Combo_ALL.FBX
- M_Big_Sword@Jump_Attack_Combo_ALL_ZeroHeight.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/2_Attacks/5__Upper_Attack (2)

- M_Big_Sword@UpperAttack.FBX
- M_Big_Sword@UpperAttack_ZeroHeight.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/3_Skills (15)

- M_Big_Sword@Skill_A.FBX
- M_Big_Sword@Skill_B.FBX
- M_Big_Sword@Skill_C.FBX
- M_Big_Sword@Skill_D.FBX
- M_Big_Sword@Skill_E.FBX
- M_Big_Sword@Skill_F.FBX
- M_Big_Sword@Skill_G_1.FBX
- M_Big_Sword@Skill_G_2.FBX
- M_Big_Sword@Skill_G_ALL.FBX
- M_Big_Sword@Skill_H.FBX
- M_Big_Sword@Skill_I.FBX
- M_Big_Sword@Skill_J.FBX
- M_Big_Sword@Skill_J_Inplace.FBX
- M_Big_Sword@Skill_K.FBX
- M_Big_Sword@Skill_L.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/4_Damages/1__Front (12)

- M_Big_Sword@Damage_Front_Big_ver_A.FBX
- M_Big_Sword@Damage_Front_Big_ver_B.FBX
- M_Big_Sword@Damage_Front_Down_StandUp.FBX
- M_Big_Sword@Damage_Front_Flying_ver_A.FBX
- M_Big_Sword@Damage_Front_Flying_ver_A_ZeroHeight.FBX
- M_Big_Sword@Damage_Front_Flying_ver_B.FBX
- M_Big_Sword@Damage_Front_Flying_ver_B_ZeroHeight.FBX
- M_Big_Sword@Damage_Front_High_KnockDown.FBX
- M_Big_Sword@Damage_Front_High_KnockDown_ZeroHeight.FBX
- M_Big_Sword@Damage_Front_Small_ver_A.FBX
- M_Big_Sword@Damage_Front_Small_ver_B.FBX
- M_Big_Sword@Damage_Front_Small_ver_C.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/4_Damages/2__Back (4)

- M_Big_Sword@Damage_Back_Down.FBX
- M_Big_Sword@Damage_Back_Down_StandUp.FBX
- M_Big_Sword@Damage_Back_Small_ver_A.FBX
- M_Big_Sword@Damage_Back_Small_ver_B.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/4_Damages/3__Left (2)

- M_Big_Sword@Damage_Left_Small_ver_A.FBX
- M_Big_Sword@Damage_Left_Small_ver_B.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/4_Damages/4__Right (2)

- M_Big_Sword@Damage_Right_Small_ver_A.FBX
- M_Big_Sword@Damage_Right_Small_ver_B.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/4_Damages/5__KnockDown (14)

- M_Big_Sword@KnockDown_Back_BW_Rolling_StandUp.FBX
- M_Big_Sword@KnockDown_Back_Big.FBX
- M_Big_Sword@KnockDown_Back_Damage.FBX
- M_Big_Sword@KnockDown_Back_FW_Rolling_StandUp.FBX
- M_Big_Sword@KnockDown_Back_ver_A.FBX
- M_Big_Sword@KnockDown_Back_ver_B.FBX
- M_Big_Sword@KnockDown_Front_BW_Rolling_StandUp.FBX
- M_Big_Sword@KnockDown_Front_Big.FBX
- M_Big_Sword@KnockDown_Front_Damage.FBX
- M_Big_Sword@KnockDown_Front_FW_Rolling_StandUp.FBX
- M_Big_Sword@KnockDown_Front_Loop.FBX
- M_Big_Sword@KnockDown_Front_Smash_Double.FBX
- M_Big_Sword@KnockDown_Front_Spear_Inplace.FBX
- M_Big_Sword@KnockDown_Front_ver_A.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/4_Damages/6__Die (2)

- M_Big_Sword@Damage_Die.FBX
- M_Big_Sword@Damage_Die_Loop.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/5_Revenges/Guard_Revenges (6)

- M_Big_Sword@Revenge_Guard_ALL.FBX
- M_Big_Sword@Revenge_Guard_Accept.FBX
- M_Big_Sword@Revenge_Guard_Attack.FBX
- M_Big_Sword@Revenge_Guard_End.FBX
- M_Big_Sword@Revenge_Guard_Loop.FBX
- M_Big_Sword@Revenge_Guard_Start.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Animation/M_Big_Sword/5_Revenges/StandUp_Revenge (1)

- M_Big_Sword@StandUp_Revenge.FBX

### Assets/Animations/Grruzam Powerful Sword Animation/Modeling (3)

- Modeling_T-Pose_Grrrru_Man(recommend).FBX
- Unity_Grruzam_BaseModeling_no_weapon.FBX
- Weapon_Avatar Mask.mask

### Assets/Animations/Humanoid Boss (1)

- Humanoid Boss.controller

## Assets/Audios

### Assets/Audios (1)

- SK_Charcharosaurus.prefab

## Assets/ExplosiveLLC

### Assets/ExplosiveLLC/PerfectLookAt/Animations (3)

- SimpleAnimationController.controller
- run.FBX
- walk.FBX

### Assets/ExplosiveLLC/PerfectLookAt/Code (1)

- PerfectLookAt.cs

### Assets/ExplosiveLLC/PerfectLookAt/Models/Human (1)

- inverse.FBX

### Assets/ExplosiveLLC/PerfectLookAt/Models/Robot Kyle/Materials (1)

- Robot_Color.mat

### Assets/ExplosiveLLC/PerfectLookAt/Models/Robot Kyle/Model (1)

- Robot Kyle.fbx

### Assets/ExplosiveLLC/PerfectLookAt/Models/Robot Kyle/Textures (2)

- Robot_Color.tga
- Robot_Normal.tga

### Assets/ExplosiveLLC/PerfectLookAt/Models/UnityModel (1)

- DefaultAvatar.fbx

### Assets/ExplosiveLLC/PerfectLookAt/Prefabs (1)

- RobotKyle.prefab

### Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack/Animations/2Hand-Staff (1)

- RPG-Character@2Hand-Staff-Cast-Attack3.fbx

### Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack/Animations/Unarmed (3)

- RPG-Character@Unarmed-Cast-Dual-Attack1.FBX
- RPG-Character@Unarmed-GetHit-F1.FBX
- RPG-Character@Unarmed-Knockback-Back1.FBX

## Assets/Font

### Assets/Font/Noto_Sans_KR/static (2)

- NotoSansKR-Regular SDF.asset
- NotoSansKR-Regular.ttf

### Assets/Font/Orbit (2)

- Orbit SDF.asset
- Orbit-Regular.ttf

### Assets/Font/Pretendard (4)

- Pretendard-Black SDF.asset
- Pretendard-Medium SDF.asset
- Pretendard-Regular SDF.asset
- Pretendard-SemiBold SDF.asset

### Assets/Font/aquire-font (2)

- Aquire SDF.asset
- Aquire-BW0ox.otf

## Assets/Heavy Station Kit

### Assets/Heavy Station Kit/HANGARS/Materials/Aggregates (4)

- H2_Aggregates_0.mat
- H2_Aggregates_2.mat
- H2_Aggregates_3.mat
- H2_Aggregates_4.mat

### Assets/Heavy Station Kit/HANGARS/Materials/Displays (4)

- H2_Dis_Door.mat
- H2_Disp_Cons1.mat
- H2_Disp_Cons1D.mat
- H2_Disp_Cons2.mat

### Assets/Heavy Station Kit/HANGARS/Materials/Doors (1)

- Door2_Red.mat

### Assets/Heavy Station Kit/HANGARS/Materials/Floors (4)

- H2_Floors.mat
- H2_FloorsGlass.mat
- H2_FloorsSolid.mat
- H2_Floors_NoL.mat

### Assets/Heavy Station Kit/HANGARS/Materials/Gateway (2)

- H2_Gateway.mat
- H2_Shield.mat

### Assets/Heavy Station Kit/HANGARS/Materials/Hangar Sky (1)

- Dot_sky hangar.mat

### Assets/Heavy Station Kit/HANGARS/Materials/Outside (1)

- H2_Outside.mat

### Assets/Heavy Station Kit/HANGARS/Materials/Props (1)

- H2_Props_0.mat

### Assets/Heavy Station Kit/HANGARS/Materials/Supports (1)

- H2_Supp_Doors_0.mat

### Assets/Heavy Station Kit/HANGARS/Materials/Top Bottom (2)

- H2_TB.mat
- H2_TB_A.mat

### Assets/Heavy Station Kit/HANGARS/Materials/Top-Down (1)

- H2_Top-Down.mat

### Assets/Heavy Station Kit/HANGARS/Materials/Walls (7)

- H2_Arches.mat
- H2_Arches_T.mat
- H2_Walls1_1.mat
- H2_Walls1_2.mat
- H2_Walls1_5.mat
- H2_Walls2_1.mat
- H2_Walls2_5.mat

### Assets/Heavy Station Kit/HANGARS/Meshes/Aggregates (8)

- agg_1.fbx
- agg_balloon.fbx
- agg_balloon_1.fbx
- agg_balloon_3.fbx
- agg_cargocase.fbx
- agg_console1.fbx
- agg_console2.fbx
- agg_reactor.fbx

### Assets/Heavy Station Kit/HANGARS/Meshes/Arches (7)

- arche_1a.fbx
- arche_1a_c.fbx
- arche_2a.fbx
- arche_3.fbx
- arche_3_c.fbx
- arche_3b.fbx
- arche_3b_c.fbx

### Assets/Heavy Station Kit/HANGARS/Meshes/Displays (3)

- H2_Cons1.fbx
- H2_Cons1_D.fbx
- H2_Dis_Doors.fbx

### Assets/Heavy Station Kit/HANGARS/Meshes/Doors (4)

- Gate.fbx
- door_0.fbx
- door_0_c.fbx
- door_3_glass.fbx

### Assets/Heavy Station Kit/HANGARS/Meshes/Floors (19)

- H2_floor_6A.fbx
- floor_0h.fbx
- floor_0h_Col.fbx
- floor_1h.fbx
- floor_1h_Col.fbx
- floor_1h_F.fbx
- floor_1h_Glass.fbx
- floor_1h_Glass_one.fbx
- floor_1h_fill.fbx
- floor_1h_fill_Col.fbx
- floor_1h_fill_F.fbx
- floor_1h_fill_one.fbx
- floor_1h_fill_one_Col.fbx
- floor_1h_fill_one_F.fbx
- floor_4h.fbx
- handrail_3.fbx
- handrail_3_Col.fbx
- handrail_4.fbx
- handrail_4_Col.fbx

### Assets/Heavy Station Kit/HANGARS/Meshes/Gateway (7)

- GW_gate1.fbx
- GW_gate2.fbx
- Gateway1.fbx
- Gateway1_Col.fbx
- Gateway2.fbx
- Gateway2_Col.fbx
- Shield.fbx

### Assets/Heavy Station Kit/HANGARS/Meshes/Outside (1)

- H2_Out_4.fbx

### Assets/Heavy Station Kit/HANGARS/Meshes/Props (7)

- hangar_barrel1.fbx
- hangar_barrel1_Coll.fbx
- hangar_barrel2.fbx
- hangar_barrel2_Coll.fbx
- hangar_battery1.fbx
- hangar_box2.fbx
- hangar_box3.fbx

### Assets/Heavy Station Kit/HANGARS/Meshes/Supports (5)

- H2_Cel_2.fbx
- H2_Cel_7.fbx
- H2_Cel_8.fbx
- H2_Sup_4.fbx
- H2_Sup_4_c.fbx

### Assets/Heavy Station Kit/HANGARS/Meshes/Top Bottom (5)

- H2_TB_Cover.fbx
- H2_TB_Cover_C.fbx
- TB_II_1.fbx
- TB_II_2.fbx
- TB_II_h_Coll.fbx

### Assets/Heavy Station Kit/HANGARS/Meshes/Top-Down (10)

- TD_hangar_ARCH0.fbx
- TD_hangar_OW1A.fbx
- TD_hangar_OW1B.fbx
- TD_hangar_OW1_c.fbx
- TD_hangar_OW2.fbx
- TD_hangar_OW2_c.fbx
- TD_hangar_OW4.fbx
- TD_hangar_OW4_c.fbx
- TD_hangar_TW0.fbx
- TD_hangar_TW0a.fbx

### Assets/Heavy Station Kit/HANGARS/Meshes/Walls (21)

- Glass_Arch_1.fbx
- Glass_Arch_4.fbx
- Glass_Out_2.fbx
- Glass_Out_U.fbx
- Glass_Wall_2.fbx
- Glass_Wall_U.fbx
- Wall_1_Gate.fbx
- Wall_1_Gate_C.fbx
- Wall_2_C.fbx
- Wall_2_Door.fbx
- Wall_2_Door_C.fbx
- Wall_2_Gate.fbx
- Wall_2_Gate_C.fbx
- Wall_2_Win.fbx
- Wall_4.fbx
- Wall_4_C.fbx
- Wall_4_Wins.fbx
- Wall_5.fbx
- Wall_5_C.fbx
- wall_1.fbx
- wall_1_C.fbx

### Assets/Heavy Station Kit/HANGARS/Post Processing (1)

- Hangars URP Profile.asset

### Assets/Heavy Station Kit/HANGARS/Prefabs/Aggregates (7)

- Aggregate1.prefab
- Balloon_B.prefab
- Balloon_C.prefab
- Balloons_3.prefab
- Cargocase.prefab
- Console2.prefab
- Reactor.prefab

### Assets/Heavy Station Kit/HANGARS/Prefabs/Arches (5)

- Arche_1_5.prefab
- Arche_1_6.prefab
- Arche_2_6.prefab
- Arche_3.prefab
- Arche_3_3.prefab

### Assets/Heavy Station Kit/HANGARS/Prefabs/Displays (2)

- H2_Cons1.prefab
- H2_Cons1_D.prefab

### Assets/Heavy Station Kit/HANGARS/Prefabs/Doors (2)

- H2_Door_Blocked.prefab
- H2_Gate2.prefab

### Assets/Heavy Station Kit/HANGARS/Prefabs/Floors (12)

- Floor0_0.prefab
- Floor1_0.prefab
- Floor1_0_F.prefab
- Floor1_Fill1.prefab
- Floor1_Fill1_F.prefab
- Floor1_Fill2_F.prefab
- Floor_4.prefab
- Floor_Fill1One.prefab
- Floor_Fill1One_F.prefab
- H2_floor_6A.prefab
- handrail_3.prefab
- handrail_4.prefab

### Assets/Heavy Station Kit/HANGARS/Prefabs/Gateway (3)

- GW_LargeGate.prefab
- Gateway.prefab
- Shield.prefab

### Assets/Heavy Station Kit/HANGARS/Prefabs/Outside (1)

- H2_LightWall_1.prefab

### Assets/Heavy Station Kit/HANGARS/Prefabs/Props (5)

- hang_Battery1.prefab
- hang_barrel1.prefab
- hang_barrel2.prefab
- hang_box2.prefab
- hang_box3.prefab

### Assets/Heavy Station Kit/HANGARS/Prefabs/Supports (4)

- Support54.prefab
- Support64.prefab
- Support65.prefab
- Support_4.prefab

### Assets/Heavy Station Kit/HANGARS/Prefabs/Top Bottom (3)

- TB_II_1B.prefab
- TB_II_2A.prefab
- TB_II_2B.prefab

### Assets/Heavy Station Kit/HANGARS/Prefabs/Top-Down (7)

- TD_hangar_ARC0.prefab
- TD_hangar_OW1A.prefab
- TD_hangar_OW1B.prefab
- TD_hangar_OW2.prefab
- TD_hangar_OW4.prefab
- TD_hangar_TW0.prefab
- TD_hangar_TW0a.prefab

### Assets/Heavy Station Kit/HANGARS/Prefabs/Walls (8)

- Wall_1.prefab
- Wall_1_Gate.prefab
- Wall_2_Door.prefab
- Wall_2_Gate.prefab
- Wall_2_Win.prefab
- Wall_4.prefab
- Wall_4_Win.prefab
- Wall_Small.prefab

### Assets/Heavy Station Kit/HANGARS/Scenes/Hangars (1)

- LightingData.asset

### Assets/Heavy Station Kit/HANGARS/Scenes/RP (1)

- ReflectionProbe-0.exr

### Assets/Heavy Station Kit/HANGARS/Textures/Aggregates (4)

- H2_Aggregates_0_A.png
- H2_Aggregates_2_A.png
- H2_Aggregates_3_A.png
- H2_Aggregates_4_A.png

### Assets/Heavy Station Kit/HANGARS/Textures/Displays (4)

- H2_Disp_Cons1D_E.png
- H2_Disp_Cons1_E.png
- H2_Disp_Cons1_M.tga
- H2_Disp_Cons1_N.png

### Assets/Heavy Station Kit/HANGARS/Textures/Doors (1)

- H2_DoorLamp_R.png

### Assets/Heavy Station Kit/HANGARS/Textures/Floors (3)

- H2_FloorsGlass_A.png
- H2_FloorsSolid_A.png
- H2_Floors_A.png

### Assets/Heavy Station Kit/HANGARS/Textures/Gateway (1)

- H2_Gateway_A.png

### Assets/Heavy Station Kit/HANGARS/Textures/Hangar Sky (6)

- Hangars2_back.png
- Hangars2_down.png
- Hangars2_front.png
- Hangars2_left.png
- Hangars2_right.png
- Hangars2_up.png

### Assets/Heavy Station Kit/HANGARS/Textures/Outside (1)

- H2_Outside_A.png

### Assets/Heavy Station Kit/HANGARS/Textures/Props (1)

- H2_Props_0_A.png

### Assets/Heavy Station Kit/HANGARS/Textures/Supports (1)

- H2_Supp_Doors_0_A.png

### Assets/Heavy Station Kit/HANGARS/Textures/Top Bottom (1)

- H2_TB_A.png

### Assets/Heavy Station Kit/HANGARS/Textures/Top-Down (1)

- H2_Top-Down_A.png

### Assets/Heavy Station Kit/HANGARS/Textures/Walls (7)

- H2_Arches_A.png
- H2_Arches_T_A.png
- H2_Walls1_1_A.png
- H2_Walls1_2_A.png
- H2_Walls1_5_A.png
- H2_Walls2_1_A.png
- H2_Walls2_5_A.png

### Assets/Heavy Station Kit/_common/Materials (1)

- _Default.mat

### Assets/Heavy Station Kit/_common/Scripts/Animated Texture (1)

- DotAnimatedTexture.cs

### Assets/Heavy Station Kit/_common/Scripts/Doors (5)

- DotHskDoor.cs
- DotHskDoorBeep.cs
- DotHskDoorHangarsGate2.cs
- DotHskDoorHangarsGate2Console.cs
- DotHskDoorSlider.cs

### Assets/Heavy Station Kit/_common/Scripts/Gateway (5)

- DotHskGate.cs
- DotHskGateHangars.cs
- DotHskGateHangarsConsole.cs
- DotHskGateHangarsParts.cs
- DotHskGateSlider.cs

### Assets/Heavy Station Kit/_common/Sounds (8)

- Blocked.wav
- Door_Heavy.wav
- Equipment_1.wav
- Gate_Move.wav
- Gate_Start.wav
- Gate_Step.wav
- Gate_Stop.wav
- Station_Ambient1.wav

### Assets/Heavy Station Kit/_common/Textures/GUI (2)

- tip_closeGate.png
- tip_openGate.png

## Assets/Knife

### Assets/Knife/Customizable Hologramm Shader/Materials (8)

- Black.mat
- Boiler.mat
- Hologram Earth 1.mat
- Hologram Gun Body.mat
- Hologram KM Body.mat
- Hologram KM Cloth.mat
- Light Cube.mat
- Light Sphere.mat

### Assets/Knife/Customizable Hologramm Shader/Models (1)

- boiler.FBX

### Assets/Knife/Customizable Hologramm Shader/Prefabs (1)

- boiler Variant.prefab

### Assets/Knife/Customizable Hologramm Shader/Scripts (1)

- LocalRotator.cs

### Assets/Knife/Customizable Hologramm Shader/Shaders (1)

- Hologram Shader.shader

### Assets/Knife/Customizable Hologramm Shader/Textures (9)

- 2_knifeman_body_Normal.png
- Blur one sided.png
- Blur.png
- Blur1.png
- Blurred line.png
- EarthAlpha.png
- EarthNormal.png
- KnifeMan_1-1_KnifeMan_Normal 1.png
- noise.png

### Assets/Knife/Customizable Hologramm Shader/Textures/Boiler (4)

- DefaultMaterial_Mixed_AO.png
- hologram low 1_DefaultMaterial_AlbedoTransparency.png
- hologram low 1_DefaultMaterial_Normal.png
- hologram low 1_DefaultMaterial_SpecularSmoothness.png

## Assets/Materials

### Assets/Materials/9. Mission (1)

- Test Npc 2.mat

### Assets/Materials/9. Mission/Mission Room/A_001 (5)

- M_Cyan.mat
- M_Gris_Métal.mat
- M_Noir_Bleuté.mat
- M_Noir_Profond.mat
- M_Orange.mat

### Assets/Materials/9. Mission/Mission Room/A_002 (6)

- M_Cyan.mat
- M_Gris_Métal.mat
- M_Noir_Bleuté.mat
- M_Noir_Profond.mat
- M_Orange.mat
- M_Plexiglas.mat

### Assets/Materials/9. Mission/Mission Room/A_003 (6)

- M_Cyan.mat
- M_Gris_Métal.mat
- M_Noir_Bleuté.mat
- M_Noir_Profond.mat
- M_Orange.mat
- M_Plexiglas.mat

### Assets/Materials/9. Mission/Mission Room/A_005 (4)

- M_Cyan.mat
- M_Gris_Bleuté.mat
- M_Gris_Métal.mat
- M_Orange.mat

### Assets/Materials/9. Mission/Mission Room/B_001 (5)

- M_Cyan.mat
- M_Gris_Métal.mat
- M_Noir_Bleuté.mat
- M_Noir_Profond.mat
- M_Orange.mat

### Assets/Materials/9. Mission/Mission Room/C_001 (5)

- M_Cyan.mat
- M_Noir_Bleuté.mat
- M_Noir_Clair.mat
- M_Noir_Profond.mat
- M_Orange.mat

### Assets/Materials/9. Mission/Mission Room/C_002 (5)

- M_Cyan.mat
- M_Gris_Métal.mat
- M_Noir_Bleuté.mat
- M_Noir_Profond.mat
- M_Orange.mat

### Assets/Materials/9. Mission/Mission Room/C_003 (5)

- M_Cyan.mat
- M_Gris_Metal.mat
- M_Noir_Bleuté.mat
- M_Noir_Profond.mat
- M_Orange.mat

### Assets/Materials/9. Mission/Mission Room/E_001 (5)

- M_Cyan.mat
- M_Gris_Métal.mat
- M_Noir_Bleuté.mat
- M_Noir_Profond.mat
- M_Orange.mat

### Assets/Materials/9. Mission/Mission Room/F_001 (12)

- M_Blanc.mat
- M_Bleu.mat
- M_Bleu_Clair.mat
- M_Cyan.mat
- M_Gris.mat
- M_Gris_Metal.mat
- M_Jaune.mat
- M_Noir_Bleuté.mat
- M_Noir_Profond.mat
- M_Orange.mat
- M_Plexiglas.mat
- M_Vert.mat

### Assets/Materials/9. Mission/Mission Room/G_001 (11)

- M_Blanc.mat
- M_Cyan.mat
- M_Marron.mat
- M_Noir_Bleuté.mat
- M_Noir_Profond.mat
- M_Orange.mat
- M_Plexiglas.mat
- M_Rock.mat
- M_Vert.mat
- M_Vert_Foncé.mat
- M_Wood.mat

### Assets/Materials/9. Mission/Mission Room/G_002 (13)

- M_ Red.mat
- M_Blanc.mat
- M_Bleu_Clair.mat
- M_Cyan.mat
- M_Jaune.mat
- M_Marron.mat
- M_Noir_Bleuté.mat
- M_Noir_Profond.mat
- M_Orange.mat
- M_Plexiglas.mat
- M_Rock.mat
- M_Vert.mat
- M_Vert_Foncé.mat

### Assets/Materials/Boss (5)

- BossTelegraph.mat
- DeathHologram.mat
- DizzyBreath.mat
- GroundDust.mat
- RoarWave.mat

### Assets/Materials/Combat (2)

- HitDebris.mat
- HitImpact.mat

### Assets/Materials/Environment (1)

- RockFragment.mat

### Assets/Materials/Humanoid Boss (3)

- Humanoid Boss.mat
- Humanoid Boss_BaseColor.png
- Humanoid Boss_Normal.png

### Assets/Materials/Psychokinesis (5)

- AvailableOutline.mat
- Floor.mat
- ForceRipple.mat
- UnavailableOutline.mat
- Wall.mat

## Assets/Models

### Assets/Models/Charcharosaurus (1)

- Charcharosaurus_Controller.controller

### Assets/Models/Charcharosaurus/FBX Files (34)

- Charcharosaurus@2HitComboClawsAttack.FBX
- Charcharosaurus@2HitComboClawsAttackForward.FBX
- Charcharosaurus@2HitComboClawsAttackForward_RM.FBX
- Charcharosaurus@3HitComboClawsAttack.FBX
- Charcharosaurus@3HitComboClawsAttackForward.FBX
- Charcharosaurus@3HitComboClawsAttackForward_RM.FBX
- Charcharosaurus@Death.FBX
- Charcharosaurus@GetHitHeavy.FBX
- Charcharosaurus@GetHitLight1.FBX
- Charcharosaurus@GetHitLight2.FBX
- Charcharosaurus@Idle.FBX
- Charcharosaurus@LeftClawsAttack.FBX
- Charcharosaurus@LeftClawsAttackForward.FBX
- Charcharosaurus@LeftClawsAttackForward_RM.FBX
- Charcharosaurus@RightClawsAttack.FBX
- Charcharosaurus@RightClawsAttackForward.FBX
- Charcharosaurus@RightClawsAttackForward_RM.FBX
- Charcharosaurus@Roar.FBX
- Charcharosaurus@Run.FBX
- Charcharosaurus@Run_RM.FBX
- Charcharosaurus@SmashAttack.FBX
- Charcharosaurus@SmashAttackForward.FBX
- Charcharosaurus@SmashAttackForward_RM.FBX
- Charcharosaurus@Turn90Left.FBX
- Charcharosaurus@Turn90Left_RM.FBX
- Charcharosaurus@Turn90Right.FBX
- Charcharosaurus@Turn90Right_RM.FBX
- Charcharosaurus@Turn90SmashAttackLeft.FBX
- Charcharosaurus@Turn90SmashAttackLeft_RM.FBX
- Charcharosaurus@Turn90SmashAttackRight.FBX
- Charcharosaurus@Turn90SmashAttackRight_RM.FBX
- Charcharosaurus@Walk.FBX
- Charcharosaurus@Walk_RM.FBX
- SK_Charcharosaurus.FBX

### Assets/Models/Charcharosaurus/Materials (1)

- M_Charcharosaurus.mat

### Assets/Models/Charcharosaurus/Prefab (1)

- SK_Charcharosaurus.prefab

### Assets/Models/Charcharosaurus/Textures (4)

- T_Charcharosaurus_BaseColor.jpg
- T_Charcharosaurus_MetallicSmoothness.tga
- T_Charcharosaurus_Normal.jpg
- T_Charcharosaurus_Occlusion.jpg

### Assets/Models/Cyborg Characters/Common/DEMO/HDRP_Settings/DiffusionProfiles (1)

- Skin.asset

### Assets/Models/Cyborg Characters/Common/DEMO/Scripts/TPS Scrips/Controllers/ThirdPersonController/Character/Models (1)

- Armature.fbx

### Assets/Models/Cyborg Characters/Common/DawnShader_SG (6)

- ClothingShader.shadergraph
- EyeShader.shadergraph
- EyeTearShader.shadergraph
- Floor.shadergraph
- HairShader.shadergraph
- SkinShader.shadergraph

### Assets/Models/Cyborg Characters/Common/Materials (4)

- MI_Avatar.mat
- MI_Eyes.mat
- MI_EyesTear.mat
- MI_Teeth.mat

### Assets/Models/Cyborg Characters/Common/Meshes/Avatar (1)

- SK_Human.fbx

### Assets/Models/Cyborg Characters/Common/Scrips/PhysicBone (1)

- ClassLibrary1.dll

### Assets/Models/Cyborg Characters/Common/Textures/Eyes (6)

- T_Eye_BC.tga
- T_Eye_MT.tga
- T_Eye_R.tga
- T_Eyes_D.tga
- T_Eyes_M.tga
- T_Flakes_N.png

### Assets/Models/Cyborg Characters/Common/Textures/Floor (1)

- T_Grid_BC.png

### Assets/Models/Cyborg Characters/Common/Textures/HumanSkins (5)

- T_MBody_M.tga
- T_MHead_M.tga
- T_Noise_BC.tga
- T_RoughnessMod_R.tga
- T_SkinPore_N.tga

### Assets/Models/Cyborg Characters/Common/Textures/Teeth (5)

- T_Teeth_BC.tga
- T_Teeth_Dirt.tga
- T_Teeth_M.tga
- T_Teeth_N.tga
- T_Teeth_ORM.tga

### Assets/Models/Cyborg Characters/Common/Textures/Tilling Textures (5)

- T_CloudPattern2_BC.tga
- T_PatternA_BC.png
- T_TextureSample5_BC.png
- T_TextureSample7_BC.tga
- T_TextureSample8_BC.tga

### Assets/Models/Cyborg Characters/G2_CyborgCharacters/Materials/Characters/Hairs (2)

- MI_Eyeslash.mat
- MI_Hairs.mat

### Assets/Models/Cyborg Characters/G2_CyborgCharacters/Materials/Characters/Skins (2)

- MI_MCyborgBody.mat
- MI_MCyborgHead.mat

### Assets/Models/Cyborg Characters/G2_CyborgCharacters/Materials/Clothings (11)

- MI_CyborgArms.mat
- MI_CyborgBoots.mat
- MI_Dreadlock.mat
- MI_Glass_Blue.mat
- MI_Glasses.mat
- MI_Helmet.mat
- MI_HoodCloth.mat
- MI_JetPack.mat
- MI_MPant.mat
- MI_MShirt.mat
- MI_Underwear.mat

### Assets/Models/Cyborg Characters/G2_CyborgCharacters/Meshes (3)

- SK_Dreadlock.fbx
- SK_MHair.fbx
- SK_PP801_SourceMeshes.fbx

### Assets/Models/Cyborg Characters/G2_CyborgCharacters/Prefabs/Prefabs Original (2)

- SK_Dreadlock.prefab
- SK_PP801_SourceMeshes.prefab

### Assets/Models/Cyborg Characters/G2_CyborgCharacters/Textures/Characters/Hairs (9)

- T_Hair_AO.tga
- T_Hair_Alpha.tga
- T_Hair_BC.tga
- T_Hair_D.tga
- T_Hair_DyeMask.tga
- T_Hair_Flow.tga
- T_Hair_ID.tga
- T_Hair_N.tga
- T_Hair_Root.tga

### Assets/Models/Cyborg Characters/G2_CyborgCharacters/Textures/Characters/Skins (14)

- T_MBody_Dirt.tga
- T_MCyborgBody_AMS.tga
- T_MCyborgBody_BC.tga
- T_MCyborgBody_EM.tga
- T_MCyborgBody_N.tga
- T_MCyborgBody_ORM.tga
- T_MCyborgBody_TattooMask.tga
- T_MCyborgHead_AMS.tga
- T_MCyborgHead_BC.tga
- T_MCyborgHead_EM.tga
- T_MCyborgHead_N.tga
- T_MCyborgHead_ORM.tga
- T_MCyborgHead_TattooMask.tga
- T_MHead_Dirt.tga

### Assets/Models/Cyborg Characters/G2_CyborgCharacters/Textures/Clothings (54)

- T_CyborgArms_BC.tga
- T_CyborgArms_Dirt.tga
- T_CyborgArms_EM.tga
- T_CyborgArms_M.tga
- T_CyborgArms_N.tga
- T_CyborgArms_ORM.tga
- T_CyborgBoots_BC.tga
- T_CyborgBoots_Dirt.tga
- T_CyborgBoots_EM.tga
- T_CyborgBoots_M.tga
- T_CyborgBoots_N.tga
- T_CyborgBoots_ORM.tga
- T_Dreadlock_BC.tga
- T_Dreadlock_Dirt.tga
- T_Dreadlock_M.tga
- T_Dreadlock_N.tga
- T_Dreadlock_ORM.tga
- T_Glasses_BC.tga
- T_Glasses_Dirt.tga
- T_Glasses_M.tga
- T_Glasses_N.tga
- T_Glasses_ORM.tga
- T_Helmet_BC.tga
- T_Helmet_Dirt.tga
- T_Helmet_EM.tga
- T_Helmet_M.tga
- T_Helmet_N.tga
- T_Helmet_ORM.tga
- T_HoodCloth_BC.tga
- T_HoodCloth_Dirt.tga
- T_HoodCloth_M.tga
- T_HoodCloth_N.tga
- T_HoodCloth_ORM.tga
- T_JetPack_BC.tga
- T_JetPack_Dirt.tga
- T_JetPack_EM.tga
- T_JetPack_M.tga
- T_JetPack_N.tga
- T_JetPack_ORM.tga
- T_MPant_BC.tga
- T_MPant_Dirt.tga
- T_MPant_M.tga
- T_MPant_N.tga
- T_MPant_ORM.tga
- T_MShirt_BC.tga
- T_MShirt_Dirt.tga
- T_MShirt_M.tga
- T_MShirt_N.tga
- T_MShirt_ORM.tga
- T_Underwear_BC.tga
- T_Underwear_Dirt.tga
- T_Underwear_M.tga
- T_Underwear_N.tga
- T_Underwear_ORM.tga

### Assets/Models/Mission Room/Furniture (4)

- SM_Bench.fbx
- SM_Chair.fbx
- SM_Operator_Command_Chair.fbx
- SM_Vending_Machine.fbx

### Assets/Models/Mission Room/Machines (12)

- SM_Control_Console_Standalone.fbx
- SM_Core_Processor.fbx
- SM_Cryogenic_Stasis_Chamber.fbx
- SM_Data_Server_Rack.fbx
- SM_Data_Workstation.fbx
- SM_Energy_Cell.fbx
- SM_Holo.fbx
- SM_Large_Control_Desk.fbx
- SM_Large_Screen_Pannel.fbx
- SM_Reactor.fbx
- SM_Server.fbx
- SM_Server_V2.fbx

### Assets/Models/Mission Room/Plants (7)

- SM_Bio_Dome_Planter.fbx
- SM_Botanical_GlassPlanter_Long.fbx
- SM_Botanical_HydroponicStation.fbx
- SM_Botanical_SpecimenChamber_Tall.fbx
- SM_Rectangular_Floor_Planter.fbx
- SM_Tall_Rectangular_Planter.fbx
- SM_Vertical_Glass_Planter.fbx

### Assets/Models/Mission Room/Structure (10)

- SM_Corner_Cover_Post.fbx
- SM_DoorFrame_Double.fbx
- SM_Door_Double_V1L.fbx
- SM_Door_Double_V1R.fbx
- SM_WallPanel_Large_Window.fbx
- SM_Wall_V0.fbx
- SM_Wall_V1.fbx
- SM_Wall_V2.fbx
- SM_Wall_V3.fbx
- SM_Wall_V5.fbx

### Assets/Models/Perderos (1)

- Perderos_Controller.controller

### Assets/Models/Perderos/FBX Files (40)

- Perderos@ClawsAttack2HitCombo.FBX
- Perderos@ClawsAttack2HitComboForward.FBX
- Perderos@ClawsAttack2HitComboForward_RM.FBX
- Perderos@ClawsAttack3HitCombo.FBX
- Perderos@ClawsAttack3HitComboForward.FBX
- Perderos@ClawsAttack3HitComboForward_RM.FBX
- Perderos@ClawsAttackLeft.FBX
- Perderos@ClawsAttackLeftForward.FBX
- Perderos@ClawsAttackLeftForward_RM.FBX
- Perderos@ClawsAttackRight.FBX
- Perderos@ClawsAttackRightForward.FBX
- Perderos@ClawsAttackRightForward_RM.FBX
- Perderos@Death.FBX
- Perderos@GetHitBack.FBX
- Perderos@GetHitFront.FBX
- Perderos@GetHitLeft.FBX
- Perderos@GetHitRight.FBX
- Perderos@IdleBreathe.FBX
- Perderos@IdleLookAround.FBX
- Perderos@JumpBackwards.FBX
- Perderos@JumpBackwards_RM.FBX
- Perderos@JumpForward.FBX
- Perderos@JumpForward_RM.FBX
- Perderos@JumpSmashAttack.FBX
- Perderos@JumpSmashAttack_RM.FBX
- Perderos@Roar.FBX
- Perderos@Run.FBX
- Perderos@SmashAttack.FBX
- Perderos@SmashAttackForward.FBX
- Perderos@SmashAttackForward_RM.FBX
- Perderos@Turn90Left.FBX
- Perderos@Turn90LeftSmashAttack.FBX
- Perderos@Turn90LeftSmashAttack_RM.FBX
- Perderos@Turn90Left_RM.FBX
- Perderos@Turn90Right.FBX
- Perderos@Turn90RightSmashAttack.FBX
- Perderos@Turn90RightSmashAttack_RM.FBX
- Perderos@Turn90Right_RM.FBX
- Perderos@Walk.FBX
- SK_Perderos.FBX

### Assets/Models/Perderos/Materials (1)

- M_Perderos.mat

### Assets/Models/Perderos/Prefabs (1)

- Perderos.prefab

### Assets/Models/Perderos/Textures (4)

- T_Perderos_BaseColor.png
- T_Perderos_MetallicSmoothness.tga
- T_Perderos_Normal.png
- T_Perderos_Occlusion.png

### Assets/Models/VARCO3DImports/9bb82ae9-9c1c-40c6-b5f9-7b9e3373e275 (1)

- 9bb82ae9-9c1c-40c6-b5f9-7b9e3373e275.usdz

### Assets/Models/VARCO3DImports/fe8f8090-b0f9-4b4f-943b-88ee92aed451_1 (1)

- Green Combat Robot-a0b7fd5fb0aa512579dbbc24ee981dbf.fbx

## Assets/Physics Materials

### Assets/Physics Materials (1)

- PM_PsychokinesisObject.physicMaterial

## Assets/Plugins

### Assets/Plugins/TextMesh Pro/Shaders (2)

- TMP_SDF-Mobile.shader
- TMP_SDF.shader

## Assets/Prefabs

### Assets/Prefabs/1. Player (1)

- Player.prefab

### Assets/Prefabs/10. UI (2)

- Quest 1.prefab
- UI Canvas.prefab

### Assets/Prefabs/10. UI/HUD (3)

- BossIntroTitle.prefab
- DeathScreen.prefab
- HUD Canvas.prefab

### Assets/Prefabs/2. Camera (1)

- Main Camera.prefab

### Assets/Prefabs/4. Superpower (2)

- Augmentation Chip Canvas.prefab
- Augmentation Chip Panel.prefab

### Assets/Prefabs/6. Boss (2)

- AttackTelegraph.prefab
- Perderos.prefab

### Assets/Prefabs/6.5. Humanoid Boss (4)

- Cube 1x.prefab
- Humanoid Boss Dummy.prefab
- Humanoid Boss.prefab
- HumanoidDeflectedProjectile.prefab

### Assets/Prefabs/8. Environmental Mechanics (1)

- WallFragment.prefab

### Assets/Prefabs/8. Environmental Mechanics/GeneratedFragments/Large (20)

- LargeObject01.asset
- LargeObject01.prefab
- LargeObject02.asset
- LargeObject02.prefab
- LargeObject03.asset
- LargeObject03.prefab
- LargeObject04.asset
- LargeObject04.prefab
- LargeObject05.asset
- LargeObject05.prefab
- LargeObject06.asset
- LargeObject06.prefab
- LargeObject07.asset
- LargeObject07.prefab
- LargeObject08.asset
- LargeObject08.prefab
- LargeObject09.asset
- LargeObject09.prefab
- LargeObject10.asset
- LargeObject10.prefab

### Assets/Prefabs/8. Environmental Mechanics/GeneratedFragments/Medium (40)

- MediumObject01.asset
- MediumObject01.prefab
- MediumObject02.asset
- MediumObject02.prefab
- MediumObject03.asset
- MediumObject03.prefab
- MediumObject04.asset
- MediumObject04.prefab
- MediumObject05.asset
- MediumObject05.prefab
- MediumObject06.asset
- MediumObject06.prefab
- MediumObject07.asset
- MediumObject07.prefab
- MediumObject08.asset
- MediumObject08.prefab
- MediumObject09.asset
- MediumObject09.prefab
- MediumObject10.asset
- MediumObject10.prefab
- MediumObject11.asset
- MediumObject11.prefab
- MediumObject12.asset
- MediumObject12.prefab
- MediumObject13.asset
- MediumObject13.prefab
- MediumObject14.asset
- MediumObject14.prefab
- MediumObject15.asset
- MediumObject15.prefab
- MediumObject16.asset
- MediumObject16.prefab
- MediumObject17.asset
- MediumObject17.prefab
- MediumObject18.asset
- MediumObject18.prefab
- MediumObject19.asset
- MediumObject19.prefab
- MediumObject20.asset
- MediumObject20.prefab

### Assets/Prefabs/8. Environmental Mechanics/GeneratedFragments/MetalShard (12)

- MetalShardObject01.asset
- MetalShardObject01.prefab
- MetalShardObject02.asset
- MetalShardObject02.prefab
- MetalShardObject03.asset
- MetalShardObject03.prefab
- MetalShardObject04.asset
- MetalShardObject04.prefab
- MetalShardObject05.asset
- MetalShardObject05.prefab
- MetalShardObject06.asset
- MetalShardObject06.prefab

### Assets/Prefabs/8. Environmental Mechanics/GeneratedFragments/Small (10)

- SmallObject01.asset
- SmallObject01.prefab
- SmallObject02.asset
- SmallObject02.prefab
- SmallObject03.asset
- SmallObject03.prefab
- SmallObject04.asset
- SmallObject04.prefab
- SmallObject05.asset
- SmallObject05.prefab

### Assets/Prefabs/8. Environmental Mechanics/PsychokinesisProps (45)

- Prop1.prefab
- Prop1_Debris.mat
- Prop1_Mesh.asset
- Prop2.prefab
- Prop2_Debris.mat
- Prop2_Mesh.asset
- Prop3.prefab
- Prop3_Debris.mat
- Prop3_Mesh.asset
- Prop4.prefab
- Prop4_Debris.mat
- Prop4_Mesh.asset
- Prop5.prefab
- Prop5_Debris.mat
- Prop5_Mesh.asset
- Prop6.prefab
- Prop6_Debris.mat
- Prop6_Mesh.asset
- Prop7.prefab
- Prop7_Debris.mat
- Prop7_Mesh.asset
- Prop8.prefab
- Prop8_Debris.mat
- Prop8_Mesh.asset
- Prop9.prefab
- Prop9_Debris.mat
- Prop9_Mesh.asset
- Prop_Barrel1 gray.prefab
- Prop_Barrel1 gray_Debris.mat
- Prop_Barrel1 gray_Mesh.asset
- Prop_Barrel1 yellow.prefab
- Prop_Barrel1 yellow_Debris.mat
- Prop_Barrel1 yellow_Mesh.asset
- Prop_Barrel1.prefab
- Prop_Barrel1_Debris.mat
- Prop_Barrel1_Mesh.asset
- Prop_Cylinder1.prefab
- Prop_Cylinder1_Debris.mat
- Prop_Cylinder1_Mesh.asset
- Prop_Cylinder2.prefab
- Prop_Cylinder2_Debris.mat
- Prop_Cylinder2_Mesh.asset
- Tank_01.prefab
- Tank_01_Debris.mat
- Tank_01_Mesh.asset

### Assets/Prefabs/8. Environmental Mechanics/PsychokinesisProps/Hangars (36)

- Balloon_B.prefab
- Balloon_B_Debris.mat
- Balloon_B_Mesh.asset
- Balloon_C.prefab
- Balloon_C_Debris.mat
- Balloon_C_Mesh.asset
- Balloons_3.prefab
- Balloons_3_Debris.mat
- Balloons_3_Mesh.asset
- Cargocase.prefab
- Cargocase_Debris.mat
- Cargocase_Mesh.asset
- Console2.prefab
- Console2_Debris.mat
- Console2_Mesh.asset
- Server.prefab
- Server_Debris.mat
- Server_Mesh.asset
- hang_Battery1.prefab
- hang_Battery1_Debris.mat
- hang_Battery1_Mesh.asset
- hang_barrel1.prefab
- hang_barrel1_Debris.mat
- hang_barrel1_Mesh.asset
- hang_barrel2.prefab
- hang_barrel2_Debris.mat
- hang_barrel2_Mesh.asset
- hang_box1.prefab
- hang_box1_Debris.mat
- hang_box1_Mesh.asset
- hang_box2.prefab
- hang_box2_Debris.mat
- hang_box2_Mesh.asset
- hang_box3.prefab
- hang_box3_Debris.mat
- hang_box3_Mesh.asset

### Assets/Prefabs/9. Mission/Mission Room/Furniture (4)

- SM_Bench.prefab
- SM_Chair.prefab
- SM_Operator_Command_Chair.prefab
- SM_Vending_Machine.prefab

### Assets/Prefabs/9. Mission/Mission Room/Machines (12)

- SM_Control_Console_Standalone.prefab
- SM_Core_Processor.prefab
- SM_Cryogenic_Stasis_Chamber.prefab
- SM_Data_Server_Rack.prefab
- SM_Data_Workstation.prefab
- SM_Energy_Cell.prefab
- SM_Holo.prefab
- SM_Large_Control_Desk.prefab
- SM_Large_Screen_Pannel.prefab
- SM_Reactor.prefab
- SM_Server.prefab
- SM_Server_V2.prefab

### Assets/Prefabs/9. Mission/Mission Room/Plants (7)

- SM_Bio_Dome_Planter.prefab
- SM_Botanical_GlassPlanter_Long.prefab
- SM_Botanical_HydroponicStation.prefab
- SM_Botanical_SpecimenChamber_Tall.prefab
- SM_Rectangular_Floor_Planter.prefab
- SM_Tall_Rectangular_Planter.prefab
- SM_Vertical_Glass_Planter.prefab

### Assets/Prefabs/9. Mission/Mission Room/Structure (10)

- SM_Corner_Cover_Post.prefab
- SM_DoorFrame_Double.prefab
- SM_Door_Double_V1L.prefab
- SM_Door_Double_V1R.prefab
- SM_WallPanel_Large_Window.prefab
- SM_Wall_V0.prefab
- SM_Wall_V1.prefab
- SM_Wall_V2.prefab
- SM_Wall_V3.prefab
- SM_Wall_V5.prefab

## Assets/Resources

### Assets/Resources (1)

- check.png

## Assets/Scenes

### Assets/Scenes/10. Build/2. Final (5)

- 01. Title.unity
- 02. Lobby.unity
- 03. Easy Boss.unity
- 04. Normal Boss.unity
- 05. Hard Boss.unity

### Assets/Scenes/10. Build/2. Final/03. Easy Boss (2)

- NavMesh-Stage.asset
- OcclusionCullingData.asset

### Assets/Scenes/10. Build/2. Final/04. Normal Boss (2)

- LightingData.asset
- OcclusionCullingData.asset

### Assets/Scenes/10. Build/2. Final/05. Hard Boss (1)

- NavMesh-Boss NavMesh.asset

### Assets/Scenes/10. Build/2. Final/Lighting (3)

- 03. Easy Boss.lighting
- 04. Normal Boss.lighting
- 05. Hard Boss.lighting

### Assets/Scenes/9. Mission (1)

- Mission Lobby Profile.asset

## Assets/Sci-Fi Base

### Assets/Sci-Fi Base/Models (81)

- Barrier_Solid_01.fbx
- Barrier_Solid_01_L1.fbx
- Barrier_Solid_01_L2.fbx
- Barrier_Solid_01_L3.fbx
- Cables1.fbx
- Cables1_L1.fbx
- Cables1_L2.fbx
- Cables1_L3.fbx
- Concrete_Ground1.fbx
- Concrete_Ground1_Debris.fbx
- Concrete_Ground1_L1.fbx
- Concrete_Ground2.fbx
- Concrete_Ground2_Debris.fbx
- Concrete_Ground2_L1.fbx
- Concrete_Ground3.fbx
- Concrete_Ground3_L1.fbx
- Concrete_Ground4.fbx
- Concrete_Ground4_Debris.fbx
- Concrete_Ground4_L1.fbx
- Concrete_wall1.fbx
- Concrete_wall1_L1.fbx
- Concrete_wall2.fbx
- Concrete_wall2_CourA.fbx
- Concrete_wall2_CourA_L1.fbx
- Concrete_wall2_CourA_L2.fbx
- Concrete_wall2_L1.fbx
- Concrete_wall2_L2.fbx
- Cover_Panel1A_L1.fbx
- Cover_Panel2A_L0.fbx
- Cover_Panel2A_L1.fbx
- Ground_Plate1.fbx
- Ground_Plate1_L1.fbx
- Ground_Plate2.fbx
- Ground_Plate2_L1.fbx
- Lamp_07.fbx
- Lamp_07_L1.fbx
- Lamp_07_L2.fbx
- Lamp_Wall_Lights.fbx
- Lamp_Wall_Lights_L1.fbx
- Lamp_Wall_Lights_L2.fbx
- Lamp_Wall_Lights_L3.fbx
- Lamp_Wall_Lights_Phys.fbx
- Lamp_Wall_Mount.fbx
- Lamp_Wall_Mount_L1.fbx
- Lamp_Wall_Mount_L2.fbx
- Lamp_Wall_Mount_L3.fbx
- Powerbox_15.fbx
- Powerbox_15_L1.fbx
- Powerbox_15_L2.fbx
- Prop_vents_08.fbx
- Prop_vents_08_L1.fbx
- Prop_vents_08_L3.fbx
- Prop_vents_08_l2.fbx
- Wall3_Prop05_L0.fbx
- Wall3_Prop05_L1.fbx
- Wall3_Prop05_L2.fbx
- Wall3_Prop05_L3.fbx
- Wall_Hangar1_Barrier1.fbx
- Wall_Hangar1_Barrier1_L1.fbx
- Wall_Hangar1_Barrier1_L2.fbx
- Wall_Hangar1_Catwalk1.fbx
- Wall_Hangar1_Catwalk1_L1.fbx
- Wall_Hangar1_Catwalk1_L2.fbx
- Wall_Hangar1_Catwalk3.fbx
- Wall_Hangar1_Catwalk3_L1.fbx
- Wall_Hangar1_Courner1.fbx
- Wall_Hangar1_Courner1_L1.fbx
- Wall_Hangar1_Courner1_L2.fbx
- Wall_Hangar1_Doors.fbx
- Wall_Hangar1_Doors_L1.fbx
- Wall_Hangar1_Doors_L2.fbx
- Wall_Hangar1_L0.fbx
- Wall_Hangar1_L1.fbx
- Wall_Hangar1_L2.fbx
- Wall_Hangar1_window1.fbx
- Wall_Hangar1_window1_L1.fbx
- Wall_Hangar1_window1_L2.fbx
- Wall_Large_04.fbx
- Wall_Large_04_LOD1.fbx
- Wall_Large_04_LOD2.fbx
- Wall_Large_04_phys.fbx

### Assets/Sci-Fi Base/Models/Materials (27)

- Barrel_01_a.mat
- Barrel_01_gray.mat
- Barrel_01_yellow.mat
- Cables2_a.mat
- Concrete_Blocks1_Stripes.mat
- Concrete_Blocks1_a.mat
- Concrete_Blocks2_a.mat
- Concrete_Blocks3_Layered.mat
- Grid_02_a.mat
- Grid_04_a.mat
- Ground_Plates1_a.mat
- Ground_concrete1_a.mat
- Pipe_Traction1_a.mat
- Platform_Panel5_a.mat
- Platform_Panel7_a.mat
- Platform_Panel9_a.mat
- Props2_a.mat
- Props_Base1_a.mat
- Tank_1_a.mat
- Vents_01_a.mat
- Wall_Large_el1_a.mat
- Wall_Large_el2_a.mat
- Wall_Large_el3_a.mat
- Wall_Panel2_a.mat
- Wall_Panel4_a.mat
- Wall_Panel7_a.mat
- Wall_Panel9_a.mat

### Assets/Sci-Fi Base/Prefabs (26)

- Barrier_Solid_01.prefab
- Cables1.prefab
- Concrete_Ground1.prefab
- Concrete_Ground2.prefab
- Concrete_Ground3.prefab
- Concrete_Ground4.prefab
- Concrete_wall1 Stripes Variant.prefab
- Concrete_wall1.prefab
- Concrete_wall2.prefab
- Concrete_wall2_courA.prefab
- Cover Panel2A.prefab
- Ground_Plate1.prefab
- Ground_Plate2.prefab
- Lamp_07.prefab
- Lamp_Wall.prefab
- Powerbox_15.prefab
- Prop_vents_08.prefab
- Wall3_Prop_05.prefab
- Wall_Hangar1.prefab
- Wall_Hangar1_Barrier1.prefab
- Wall_Hangar1_Catwalk1.prefab
- Wall_Hangar1_Catwalk3.prefab
- Wall_Hangar1_Courner1.prefab
- Wall_Hangar1_Doors.prefab
- Wall_Hangar1_Window.prefab
- Wall_Large_04.prefab

### Assets/Sci-Fi Base/Rocks/Textures (2)

- Rock_Detail1_Base_Color.tif
- Rock_Detail1_Normal.tif

### Assets/Sci-Fi Base/Textures (77)

- Barrel_01_a.tif
- Barrel_01_gray.tif
- Barrel_01_mask.tif
- Barrel_01_n.tif
- Barrel_01_yellow.tif
- Cables2_a.tif
- Cables2_mask.tif
- Cables2_n.tif
- Concrete_Blocks1_Stripes.tif
- Concrete_Blocks1_a.tif
- Concrete_Blocks1_mask.tif
- Concrete_Blocks1_n.tif
- Concrete_Blocks2_a.tif
- Concrete_Blocks2_maks.tif
- Concrete_Blocks2_n.tif
- Concrete_Blocks3_Mask.tif
- Concrete_Blocks3_a.tif
- Concrete_Blocks3_n.tif
- Grid_02_a.tif
- Grid_02_mask.tif
- Grid_02_n.tif
- Grid_04_a.tif
- Grid_04_mask.tif
- Grid_04_n.tif
- Ground_Plates1_a.tif
- Ground_Plates1_mask.tif
- Ground_Plates1_n.tif
- Ground_concrete1_a.tif
- Ground_concrete1_mask.tif
- Ground_concrete1_n.tif
- Pipe_Traction1_a.png
- Pipe_Traction1_mask.tif
- Pipe_Traction1_n.png
- Platform_Panel5_a.tif
- Platform_Panel5_e.tif
- Platform_Panel5_mask.tif
- Platform_Panel5_n.tif
- Platform_Panel7_a.tif
- Platform_Panel7_mask.tif
- Platform_Panel7_n.tif
- Platform_Panel9_a.tif
- Platform_Panel9_mask.tif
- Platform_Panel9_n.tif
- Props2_a.tif
- Props2_mask.tif
- Props2_n.tif
- Props_Base1_a.tif
- Props_Base1_e.tif
- Props_Base1_mask.tif
- Props_Base1_n.tif
- Tank_1_a.tif
- Tank_1_mask.tif
- Tank_1_n.tif
- Vents_01_a.tif
- Vents_01_mask.tif
- Vents_01_n.tif
- Wall_Large_el1_a.tif
- Wall_Large_el1_mask.tif
- Wall_Large_el1_n.tif
- Wall_Large_el2_a.tif
- Wall_Large_el2_mask.tif
- Wall_Large_el2_n.tif
- Wall_Large_el3_a.tif
- Wall_Large_el3_mask.tif
- Wall_Large_el3_n.tif
- Wall_Panel2_a.tif
- Wall_Panel2_mask.tif
- Wall_Panel2_n.tif
- Wall_Panel4_a.tif
- Wall_Panel4_mask.tif
- Wall_Panel4_n.tif
- Wall_Panel7_a.tif
- Wall_Panel7_mask.tif
- Wall_Panel7_n.tif
- Wall_Panel9_a.tif
- Wall_Panel9_mask.tif
- Wall_Panel9_n.tif

## Assets/Scripts

### Assets/Scripts/1. Player (6)

- AnimStateEventListener.cs
- AnimStateEventSender.cs
- Player.cs
- PlayerCombatIdle.cs
- PlayerController.cs
- PlayerStats.cs

### Assets/Scripts/10. UI (19)

- BossHUD.cs
- BossIntroTitle.cs
- ChipShapeGraphic.cs
- Crosshair.cs
- DamageVignette.cs
- DeathScreen.cs
- Minimap.cs
- MissionListUI.cs
- PlayerHUD.cs
- SkillSlotGraphic.cs
- SkillSlotHUD.cs
- StartMenuUI.cs
- TooltipTrigger.cs
- TooltipView.cs
- UIGlitch.cs
- UILineReveal.cs
- UIStaggerReveal.cs
- VectorBar.cs
- VectorPanel.cs

### Assets/Scripts/11. Reward (3)

- BossChipReward.cs
- BossRewardPanel.cs
- RewardIconView.cs

### Assets/Scripts/2. Camera (1)

- CameraController.cs

### Assets/Scripts/3. Psychokinesis (3)

- PsychokinesisController.cs
- PsychokinesisForceWave.cs
- PsychokinesisObject.cs

### Assets/Scripts/4. Superpower/1. AugmentationChip (1)

- AugmentationChipInventory.cs

### Assets/Scripts/4. Superpower/1. AugmentationChip/UI (5)

- AugmentationChipLayoutBuilder.cs
- AugmentationChipPanel.cs
- AugmentationChipToggle.cs
- ChipGridView.cs
- ChipTrayView.cs

### Assets/Scripts/4. Superpower/3. Skill (2)

- PassiveSkillRunner.cs
- SkillCaster.cs

### Assets/Scripts/4. Superpower/3. Skill/Skills/Defense (2)

- TimeReverseEffect.cs
- TimeReverseRecorder.cs

### Assets/Scripts/4. Superpower/3. Skill/Skills/Utility (1)

- TimeSlowEffect.cs

### Assets/Scripts/4. Superpower/3. Skill/UI (1)

- SkillSlotBarView.cs

### Assets/Scripts/4. Superpower/Development (3)

- AugmentationChipEquipTester.cs
- AugmentationChipGrantTester.cs
- InGameDebugTool.cs

### Assets/Scripts/5. Combat (1)

- CombatManager.cs

### Assets/Scripts/5. Combat/1. Entity (1)

- CombatEntity.cs

### Assets/Scripts/5. Combat/1. Entity/Stats (4)

- CombatStats.cs
- EasyBossStats.asset
- HardBossStats.asset
- PlayerStats.asset

### Assets/Scripts/5. Combat/1. Entity/Stats/_OLD (1)

- DebugPlayerStats.asset

### Assets/Scripts/5. Combat/99. Development (1)

- PlayerHeavyHitTester.cs

### Assets/Scripts/5. Combat/99. Sample (1)

- SampleCombatMonster.cs

### Assets/Scripts/6. Boss (17)

- BossDeathResidue.cs
- BossDeathShed.cs
- BossDizzy.cs
- BossDizzyBreath.cs
- BossDizzyStars.cs
- BossFootDust.cs
- BossGroggy.cs
- BossHitFeedback.cs
- BossHitboxRegistrar.cs
- BossIntroCinematic.cs
- BossLyingGroundFit.cs
- BossMonster.cs
- BossMovement.cs
- BossNavMeshGuard.cs
- BossPathfinder.cs
- BossRoarWave.cs
- BossShatterCollider.cs

### Assets/Scripts/6. Boss/1. States (6)

- BossChaseState.cs
- BossDeathState.cs
- BossGroggyState.cs
- BossHopState.cs
- BossIdleState.cs
- BossTurnState.cs

### Assets/Scripts/6. Boss/1. States/Pattern (18)

- Boss2Combo.cs
- Boss2ComboForward.cs
- Boss3Combo.cs
- Boss3ComboForward.cs
- BossCharge.cs
- BossJumpSmash.cs
- BossLeftClaw.cs
- BossLeftClawForward.cs
- BossLeftTurnSmash.cs
- BossPsychokinesisDropPattern.cs
- BossPsychokinesisThrowPattern.cs
- BossRightClaw.cs
- BossRightClawForward.cs
- BossRightTurnSmash.cs
- BossRoarPattern.cs
- BossShockwavePattern.cs
- BossSmash.cs
- BossSmashForward.cs

### Assets/Scripts/6. Boss/2. Telegraph (1)

- AttackTelegraph.cs

### Assets/Scripts/6.5. Humanoid Boss (6)

- HumanoidBossCombatEntity.cs
- HumanoidBossHitboxes.cs
- HumanoidChaseState.cs
- HumanoidGroggyState.cs
- HumanoidIdleState.cs
- HumanoidStrafeState.cs

### Assets/Scripts/6.5. Humanoid Boss/Clone (3)

- HumanoidClone.cs
- HumanoidCloneAppearState.cs
- HumanoidCloneSummon.cs

### Assets/Scripts/6.5. Humanoid Boss/Effect (1)

- HumanoidSwordTrail.cs

### Assets/Scripts/6.5. Humanoid Boss/Environment (1)

- CubeRain.cs

### Assets/Scripts/6.5. Humanoid Boss/Pattern (23)

- HumanoidCharge.cs
- HumanoidCombo3.cs
- HumanoidCombo4A.cs
- HumanoidCombo4B.cs
- HumanoidCombo7.cs
- HumanoidDashSlashA.cs
- HumanoidDashSlashB.cs
- HumanoidDiagonalSlashA.cs
- HumanoidDiagonalSlashI.cs
- HumanoidDimensionSlash.cs
- HumanoidDimensionSlash_2.cs
- HumanoidHeavySlash.cs
- HumanoidJumpSmashF.cs
- HumanoidJumpSmashJ.cs
- HumanoidLeftTurnSlash.cs
- HumanoidRightTurnSlash.cs
- HumanoidRiseSmash.cs
- HumanoidRiseSpinSmash.cs
- HumanoidSlash.cs
- HumanoidSpinSweep.cs
- HumanoidTackleCombo.cs
- HumanoidTackleUpper.cs
- HumanoidThrust.cs

### Assets/Scripts/6.5. Humanoid Boss/Phase (2)

- HumanoidPhaseController.cs
- HumanoidPhaseTransition.cs

### Assets/Scripts/6.5. Humanoid Boss/Reaction (5)

- HumanoidDeflectSlash.cs
- HumanoidDeflectedProjectile.cs
- HumanoidGuardCounter.cs
- HumanoidProjectileWatcher.cs
- HumanoidSidestep.cs

### Assets/Scripts/7. Arena (1)

- SceneLoadManager.cs

### Assets/Scripts/8. Environmental Mechanics (5)

- PsychokinesisFallZone.cs
- WallFragment.cs
- WallFragmentDebris.cs
- WallFragmentRaycastTest.cs
- WallFragmentSpawner.cs

### Assets/Scripts/9. Mission (4)

- BossHologramDisplay.cs
- MissionNPC.cs
- MissionUI.cs
- StageMoveUI.cs

### Assets/Scripts/98. Utils/ObjectPool (1)

- ObjectPoolManager.cs

## Assets/Settings

### Assets/Settings (1)

- EnvironmentDemo URP Profile.asset

## Assets/Shaders

### Assets/Shaders (6)

- BossTelegraph.shader
- ForceRipple.shader
- GroundDust.shader
- HitDebris.shader
- HitImpact.shader
- TriplanarRock.shader

## Assets/VFX

### Assets/VFX/Hovl Studio/HSFiles/Materials (2)

- Trail21bcg.mat
- Trail70dn.mat

### Assets/VFX/Hovl Studio/HSFiles/Shaders (1)

- DissolveNoise.shader

### Assets/VFX/Hovl Studio/HSFiles/Shaders/Shader Graph (1)

- HS_Blend_CG.shadergraph

### Assets/VFX/Hovl Studio/HSFiles/Textures (3)

- Noise62.png
- Trail21.png
- Trail70.png

## Packages/com.unity.ai.navigation

### Packages/com.unity.ai.navigation/Gizmos (1)

- NavMeshSurface Icon.png

### Packages/com.unity.ai.navigation/Runtime (1)

- NavMeshSurface.cs

## Packages/com.unity.importer.usd

### Packages/com.unity.importer.usd/Shaders (1)

- usd_preview_surface.shadergraph

### Packages/com.unity.importer.usd/Unity.Importer.USD.Editor/ImportGraph (1)

- usdImporter.asset

### Packages/com.unity.importer.usd/Unity.Importer.USD.Editor/Modular (1)

- UsdModularImporter.cs

### Packages/com.unity.importer.usd/Unity.Importer.USD.Editor/Modular/Icons (1)

- d_PrefabModel_NotImported@256.png

### Packages/com.unity.importer.usd/Unity.Importer/Graph (1)

- ImporterGraph.cs

## Packages/com.unity.inputsystem

### Packages/com.unity.inputsystem/InputSystem/Editor/AssetImporter (1)

- InputActionImporter.cs

### Packages/com.unity.inputsystem/InputSystem/Editor/Icons (1)

- InputSystemUIInputModule@64.png

### Packages/com.unity.inputsystem/InputSystem/Plugins/PlayerInput (1)

- DefaultInputActions.inputactions

### Packages/com.unity.inputsystem/InputSystem/Plugins/UI (1)

- InputSystemUIInputModule.cs

## Packages/com.unity.render-pipelines.core

### Packages/com.unity.render-pipelines.core/Runtime/Volume (2)

- Volume.cs
- VolumeProfile.cs

## Packages/com.unity.render-pipelines.universal

### Packages/com.unity.render-pipelines.universal/Editor (1)

- AssetVersion.cs

### Packages/com.unity.render-pipelines.universal/Runtime (2)

- UniversalAdditionalCameraData.cs
- UniversalAdditionalLightData.cs

### Packages/com.unity.render-pipelines.universal/Runtime/Materials (1)

- Lit.mat

### Packages/com.unity.render-pipelines.universal/Runtime/Overrides (7)

- Bloom.cs
- ChromaticAberration.cs
- ColorAdjustments.cs
- LiftGammaGain.cs
- Tonemapping.cs
- Vignette.cs
- WhiteBalance.cs

### Packages/com.unity.render-pipelines.universal/Shaders (3)

- Lit.shader
- SimpleLit.shader
- Unlit.shader

### Packages/com.unity.render-pipelines.universal/Shaders/Nature (1)

- SpeedTree7.shader

## Packages/com.unity.shadergraph

### Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor (1)

- AssetVersion.cs

### Packages/com.unity.shadergraph/Editor/Importers (1)

- ShaderGraphImporter.cs

## Packages/com.unity.ugui

### Packages/com.unity.ugui/Editor Resources/Gizmos (2)

- TMP - Font Asset Icon.psd
- TMP - Text Component Icon.psd

### Packages/com.unity.ugui/Runtime/TMP (3)

- TMP_FontAsset.cs
- TextMeshPro.cs
- TextMeshProUGUI.cs

### Packages/com.unity.ugui/Runtime/UGUI/EventSystem (1)

- EventSystem.cs

### Packages/com.unity.ugui/Runtime/UGUI/UI/Core (8)

- Button.cs
- GraphicRaycaster.cs
- Image.cs
- RectMask2D.cs
- ScrollRect.cs
- Scrollbar.cs
- Text.cs
- Toggle.cs

### Packages/com.unity.ugui/Runtime/UGUI/UI/Core/Layout (6)

- CanvasScaler.cs
- ContentSizeFitter.cs
- GridLayoutGroup.cs
- HorizontalLayoutGroup.cs
- LayoutElement.cs
- VerticalLayoutGroup.cs

### Packages/com.unity.ugui/Runtime/UGUI/UI/Core/VertexModifiers (1)

- Outline.cs
