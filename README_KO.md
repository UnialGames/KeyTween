# KeyTween

Unity uGUI용 키프레임 UI 애니메이션 — 타임라인에서 만들고, Prefab 안에 저장하고, 이름으로 재생합니다.

[![Unity 6000.0+](https://img.shields.io/badge/unity-6000.0%2B-000.svg)](https://unity.com/releases/editor/archive)
[![Release](https://img.shields.io/github/v/release/UnialGames/KeyTween)](https://github.com/UnialGames/KeyTween/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE.md)

[English README](README.md)

<img src="Docs~/media/rpg/rpg-summon.gif" width="640" alt="RPG 샘플: 소환">

## 개요

KeyTween은 Animator 클립을 만들듯 타임라인에 키를 찍어 UI 애니메이션을 만들지만, 애니메이션은 **Prefab 안에** 이름 붙은 상태(`Hover`, `Open`, `Close` …)로 들어갑니다. 클립·컨트롤러 같은 별도 에셋이 없고, 트랙은 Prefab 안의 컴포넌트를 직접 가리킵니다. Play 모드 없이 미리보고, 언제 재생할지(포인터 진입, 클릭, 활성화 …)를 코드 없이 고르며, 코드가 필요하면 상태 이름으로 재생하고 `await`합니다.

- **코드가 아니라 에디터에서.** 타임라인 창, 트랙 추가 창, 키 인스펙터, 녹화, 편집·Prefab·Play 모드 어디서나 미리보기
- **코드는 한 줄.** `await anim.PlayAsync("Open")` — Unity `Awaitable`, 끝나지 못한 재생이 어떻게 끝나는지 규칙이 분명함
- **Prefab 그대로.** 상태가 Prefab과 함께 다니고, 바깥 Prefab이 안에 든 Prefab의 상태를 부르고 자기 안에서만 트랙을 덮어씀
- **트윈 수준의 성능.** 쉴 때 비용 0, 재생 중 GC 할당 0, 이징 트랙은 Burst로 계산

영상: [RPG 샘플](https://youtu.be/79Aw-O2kEgA) · [에디터에서 버튼 호버 만들기](https://youtu.be/13JJNE4_FwI) · [팝업 안의 버튼만 바꾸기](https://youtu.be/rXO_kNexiGc)

## 목차

- [설치](#설치)
- [시작하기](#시작하기)
- [재생과 기다리기](#재생과-기다리기)
- [여러 상태가 겹칠 때](#여러-상태가-겹칠-때)
- [실행 중에 끝 값 바꾸기](#실행-중에-끝-값-바꾸기)
- [애니메이션 이벤트](#애니메이션-이벤트)
- [코드 없이 재생 (Play On)](#코드-없이-재생-play-on)
- [중첩 Prefab: 자식 호출과 오버라이드](#중첩-prefab-자식-호출과-오버라이드)
- [샘플링, 값 읽고 쓰기](#샘플링-값-읽고-쓰기)
- [스크롤 리스트](#스크롤-리스트)
- [화면 전환](#화면-전환)
- [연동 (UniTask, R3, UIEffect)](#연동-unitask-r3-uieffect)
- [샘플](#샘플)
- [성능](#성능)
- [문서](#문서)
- [라이선스](#라이선스)

## 설치

**요구 사항:** Unity 6000.0 이상. 다음 Unity 패키지가 함께 설치됩니다: `com.unity.ugui` 2.0.0(TextMeshPro 포함), `com.unity.mathematics` 1.3.2, `com.unity.burst` 1.8.25.

**Window > Package Manager**에서 **+ > Add package from git URL…**을 누르고 아래 주소를 넣습니다.

```
https://github.com/UnialGames/KeyTween.git#v0.1.1
```

또는 `Packages/manifest.json`에 추가합니다.

```json
{
  "dependencies": {
    "com.unialgames.keytween": "https://github.com/UnialGames/KeyTween.git#v0.1.1"
  }
}
```

`#v0.1.1` 대신 `#main`을 쓰면 최신 릴리스를 따라갑니다. **샘플:** Package Manager에서 KeyTween을 고르고 **Samples** 탭에서 Import합니다([샘플](#샘플)).

## 시작하기

### 1. 에디터에서 호버 연출 만들기

포인터를 올리면 버튼이 1.1배로 커지며 밝아지고, 빼면 돌아옵니다. 코드는 없습니다. (전체 영상: [Button Hover Editor](https://youtu.be/13JJNE4_FwI))

1. 버튼 Prefab을 열고 **Window > KeyTween Editor**를 엽니다.
2. **Add KeyTween to 'Button'**을 누릅니다(`KeyTween` 컴포넌트를 직접 붙여도 됩니다).
3. 상태 탭의 **+**로 상태를 만들고, 상태 인스펙터에서 이름을 `Hover`로 바꿉니다.
4. 타임라인 빈 곳을 우클릭 → **Add Track…** → `RectTransform` 아래 **Scale**, `Image` 아래 **Color**를 고르고 → **Add 2**. 트랙마다 0초 키와 끝 키(0.2초)가 생깁니다.

   <img src="Docs~/media/editor/editor-add-track.gif" width="640" alt="트랙 추가">

5. Scale의 끝 키를 클릭해 Value `1.1`, Ease `OutBack`. Color의 끝 키는 조금 밝은 색으로 바꿉니다.
6. 눈금자를 끌거나 **▶**로 미리봅니다. Play 모드가 필요 없습니다.

   <img src="Docs~/media/editor/editor-preview.gif" width="640" alt="미리보기">

7. 상태 인스펙터의 **Play On**을 `PointerEnter`로 고릅니다.
8. 같은 방법으로 `Unhover`를 만듭니다. 끝 키는 `1`과 원래 색, Play On은 `PointerExit`.
9. Play 모드에서 버튼 위로 포인터를 올렸다 뺍니다.

   <img src="Docs~/media/editor/editor-play-mode.gif" width="494" alt="Play 모드">

### 2. 코드에서 재생하기

```csharp
using System;
using KeyTweening;
using UnityEngine;

public class Popup : MonoBehaviour
{
    [SerializeField] KeyTween anim;

    public async Awaitable Open() => await anim.PlayAsync("Open");

    public async Awaitable Close()
    {
        try { await anim.PlayAsync("Close", cancellationToken: destroyCancellationToken); }
        catch (OperationCanceledException) { return; }   // 닫는 도중 다시 열림 → 그대로 둠
        Destroy(gameObject);
    }
}
```

애니메이션 자체(시간, 이징, 어떤 속성)는 에디터에 있습니다. 코드는 *어느 상태*를 재생하고 *끝난 뒤 무엇을 할지*만 정합니다.

## 재생과 기다리기

```csharp
anim.Play("Hover");                                // 재생만. 0초 모습을 바로 씀
await anim.PlayAsync("Open");                      // 끝나면(또는 Complete되면) 완료
await anim.PlayReversedAsync("Open");              // 끝 → 시작. 이벤트·사운드·자식 호출도 거꾸로
anim.Complete("Typewriter");                       // 끝으로 건너뜀. 남은 이벤트는 순서대로 발생
anim.Stop("Idle");                                 // 그 자리에서 멈춤(기다리던 PlayAsync는 취소)
anim.Sample("Collapse", 0.25f);                    // 재생하지 않고 그 시간의 모습만
bool playing = anim.IsPlaying("Open");
float length = anim.Duration("Open");              // 오버라이드 반영. 없는 상태면 0
```

`PlayAsync`가 끝나는 경우:

| 경우 | `PlayAsync` | 남은 이벤트 | 남은 사운드 |
|---|---|---|---|
| 끝에 도달 | 완료 | 모두 발생 | 모두 재생 |
| `Complete` (또는 다른 상태가 인터럽트 `Complete`로 시작) | 완료 | 순서대로 발생 | 재생 안 함 |
| 다른 상태가 속성을 모두 가져감 | **취소** | 발생 안 함 | 재생 안 함 |
| 같은 상태를 다시 재생 | 이전 재생 **취소** | 발생 안 함 | 재생 안 함 |
| 오브젝트 비활성화, `Stop`, 토큰 취소 | **취소** | 발생 안 함 | 재생 안 함 |

취소는 `OperationCanceledException`이라서, "정말 끝났을 때만 다음 동작"이 기본이 됩니다. 반복 상태의 `PlayAsync`는 `Complete`나 `Stop`으로만 끝납니다. 시간은 실시간입니다(`Time.timeScale`을 따르지 않음). 없는 상태 이름은 경고를 남기고 바로 완료됩니다.

**예: 대사창.** 글을 바꾸고 같은 타자기 상태를 재생합니다. 건너뛰기는 `Complete`입니다.

```csharp
dialogue.text = nextLine;
var typing = anim.PlayAsync("Typewriter");
// 건너뛰기 버튼: anim.Complete("Typewriter");
await typing;
```

## 여러 상태가 겹칠 때

여러 상태가 동시에 재생될 수 있습니다. 새 상태는 **자기가 움직이는 속성만** 가져오고, 나머지는 계속 재생됩니다(호버 중에 누르기). 가져간 상태를 어떻게 할지는 인터럽트로 정합니다.

| `KeyTweenInterrupt` | 동작 |
|---|---|
| `Cancel` (기본) | 겹치는 속성을 그 자리에서 가져옵니다. 속성을 모두 잃은 상태는 멈춥니다. 지금 값에서 출발하는 트랙은 그 자리에서 이어집니다. |
| `Complete` | 겹치는 상태를 먼저 끝 값으로 맞춥니다. |

```csharp
anim.Play("Close", KeyTweenInterrupt.Complete);    // Open을 끝낸 뒤 그 끝 모습에서 닫기
```

Play On으로 재생할 때도 상태 인스펙터에서 같은 설정을 고릅니다.

## 실행 중에 끝 값 바꾸기

재화·전투력처럼 실행 중에야 아는 값은, 움직임은 에디터에서 만들고 끝 값만 넘깁니다. 다음 재생부터 그 트랙의 마지막 키를 바꾸며, 저장된 에셋은 바뀌지 않습니다.

```csharp
// 금화 라벨의 "Count" 상태: Number (Count) 트랙, 0.6초 OutQuad (에디터에서 만듦)
counter.SetEndValue("Count", goldLabel, KeyTweenProperty.TextNumber, gold);
counter.Play("Count");
```

`SetEndValue(state, target, property, Vector4)`와 `float` 오버로드(x만). 키 값과 똑같이 해석됩니다(Relative To Rest, Size Relative 반영).

## 애니메이션 이벤트

에디터의 이벤트 트랙에 이벤트를 놓고 코드에서 받습니다. 이벤트에 데이터를 실을 수도 있습니다. `[KeyTweenEvent]`를 붙인 struct는 에디터의 이벤트 형식 목록에 나오고, public 필드가 저장됩니다.

```csharp
[KeyTweenEvent("Popup Sound")]
public struct PopupSoundEvent { public string clip; public float volume; }

anim.EventRaised += (in KeyTweenEventInfo e) => Debug.Log($"{e.State}:{e.Name} at {e.Time}");

using var sub = anim.AddListener<PopupSoundEvent>((in KeyTweenEventInfo e, in PopupSoundEvent data) =>
    Debug.Log($"play {data.clip} at {data.volume}"));

KeyTweenEventInfo shown = await anim.WaitEventAsync("Shown");   // 어느 상태든 다음 "Shown"까지
```

`KeyTweenEventInfo`(`in`으로 전달): `Animation`, `State`, `Name`, `Time`, `TryGetData<T>(out T)`. `Complete`는 건너뛴 이벤트를 순서대로 발생시키고, 취소된 재생은 발생시키지 않습니다. 코드 없이 받기: **Add Component > KeyTween > Event Listener**에서 이벤트 이름 → UnityEvent를 연결합니다.

**사운드**는 따로 트랙이 있습니다(시간마다 클립과 볼륨). `KeyTween.audioSource`를 지정하면 그 소스로, 비우면 숨은 공용 2D 소스 하나로 재생합니다.

## 코드 없이 재생 (Play On)

상태 인스펙터의 **Play On**으로 재생 시점을 고릅니다. 에디터가 `KeyTweenTrigger`를 붙여 줍니다.

| Play On | 재생되는 때 |
|---|---|
| `None` | 코드에서만 |
| `OnEnable` / `Start` | 컴포넌트가 켜질 때 / 처음 켜진 뒤 한 번 |
| `PointerEnter` / `PointerExit` / `PointerDown` / `PointerUp` / `Click` | 포인터 이벤트 |
| `Select` / `Deselect` | 선택 이벤트 |
| `ToggleOn` / `ToggleOff` | 같은 오브젝트 `Toggle`의 값 변경 |

`interactable`이 꺼진 Selectable은 입력 이벤트를 무시합니다. Button `OnClick` 같은 UnityEvent에서는 `KeyTween.PlayState(string)`(역재생은 `PlayStateReversed`)을 연결합니다.

## 중첩 Prefab: 자식 호출과 오버라이드

공용 Prefab(버튼)을 다른 Prefab(팝업) 안에 넣었을 때:

- **자식 호출** — 팝업의 `Open` 상태가 0.25초에 버튼의 `Hover`를 부릅니다(타임라인 우클릭 → **Add Child Call ▸ 자식 ▸ 상태**). 부모 타임라인에 막대로 보이고, 부모 길이에 포함됩니다.
- **오버라이드** — 호출 아래에 불린 상태의 트랙이 흐리게 보입니다. 하나를 클릭하면 **그 트랙만** 팝업에서 덮어씁니다. 버튼 Prefab은 바뀌지 않고, 손대지 않은 트랙은 계속 원본을 따릅니다. 여러 단계(화면 > 팝업 > 버튼)에서는 가장 바깥이 이깁니다(Unity 중첩 Prefab 규칙과 같음).

영상: [팝업 안의 버튼만 바꾸기](https://youtu.be/rXO_kNexiGc) · 자세히: [Guide §12](Docs~/Guide.md#12-자식-호출과-오버라이드-중첩-prefab)

## 샘플링, 값 읽고 쓰기

```csharp
// 시간이 아닌 값으로 상태의 모습을 정함. 예: 헤더가 접힌 정도(0..1)
anim.Sample("Collapse", progress * anim.Duration("Collapse"));

// 트랙과 같은 방식으로 속성 하나를 읽고 씀(한 값짜리 속성은 x)
Vector4 color = KeyTween.Read(image, KeyTweenProperty.GraphicColor);
KeyTween.Write(label, KeyTweenProperty.TextNumber, new Vector4(1234, 0, 0, 0));   // "1,234"
```

## 스크롤 리스트

`KeyTweenScrollList`는 `ScrollRect`에 붙이는 **가상화** 리스트입니다. 보이는 아이템만 만들고 스크롤하면 재사용합니다. 모든 형태는 이 컴포넌트 하나의 설정이고, 모든 연출은 아이템 `KeyTween`의 상태입니다(Stagger 진입, 등장, 뷰포트 안 위치로 정하는 스크럽 상태).

```csharp
// UI Toolkit ListView처럼 연결. ItemView는 아이템 Prefab에 붙은 아무 컴포넌트
list.SetData(items, (ItemView view, Item item, int index) => view.Set(item));
list.SetCount(100, (ItemView view, int index) => view.Set(items[index]),
                   (ItemView view, int index) => view.Clear());          // unbind는 선택

list.prefabOf = i => items[i].isHeader ? 1 : 0;   // 여러 종류 아이템(SetCount 전에)
list.SetCount(items.Count);                        // 개수만: 스크롤 위치 유지, 새 아이템만 등장
list.ScrollTo(20, KeyTweenScrollList.Alignment.Center, 0.3f);
list.CurrentIndexChanged += i => indicator.text = $"{i + 1} / {list.Count}";

list.RemoveItem(5);                                // 데이터에서 지운 뒤: 아래 아이템이 올라옴
await list.ResizeItem(3, 0f);                      // 아코디언: 한 아이템 크기를 애니메이션한 뒤 지우기
```

배치: 세로, 가로, 그리드, 메이슨리. 여기에 루프, 스냅(가까운 쪽 / 한 번에 한 칸), 자동 스크롤, 끝 유지, 섹션 헤더 고정을 더해 캐러셀, 페이지, 배너, 휠 피커, 채팅, 아코디언, 탭, 접히는 헤더, 당겨서 새로고침을 만듭니다. 도우미: `KeyTweenPageIndicator`, `KeyTweenScrollScrub`, `KeyTweenPullToRefresh`. **UI Components** 샘플에 18가지 예시가 있습니다. 자세히: [ScrollList](Docs~/ScrollList.md)

## 화면 전환

Page, Modal, Sheet 컨테이너(UnityScreenNavigator와 같은 역할)가 패키지 안에 있습니다. 전환 연출은 화면 Prefab `KeyTween`의 이름 붙은 상태이고, 없으면 기본 전환을 씁니다. 모두 `Awaitable`입니다.

```csharp
await pages.Push(loadingPage, stack: false);   // 다음 페이지가 들어오면 기록에서 빠지고 파괴됨
await pages.Push(homePage);
await pages.Pop();

await modals.Push(confirmPopup);               // 배경이 함께 생김. 상태 "Enter" / "Exit"
await modals.Pop();

int shop = sheets.Register(shopTabPrefab);     // 탭
await sheets.Show(shop);
```

| 컨테이너 | 화면의 상태 | 기본 전환 (0.3초) |
|---|---|---|
| `KeyTweenPageContainer` | `PushEnter`, `PushExit`, `PopEnter`, `PopExit` | 한 화면 폭 슬라이드 |
| `KeyTweenModalContainer` | `Enter`, `Exit` (배경도 같은 이름) | 크기 0.3 → 1 + 페이드 |
| `KeyTweenSheetContainer` | `Enter`, `Exit`, `EnterBackward`, `ExitBackward` | 페이드 |

전환 중에는 모든 컨테이너의 입력이 막힙니다. 화면의 컴포넌트가 `IKeyTweenScreenLifecycle`을 구현하면 불립니다(모든 메서드는 선택).

```csharp
public sealed class HomePage : MonoBehaviour, IKeyTweenScreenLifecycle
{
    public async Awaitable WillEnter(KeyTweenTransition t) => await LoadAsync();   // 애니메이션 전에 기다림
    public void DidEnter(KeyTweenTransition t) { }
    public Awaitable WillExit(KeyTweenTransition t) => KeyTweenScreen.Completed();
    public void DidExit(KeyTweenTransition t) { }
    Awaitable LoadAsync() => KeyTweenScreen.Completed();
}
```

**공유 요소 전환** — 목록의 초상화가 상세 페이지의 초상화 자리로 날아갑니다. 움직임은 에디터에서 만든 Position·Size 트랙이고, 실행 중에는 상대 쪽 화면상 사각형만 채웁니다.

```csharp
public Awaitable WillEnter(KeyTweenTransition t)
{
    KeyTweenSharedElement.SetPartnerKeys(GetComponent<KeyTween>(), "PushEnter", portrait, card, atStart: true);
    return KeyTweenScreen.Completed();
}
```

자세히: [Screens](Docs~/Screens.md)

## 연동 (UniTask, R3, UIEffect)

패키지는 서드파티 없이 동작합니다. 아래 라이브러리가 설치돼 있으면 연동 코드가 자동으로 컴파일됩니다(asmdef version defines). 설정은 없습니다.

```csharp
// UniTask (com.cysharp.unitask 2.0 이상)
await UniTask.WhenAll(popup.PlayUniTaskAsync("Open"), dim.PlayUniTaskAsync("Show"));
bool canceled = await popup.PlayUniTaskAsync("Close").SuppressCancellationThrow();
KeyTweenEventInfo info = await anim.WaitEventUniTaskAsync("Shown");

// R3 (com.cysharp.r3 1.0 이상)
open.OnClickAsObservable().ThrottleFirst(TimeSpan.FromSeconds(0.5))
    .SelectMany(_ => popup.PlayAsObservable("Open"))
    .Subscribe(_ => Debug.Log("opened")).AddTo(this);
popup.EventAsObservable("Shown").Subscribe(e => Debug.Log(e.Time)).AddTo(this);
```

| 라이브러리 | 추가되는 것 |
|---|---|
| UniTask | `PlayUniTaskAsync`, `PlayReversedUniTaskAsync`, `WaitEventUniTaskAsync` (완료·취소 규칙은 `PlayAsync`와 같음) |
| R3 | `PlayAsObservable`(구독하면 재생, 끝나면 한 번 내보냄, 구독을 끊으면 멈춤), `EventsAsObservable`, `EventAsObservable` |
| UIEffect (`com.coffee.ui-effect` 5.0 이상) | 트랙 14종: 톤, 색, 샘플링, 트랜지션(디졸브 등), 엣지 샤인, 그라데이션, 디테일 |

자세히: [Integrations](Docs~/Integrations.md)

## 샘플

| 샘플 | 받는 곳 | 내용 | 필요 |
|---|---|---|---|
| **RPG Sample** | Package Manager › Samples | 모바일 RPG UI 한 벌: 타이틀, 로비, 영웅·강화, 가방, 소환, 모험, 전투, 퀘스트, 상점, 우편, 길드 채팅, 랭킹, 스토리, 설정. 모든 움직임이 Prefab의 상태이고 스크립트는 화면을 열고 데이터를 채우기만 함. 장면 `Scenes/RPG.unity` | Input System |
| **UI Components** | Package Manager › Samples | 버튼, 호버, 토글, 팝업, 글자 효과, 스크롤 리스트 18종. 씬마다 하나씩 | Input System |

TMP Essential Resources(TextMeshPro의 설정과 셰이더)가 없는 프로젝트에 샘플을 Import하면 자동으로 함께 가져옵니다. UniTask·R3 예제는 [Integrations](Docs~/Integrations.md)에 있습니다.

| 영웅 목록 → 상세(공유 요소 전환) → 강화 | 모험 → 편성 → 전투 → 승리 |
|---|---|
| ![영웅](Docs~/media/rpg/rpg-heroes.gif) | ![전투](Docs~/media/rpg/rpg-battle.gif) |
| **랭킹: 긴 목록 스크롤** | **우편 받기 · 출석 보상** |
| ![랭킹](Docs~/media/rpg/rpg-ranking.gif) | ![우편](Docs~/media/rpg/rpg-mail.gif) |

## 성능

Windows 플레이어, IL2CPP 릴리스, 2,000개. 괄호는 같은 값을 스크립트로 직접 넣는 경우보다 더 든 시간, 즉 그 방식 자체의 비용입니다.

| | KeyTween | DOTween Pro | LitMotion | Animator | AnimationUI |
|---|---:|---:|---:|---:|---:|
| 맥박 반복 (ms) | 7.69 (+0.38) | 7.71 (+0.40) | 7.66 (+0.34) | 10.16 (+2.85) | 9.31 (+2.00) |
| 목록 등장 (ms) | 1.70 (+0.04) | 1.59 (−0.06) | 1.53 (−0.13) | 5.23 (+3.57) | 2.18 (+0.52) |
| 글자 물결 100개 (ms) | **0.61** | 2.11 | 2.05 | 지원 안 함 | 지원 안 함 |

- 재생 중 GC 할당이 없고, 코드 트윈(DOTween Pro·LitMotion)과 같은 수준이며 Animator·AnimationUI보다 가볍습니다. 글자 효과는 가장 빠릅니다.
- 약한 곳은 재생 시작 비용입니다(2,000개를 한 번에 다시 재생: 2.31ms, LitMotion 1.34ms).

측정 방법과 전체 표: [Performance](Docs~/Performance.md), [Benchmarks](Docs~/Benchmarks.md)

## 문서

| 문서 | 내용 |
|---|---|
| [Guide](Docs~/Guide.md) | 에디터: 창, 상태·트랙·키, 미리보기, 저장, 녹화, 중첩 Prefab 오버라이드 |
| [API](Docs~/API.md) | 코드: 재생, 기다리기, 이벤트, 런타임 값 |
| [ScrollList](Docs~/ScrollList.md) | 스크롤 리스트와 도우미 컴포넌트 |
| [Screens](Docs~/Screens.md) | 화면 전환 컨테이너, 생명주기, 공유 요소 |
| [Integrations](Docs~/Integrations.md) | UniTask, R3, UIEffect |
| [Comparison](Docs~/Comparison.md) | 다른 도구와 제작 편의 비교 |
| [Performance](Docs~/Performance.md) · [Benchmarks](Docs~/Benchmarks.md) | 위 수치의 측정 방법과 전체 결과 표 |

## 라이선스

[MIT](LICENSE.md). RPG 샘플의 아이콘은 [game-icons.net](https://game-icons.net)(CC BY 3.0, 작가는 `Samples/RPG/Art/CREDITS.md`), 글꼴은 Passion One(SIL OFL 1.1)입니다.
