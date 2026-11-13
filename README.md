# ODDITY (공개용 사본)

포트폴리오 첨부용으로 공개한 저장소입니다.
학원 저장소(KooHoo-Dev/3rd_3D_1-3D-Project-team_odd)의 커밋 이력을 브랜치와 함께 그대로 옮기고,
**외부 에셋과 수업 자료만 모든 커밋에서 제외**했습니다. 커밋 메시지·작성자·날짜는 원본과 같고, 커밋 해시는 다릅니다.
원본의 Pull Request 기록은 옮겨지지 않았습니다.

제외한 파일만 담고 있던 커밋(예: 에셋 추가 커밋)은 이력을 보존하려고 지우지 않고 빈 커밋으로 남겼습니다.

## 제외한 것

라이선스상 재배포할 수 없거나 출처가 팀이 아닌 파일입니다.

| 종류 | 경로 |
|---|---|
| 환경·소품 에셋 | `Sci-Fi Base`, `Heavy Station Kit`, `Knife`, `OZEA_STUDIO_ULTIMATE`, `Synty`, `99. External Assets`, `Models/Mission Room` |
| 캐릭터·크리처 모델 | `Models/Cyborg Characters`, `Models/Sci Fi Creatures Vol 1`, `Models/Charcharosaurus`, `Models/Perderos`, 보스 텍스처 2장 |
| 애니메이션 | `ExplosiveLLC`, `Animations/Grruzam Powerful Sword Animation`, `Animations/99. MODIFY`, `Animations`·`Animations/1. Player`의 FBX |
| 이펙트 | `VFX/Hovl Studio`, `VFX/SpecialSkillsEffectsPack` |
| 사운드 | `MASTER_Electronic Cinematic Bundle`, `Resources/Audios`, `Resources`의 음악 파일 |
| 플러그인·패키지 | Odin Inspector(`Sirenix`), DOTween, Rainbow Folders(`Borodar`), vTabs, Hierarchy Decorator, VARCO3D Bridge |
| 폰트 | `Font/aquire-font` |
| 기타 | `Fragmented Objects` 데모 씬 |
| 수업 자료 | `00. Study`, `10. Scripts`, `19. Legacy`, `Study`, `Scripts/99. Study` — 게임 코드가 쓰는 7개 파일(`SimpleInput`, `SingletonBase`, `TSVReader`, `TransformExtensions`, `LayerMaskExtensions`, `SimpleFollowCamera`, `BasicThirdPersonController`)만 남김 |

## 열어 볼 때

- Unity `6000.3.15f1`
- 위 에셋이 빠져 있어 **그대로는 실행되지 않습니다.** DOTween·Odin Inspector를 쓰는 스크립트는 컴파일 오류가 나고, 씬·프리팹의 모델·애니메이션·이펙트·사운드 참조는 비어 있습니다.
- 코드는 `Assets/Scripts/`, 에디터 도구는 `Assets/Editor/`에 있습니다.

## 포함된 폰트

Pretendard·Noto Sans KR·Orbit·Orbitron은 SIL Open Font License 1.1로 배포되는 폰트입니다.
