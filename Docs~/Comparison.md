# 제작 편의 비교

같은 연출을 도구별로 만드는 절차를 비교합니다. 근거는 이 저장소에서 실제로 만든 것들입니다.
- `Benchmarks/Assets/Bench/*Driver.cs`: 같은 연출 3가지(맥박, 목록 등장, 글자 물결)를 방식별로 구현한 코드
- Unity-AnimationUI · UnityScreenNavigator 데모 재현(`Assets/Reference`)
- RPG 샘플(`Assets/KeyTween/Samples/RPG`)

다른 도구는 이 작업에서 직접 써 본 범위만 적었고, 써 보지 않은 기능은 "확인 안 함"으로 표시했습니다. 성능은 [Performance](Performance.md)에 따로 있습니다.

## 1. 한 연출을 만드는 절차 — 버튼 맥박 (크기 + 색, 반복)

| 도구 | 만드는 곳 | 절차 | 실행 코드 |
|---|---|---|---|
| **KeyTween** | KeyTween 창 | 1. 버튼에 `KeyTween` 추가 → 2. 상태 "Pulse" 만들기, **Loop** 켜기 → 3. 트랙 추가 창에서 Scale·Color 선택 → 4. 0 / 0.5 / 1초에 키, 이징 InOutSine → 5. Play On: OnEnable | **0줄**(켜질 때 재생). 코드로 부를 때 `anim.Play("Pulse")` 1줄 |
| Animator | Animation 창 + Animator 창 | 1. Animation 창에서 Create(클립·컨트롤러 에셋 2개) → 2. Record로 키 찍기 → 3. 클립의 Loop Time 확인 → 4. 컨트롤러에서 기본 상태 확인 | 0줄(기본 상태). 다른 연출로 바꾸려면 상태·전환 또는 `Play` |
| DOTween Pro | 인스펙터(`DOTweenAnimation`) 또는 코드 | 인스펙터: 효과 하나당 컴포넌트 하나(크기 1개 + 색 1개), Loops −1·Yoyo. 코드: 아래 2줄 | `DOScale(...).SetLoops(-1, Yoyo)`, `DOColor(...)...` **2줄** |
| LitMotion | 코드 | 값마다 Motion 하나를 만들고 대상에 바인딩 | `LMotion.Create(...).WithLoops(-1, Yoyo).BindToLocalScale(rt)` 등 **2개 문장(4줄)** |
| AnimationUI | 인스펙터(`AnimationUI` 시퀀스 목록) | 크기·색 단계 + Wait + 되돌리는 단계 + Wait. 반복 기능이 없음 | 반복하려면 `OnAnimationEnded += Play` **1줄** |

## 2. 연출별로 드는 일

측정 프로젝트에서는 같은 조건으로 재려고 모든 방식을 코드로 만들었습니다. 실제로 쓸 때의 차이는 연출을 **에디터에서 만들 수 있는지**입니다.

| 연출 | KeyTween | Animator | DOTween Pro | LitMotion | AnimationUI |
|---|---|---|---|---|---|
| 맥박 반복 | 에디터(창) | 에디터(창) | 인스펙터 또는 코드 2줄 | 코드 2문장 | 인스펙터(시퀀스 4단계) + 반복 코드 1줄 |
| 목록 등장 (현재 위치 → 0) | 에디터, Start = Current Value | 에디터, 시작 값은 클립에 고정 | 코드 2줄(`From`) | 코드 2문장 | 인스펙터 |
| 글자 물결 (글자 단위) | **에디터**(Char 트랙 + Stagger) | 불가 | 코드: `DOTweenTMPAnimator` + 글자마다 반복문 | 코드: 글자마다 `BindToTMPCharPositionY` 반복문 | 불가 |
| 숫자 세기 (재화) | 에디터(Number 트랙) + 끝 값 1줄(`SetEndValue`) | 확인 안 함 | 코드 | 코드 | 불가 |
| 공유 요소 전환 | 에디터(상태) + 상대 쪽 키 1줄(`KeyTweenSharedElement`) | 확인 안 함 | 코드 | 코드 | 불가 |
| UIEffect 값 (디졸브 등) | **에디터**(UIEffect 트랙 14종) | 에디터(직렬화 필드 기록) | 코드(`DOTween.To`) | 코드(`Bind`) | 불가 |

## 3. 기능 비교 (이 저장소에서 확인한 것)

| | KeyTween | Animator | DOTween Pro | LitMotion | AnimationUI |
|---|---|---|---|---|---|
| 에디터에서 키프레임 편집 | ✅ 타임라인 창 | ✅ Animation 창 | 인스펙터 값(키프레임 아님) | ❌ (별도 패키지는 확인 안 함) | 시퀀스 목록(시작·길이) |
| 녹화 | ✅ ● (Scene·Inspector 변경 → 키) | ✅ Record | ❌ | ❌ | ❌ |
| 편집 모드 미리보기 | ✅ 재생·스크럽·반복 | ✅ | 확인 안 함 | ❌ | 확인 안 함 |
| Play 중 고친 값 남기기 | ✅ Auto Save · Save | Animation 창 편집은 남음 | ❌ | ❌ | 확인 안 함 |
| 현재 값에서 시작 | ✅ Start = Current Value | ❌ (클립 값) | ✅ `From` 생략 시 | ✅ | ❌ |
| 역재생 | ✅ `PlayReversed` (현재 값 시작도 원래 자리로) | 속도 −1(파라미터) | ✅ `PlayBackwards` | 확인 안 함 | ✅ `PlayReversed` |
| 반복 | ✅ 상태 Loop | ✅ Loop Time | ✅ | ✅ | ❌ (다시 Play) |
| 글자 단위 | ✅ 에디터 | ❌ | 코드 | 코드 | ❌ |
| 이벤트·사운드 트랙 | ✅ | 이벤트 ✅ | 콜백(코드) | 콜백(코드) | ✅ 이벤트·사운드 단계 |
| 화면 전환 컨테이너 | ✅ Page·Modal·Sheet | ❌ | ❌ | ❌ | ❌ |
| await | ✅ `Awaitable`, UniTask 선택 | ❌ | ✅ (`AsyncWaitForCompletion`) | ✅ | ❌ |
| 필요한 패키지 | Unity 공식만(uGUI·Mathematics·Burst) | 내장 | DOTween (Pro는 유료) | LitMotion (+ Burst·Collections) | AnimationUI 소스 |

## 4. 고칠 때 드는 일

| 바꾸고 싶은 것 | KeyTween | 코드 트윈 (DOTween·LitMotion) | Animator |
|---|---|---|---|
| 길이·이징 | 창에서 키를 끌거나 이징 선택, 바로 미리보기 | 코드 수정 → 컴파일 → Play로 확인 | Animation 창에서 키 이동(이징은 커브 접선) |
| 연출 추가 | 상태 하나 추가 | 메서드·필드 추가 | 클립·상태·전환 추가 |
| Prefab마다 조금 다르게 | 자식 상태 오버라이드(현재 Prefab에서) | 코드 분기 또는 값 노출 | Override Controller |
| 디자이너가 직접 | ✅ 코드 없음 | ❌ (DOTween Pro 인스펙터는 일부 가능) | ✅ |

## 5. 정리

- **KeyTween**: 연출을 코드 없이 창에서 만들고 바로 미리봅니다. 코드 트윈에서만 되던 것(현재 값 시작, 글자 단위, await)도 에디터에서 됩니다.
- **성능**(IL2CPP, 2,000개 맥박의 자체 비용): KeyTween +0.38ms로 DOTween Pro(+0.40)·LitMotion(+0.34)과 같은 수준이고, Animator(+2.85)·AnimationUI(+2.00)보다 훨씬 가볍습니다. 글자 효과는 가장 빠르고(텍스트 100개 0.61ms, 코드 트윈 약 2.1ms), 재생 중 GC 할당이 없습니다.
- **코드 트윈(LitMotion, DOTween)**: 재생 시작 비용이 가장 낮습니다(LitMotion). 대신 연출이 코드에 있어서 미리보기와 수정에 컴파일이 들어갑니다.
- **Animator**: 에디터 제작과 녹화는 되지만 UI 연출에서 가장 무겁고, 글자 단위와 현재 값 시작이 없습니다.
