# KeyTween

KeyTween은 Unity uGUI용 키프레임 UI 애니메이션 도구입니다. 애니메이션을 타임라인에서 만들어 Prefab 안에 저장하고, 이름으로 재생합니다.

[![Unity 6000.0+](https://img.shields.io/badge/unity-6000.0%2B-000.svg)](https://unity.com/releases/editor/archive)
[![Release](https://img.shields.io/github/v/release/UnialGames/KeyTween)](https://github.com/UnialGames/KeyTween/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE.md)

[Document](https://unialgames.github.io/KeyTween/articles/ko/overview.html) · [English README](README.md)

<img src="Docs~/media/rpg/rpg-summon.gif" width="640" alt="RPG 샘플: 소환">

## 개요

KeyTween에서는 Animator 클립을 만들 때처럼 타임라인에 키를 놓아 UI 애니메이션을 만듭니다. 다만 애니메이션은 클립이 아니라 `Hover`, `Open`, `Close`처럼 이름 붙은 상태로 **Prefab 안에** 저장됩니다.

### 주요 특징

* **별도 에셋이 없습니다.** 클립이나 컨트롤러 같은 에셋이 없고, 각 트랙은 Prefab 안의 컴포넌트를 직접 가리킵니다.
* **에디터에서 만듭니다.** 타임라인 창, 트랙 추가 창, 키 인스펙터, 녹화 기능으로 애니메이션을 만듭니다.
* **Play 모드 없이 미리 봅니다.** 미리보기는 편집 모드, Prefab 모드, Play 모드 어디서나 동작합니다.
* **재생 시점을 코드 없이 정합니다.** 포인터 진입, 클릭, 활성화 같은 시점을 상태마다 고릅니다.
* **코드는 한 줄이면 됩니다.** `await anim.PlayAsync("Open")`은 상태를 재생하고 Unity `Awaitable`을 돌려줍니다. 재생이 끝까지 가지 못했을 때 어떻게 끝나는지도 규칙으로 정해져 있습니다.
* **Prefab과 함께 움직입니다.** 상태는 Prefab에 저장되어 Prefab과 함께 다닙니다. 바깥 Prefab은 안에 든 Prefab의 상태를 호출하고 그 트랙을 덮어쓸 수 있습니다. 덮어쓴 내용은 바깥 Prefab 안에서만 적용됩니다.
* **트윈 라이브러리 수준의 성능을 냅니다.** 재생하지 않을 때는 비용이 들지 않고, 재생 중에도 GC 할당이 없습니다. 이징 트랙은 Burst로 계산합니다.

### 영상으로 보기

실제로 동작하는 모습은 다음 영상에서 볼 수 있습니다.

* [RPG 샘플](https://youtu.be/79Aw-O2kEgA)
* [에디터에서 버튼 호버 만들기](https://youtu.be/13JJNE4_FwI)
* [팝업 안의 버튼만 바꾸기](https://youtu.be/rXO_kNexiGc)

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

KeyTween을 프로젝트에 설치하는 방법을 설명합니다.

### 요구 사항

* Unity 6000.0 이상
* 함께 설치되는 패키지
  * `com.unity.ugui` 2.0.0 (TextMeshPro 포함)
  * `com.unity.mathematics` 1.3.2
  * `com.unity.burst` 1.8.25

### Package Manager로 설치 (권장)

Package Manager에서 Git 주소로 설치합니다.

1. **Window > Package Manager**를 엽니다.
2. **+** 버튼을 누르고 **Add package from git URL...** 항목을 고릅니다.
3. 다음 주소를 입력합니다.

```text
https://github.com/UnialGames/KeyTween.git
```

`Packages/manifest.json`의 `dependencies`에 직접 추가할 수도 있습니다.

```json
{
  "dependencies": {
    "com.unialgames.keytween": "https://github.com/UnialGames/KeyTween.git"
  }
}
```

### 업데이트

위 주소는 항상 최신 릴리스를 가리킵니다. 새 버전이 나오면 Package Manager에서 KeyTween을 고르고 **Update**를 누릅니다.

### 특정 버전으로 설치하기

주소 뒤에 `#`과 버전 태그를 붙이면 그 버전에 고정됩니다. 버전 목록은 [Releases](https://github.com/UnialGames/KeyTween/releases)에 있습니다.

```text
https://github.com/UnialGames/KeyTween.git#v0.1.3
```

* 고정한 버전은 **Update**를 눌러도 바뀌지 않습니다.
* 버전을 바꾸려면 `Packages/manifest.json`의 태그를 고칩니다.
* 최신 릴리스를 다시 따라가려면 주소에서 `#v…`를 지웁니다.

### 샘플 가져오기

1. Package Manager에서 KeyTween을 고릅니다.
2. **Samples** 탭에서 원하는 샘플의 **Import**를 누릅니다.

샘플은 프로젝트 안으로 복사됩니다. 패키지를 업데이트한 뒤에는 샘플을 다시 Import해야 새 버전이 반영됩니다.

## 시작하기

에디터에서 첫 애니메이션을 만들고, 코드에서 상태를 재생하는 방법을 알아봅니다. *상태*란 `Hover`나 `Open`처럼 이름이 붙은 애니메이션 하나를 말하며, Prefab의 `KeyTween` 컴포넌트에 저장됩니다.

### 1. 에디터에서 호버 연출 만들기

포인터를 올리면 버튼이 1.1배로 커지면서 밝아지고, 포인터를 빼면 원래대로 돌아오게 만듭니다. 코드는 작성하지 않습니다.

다 만들고 나면 KeyTween 창은 아래처럼 보입니다.

* `Hover`와 `Unhover` 상태
* 버튼의 Scale 트랙과 Color 트랙
* 오른쪽의 키 인스펙터

![Hover와 Unhover 상태가 있는 KeyTween 창](Docs~/media/editor/editor-window.png)

1. 버튼 Prefab을 열고 **Window > KeyTween Editor**를 엽니다.
2. **Add KeyTween to 'Button'** 버튼을 누릅니다. `KeyTween` 컴포넌트를 직접 추가해도 됩니다.
3. 상태 탭의 **+** 버튼을 눌러 상태를 만들고, 상태 인스펙터에서 이름을 `Hover`로 바꿉니다. 새 상태는 타임라인이 비어 있으며, 상태 인스펙터에는 Name, Loop, Duration, Play On 설정이 나옵니다.

   ![새로 만든 Hover 상태와 상태 인스펙터](Docs~/media/editor/editor-state.png)

4. 타임라인의 빈 곳을 우클릭하고 **Add Track…** 항목을 고릅니다.
5. `RectTransform` 아래의 **Scale**과 `Image` 아래의 **Color**를 선택한 뒤 **Add 2**를 누릅니다. 트랙마다 0초 키와 0.2초 끝 키가 만들어집니다.

   <img src="Docs~/media/editor/editor-add-track.gif" width="640" alt="Add Track으로 트랙 추가">

6. Scale 트랙의 끝 키를 클릭하고 Value를 `1.1`, Ease를 `OutBack`으로 설정합니다.
7. Color 트랙의 끝 키를 조금 더 밝은 색으로 바꿉니다.
8. 눈금자를 끌거나 **▶** 버튼을 눌러 미리 봅니다. Play 모드에 들어갈 필요가 없습니다.

   <img src="Docs~/media/editor/editor-preview.gif" width="640" alt="에디터에서 미리보기">

9. 상태 인스펙터에서 **Play On**을 `PointerEnter`로 설정합니다.
10. 같은 방법으로 `Unhover` 상태를 만듭니다. 끝 키는 `1`과 원래 색으로 되돌리고, Play On은 `PointerExit`로 설정합니다.
11. Play 모드에 들어가 버튼 위에 포인터를 올렸다가 뺍니다.

    <img src="Docs~/media/editor/editor-play-mode.gif" width="494" alt="Play 모드에서 본 호버 연출">

전체 과정은 [Button Hover Editor](https://youtu.be/13JJNE4_FwI) 영상에서 볼 수 있습니다. 에디터의 자세한 사용법은 [Guide](Docs~/Guide.md)를 참고하세요.

### 2. 코드에서 재생하기

스크립트에서 상태를 재생하려면 `KeyTween` 컴포넌트를 참조하고 상태 이름으로 호출합니다. 아래 팝업은 열릴 때 `Open`을 재생하고, `Close`가 끝나면 자신을 파괴합니다.

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
        catch (OperationCanceledException) { return; }   // 닫는 도중 다시 열렸으면 팝업을 그대로 둠
        Destroy(gameObject);
    }
}
```

에디터와 코드는 다음과 같이 역할을 나눕니다.

* 에디터는 애니메이션 자체를 정합니다. 시간, 이징, 움직일 속성이 여기에 속합니다.
* 코드는 *어느 상태*를 재생할지와 *끝난 뒤 무엇을 할지*만 정합니다.

자세한 내용은 [API](Docs~/API.md#재생)를 참고하세요.

## 재생과 기다리기

`KeyTween` 컴포넌트는 상태를 이름으로 재생합니다. 재생 메서드와 재생이 끝나는 규칙을 설명합니다.

### 주요 메서드

```csharp
anim.Play("Hover");                                // 기다리지 않고 재생. 0초 모습을 바로 적용
await anim.PlayAsync("Open");                      // 끝까지 재생되면(또는 Complete되면) 완료
await anim.PlayReversedAsync("Open");              // 끝에서 시작으로 재생. 이벤트, 사운드, 자식 호출도 거꾸로 실행
anim.Complete("Typewriter");                       // 끝으로 건너뜀. 남은 이벤트는 순서대로 발생
anim.Stop("Idle");                                 // 현재 모습에서 멈춤. 기다리던 PlayAsync는 취소됨
anim.Sample("Collapse", 0.25f);                    // 재생하지 않고 지정한 시간의 모습만 보여 줌
bool playing = anim.IsPlaying("Open");
float length = anim.Duration("Open");              // 오버라이드를 반영한 길이. 없는 상태면 0
```

* 재생 시간은 실제 시간을 따르며, `Time.timeScale`의 영향을 받지 않습니다.
* 없는 상태 이름을 넘기면 경고를 남기고 바로 완료됩니다.

### 재생이 끝나는 경우

`PlayAsync`가 끝나는 방식은 경우마다 다릅니다.

| 경우 | `PlayAsync` | 남은 이벤트 | 남은 사운드 |
|---|---|---|---|
| 끝에 도달 | 완료 | 모두 발생 | 모두 재생 |
| `Complete` | 완료 | 순서대로 발생 | 재생 안 함 |
| 다른 상태가 속성을 모두 가져감 | **취소** | 발생 안 함 | 재생 안 함 |
| 같은 상태를 다시 재생 | 이전 재생 **취소** | 발생 안 함 | 재생 안 함 |
| 오브젝트 비활성화, `Stop`, 토큰 취소 | **취소** | 발생 안 함 | 재생 안 함 |

* 취소된 재생은 `OperationCanceledException`을 던집니다.
* 따라서 `await` 뒤의 코드는 상태가 정말 끝까지 재생됐을 때만 실행됩니다. 대부분의 경우 이것이 원하는 동작입니다.
* 반복 상태의 `PlayAsync`는 `Complete`나 `Stop`을 호출해야만 끝납니다.

자세한 내용은 [끝나는 경우](Docs~/API.md#끝나는-경우)를 참고하세요.

### 예: 대사창

다음 대사를 보여 줄 때는 글을 바꾸고 같은 타자기 상태를 다시 재생합니다. 글자가 다 나오기 전에 건너뛰려면 `Complete`를 호출합니다.

```csharp
dialogue.text = nextLine;
var typing = anim.PlayAsync("Typewriter");
// 건너뛰기 버튼: anim.Complete("Typewriter");
await typing;
```

### 여러 KeyTween을 함께 기다리기

`PlayAsync`는 `Task`가 아니라 Unity `Awaitable`을 돌려주므로 `Task.WhenAll`에 바로 넘길 수 없습니다. 재생은 호출하는 순간 시작되므로, 먼저 모두 시작해 두고 하나씩 차례로 기다리면 됩니다.

```csharp
var plays = anims.Select(a => a.PlayAsync("Hover")).ToList();   // 여기서 모두 시작
foreach (var play in plays) await play;                         // 모두 끝날 때까지 기다림

// Task로 바꾸어 기다리기:   static async Task AsTask(Awaitable a) => await a;
await Task.WhenAll(anims.Select(a => AsTask(a.PlayAsync("Hover"))));

// UniTask로 기다리기:
await UniTask.WhenAll(anims.Select(a => a.PlayUniTaskAsync("Hover")));
```

자세한 내용은 [여러 KeyTween을 함께 기다리기](Docs~/API.md#여러-keytween을-함께-기다리기)를 참고하세요.

## 여러 상태가 겹칠 때

한 `KeyTween`의 여러 상태가 동시에 재생될 수 있습니다. 상태끼리 속성을 나누는 방식을 설명합니다.

### 속성을 넘겨받는 방식

* 새 상태가 시작되면 **자기가 움직이는 속성만** 넘겨받습니다.
* 넘겨받은 속성은 지금 값에서 이어서 움직입니다. 그래서 호버가 중간에 끊겨도 모습이 튀지 않습니다.
* 다른 상태는 자기에게 남은 속성을 계속 재생합니다. 예를 들어 호버 중에 버튼을 누르면, 누르기 상태가 쓰지 않는 속성은 호버 상태가 계속 움직입니다.
* 속성을 모두 잃은 상태는 멈추고, 그 상태의 `PlayAsync`는 취소됩니다.

### 앞 상태를 끝내고 다음 상태 시작하기

다음 상태를 시작하기 전에 앞 상태를 끝내야 한다면 먼저 `Complete`를 호출합니다. 그러면 앞 상태가 끝 모습에 도달하고, 남은 이벤트가 발생하며, 그 상태를 기다리던 `await`가 완료됩니다.

```csharp
anim.Complete("Open");    // Open이 끝 모습으로 건너뜀. 기다리던 await는 완료되고 "Shown" 이벤트도 발생
anim.Play("Close");       // 그다음 열린 모습에서 Close 시작
```

자세한 내용은 [여러 상태가 겹칠 때](Docs~/Guide.md#11-여러-상태가-겹칠-때)를 참고하세요.

## 실행 중에 끝 값 바꾸기

재화나 전투력처럼 실행 중에야 알 수 있는 값이 있습니다. 이런 값은 움직임을 에디터에서 만들어 두고, 코드에서 끝 값만 넘깁니다.

### 끝 값 넘기기

```csharp
// 금화 라벨의 "Count" 상태: Number (Count) 트랙, 0.6초 OutQuad (에디터에서 만듦)
counter.SetEndValue("Count", goldLabel, KeyTweenProperty.TextNumber, gold);
counter.Play("Count");
```

* 넘긴 값은 다음 재생부터 그 트랙의 마지막 키를 대신합니다.
* 저장된 에셋은 바뀌지 않습니다.

### 넘긴 값이 적용되는 방식

* `SetEndValue(state, target, property, Vector4)`에는 x 값만 바꾸는 `float` 오버로드도 있습니다.
* 넘긴 값은 키 값과 똑같이 해석됩니다. 트랙의 Relative To Rest와 Size Relative 설정이 그대로 적용됩니다.

자세한 내용은 [런타임에 끝 값 바꾸기](Docs~/API.md#런타임에-끝-값-바꾸기)를 참고하세요.

## 애니메이션 이벤트

이벤트는 에디터에서 이벤트 트랙에 놓고, 코드에서 받습니다. 이벤트를 받는 방법과 사운드 트랙을 설명합니다.

### 이벤트에 데이터 싣기

`[KeyTweenEvent]`를 붙인 struct는 에디터의 이벤트 형식 목록에 나타납니다. 그 public 필드가 이벤트와 함께 저장됩니다.

```csharp
[KeyTweenEvent("Popup Sound")]
public struct PopupSoundEvent { public string clip; public float volume; }
```

### 코드에서 이벤트 받기

```csharp
anim.EventRaised += (in KeyTweenEventInfo e) => Debug.Log($"{e.State}:{e.Name} at {e.Time}");

using var sub = anim.AddListener<PopupSoundEvent>((in KeyTweenEventInfo e, in PopupSoundEvent data) =>
    Debug.Log($"play {data.clip} at {data.volume}"));

KeyTweenEventInfo shown = await anim.WaitEventAsync("Shown");   // 어느 상태에서든 다음 "Shown"이 올 때까지 기다림
```

핸들러는 `KeyTweenEventInfo`를 `in`으로 전달받습니다. 여기에는 다음 멤버가 있습니다.

* `Animation`
* `State`
* `Name`
* `Time`
* `TryGetData<T>(out T)`

`Complete`는 건너뛴 이벤트를 순서대로 발생시킵니다. 취소된 재생은 이벤트를 발생시키지 않습니다.

### 코드 없이 이벤트 받기

**Add Component > KeyTween > Event Listener**를 추가합니다. 이 컴포넌트에서 이벤트 이름마다 UnityEvent를 연결할 수 있습니다.

### 사운드 트랙

사운드는 전용 트랙이 따로 있으며, 지정한 시간에 클립을 지정한 볼륨으로 재생합니다.

* `KeyTween.audioSource`를 지정하면 그 오디오 소스로 재생합니다.
* 비워 두면 숨겨진 공용 2D 오디오 소스 하나로 재생합니다.

자세한 내용은 [애니메이션 이벤트](Docs~/API.md#애니메이션-이벤트)와 [이벤트와 사운드](Docs~/Guide.md#10-이벤트와-사운드)를 참고하세요.

## 코드 없이 재생 (Play On)

상태 인스펙터의 **Play On**에서 상태가 재생될 시점을 고르면 코드 없이 재생할 수 있습니다. UnityEvent에서 재생하는 방법도 설명합니다.

### Play On 고르기

항목을 고르면 에디터가 그에 맞는 `KeyTweenTrigger` 컴포넌트를 붙여 줍니다.

![상태 인스펙터의 Play On 목록](Docs~/media/editor/editor-play-on.png)

| Play On | 재생되는 때 |
|---|---|
| `None` | 코드에서만 |
| `OnEnable` / `Start` | 컴포넌트가 켜질 때 / 처음 켜진 뒤 한 번 |
| `PointerEnter` / `PointerExit` / `PointerDown` / `PointerUp` / `Click` | 포인터 이벤트 |
| `Select` / `Deselect` | 선택 이벤트 |
| `ToggleOn` / `ToggleOff` | 같은 오브젝트 `Toggle`의 값 변경 |

Selectable의 `interactable`이 꺼져 있으면 입력으로 동작하는 트리거는 무시됩니다.

### UnityEvent에서 재생하기

Button의 `OnClick` 같은 UnityEvent에서도 상태를 재생할 수 있습니다.

* 재생하려면 `KeyTween.Play (string)`을 고르고 상태 이름을 적습니다.
* 거꾸로 재생하려면 `PlayReversed (string)`을 고릅니다.

자세한 내용은 [Play On](Docs~/Guide.md#4-play-on--재생-시점)을 참고하세요.

## 중첩 Prefab: 자식 호출과 오버라이드

버튼 같은 공용 Prefab은 팝업 같은 다른 Prefab 안에 자주 들어갑니다. KeyTween은 이런 경우를 자식 호출과 오버라이드로 다룹니다.

### 자식 호출 추가하기

바깥 Prefab의 상태가 안에 든 Prefab의 상태를 부를 수 있습니다. 예를 들어 팝업의 `Open` 상태가 0.25초에 버튼의 `Hover` 상태를 부르게 할 수 있습니다.

1. 바깥 Prefab 상태의 타임라인을 우클릭합니다.
2. **Add Child Call ▸ 자식 ▸ 상태**를 고릅니다.

자식 호출은 부모 타임라인에 막대로 표시되며, 부모 상태의 길이에 포함됩니다.

### 자식의 트랙 덮어쓰기

자식 호출 아래에는 불린 상태의 트랙이 흐리게 표시됩니다. 그중 하나를 클릭하면 **그 트랙만** 팝업 안에서 덮어씁니다.

* 버튼 Prefab 자체는 바뀌지 않습니다.
* 손대지 않은 트랙은 계속 원본을 따릅니다.
* 화면 > 팝업 > 버튼처럼 여러 단계로 중첩된 경우에는 가장 바깥의 오버라이드가 적용됩니다. 이는 Unity 중첩 Prefab의 규칙과 같습니다.

아래 그림에서는 팝업이 안에 든 `OkButton`의 Color 트랙을 덮어썼습니다. Game 뷰에는 버튼 원래의 빨간색이 보입니다. Scene 뷰와 KeyTween 창에는 팝업이 버튼의 `Hover` 상태에 넣은 노란색 Color 키가 보입니다.

![팝업이 안에 든 OkButton의 트랙을 덮어쓴 모습](Docs~/media/editor/editor-override.png)

전체 과정은 [팝업 안의 버튼만 바꾸기](https://youtu.be/rXO_kNexiGc) 영상에서 볼 수 있습니다. 자세한 내용은 [Guide §12](Docs~/Guide.md#12-자식-호출과-오버라이드-중첩-prefab)를 참고하세요.

## 샘플링, 값 읽고 쓰기

상태를 재생하지 않고 모습을 정하거나, 속성 하나를 직접 읽고 쓰는 방법을 설명합니다.

### 시간이 아닌 값으로 상태 움직이기

`Sample`은 상태를 재생하지 않고 지정한 시간의 모습만 보여 줍니다. 그래서 시간이 아닌 다른 값으로 상태를 움직일 수 있습니다.

```csharp
// 시간이 아닌 값으로 상태의 모습을 정함. 예: 헤더가 접힌 정도(0..1)
anim.Sample("Collapse", progress * anim.Duration("Collapse"));
```

### 속성 하나 읽고 쓰기

`KeyTween.Read`와 `KeyTween.Write`는 트랙과 같은 방식으로 속성 하나를 읽고 씁니다.

```csharp
// 트랙과 같은 방식으로 속성 하나를 읽고 씀(값이 하나인 속성은 x를 씀)
Vector4 color = KeyTween.Read(image, KeyTweenProperty.GraphicColor);
KeyTween.Write(label, KeyTweenProperty.TextNumber, new Vector4(1234, 0, 0, 0));   // "1,234"
```

자세한 내용은 [값 읽고 쓰기](Docs~/API.md#값-읽고-쓰기)를 참고하세요.

## 스크롤 리스트

`KeyTweenScrollList`는 `ScrollRect`에 붙여 쓰는 **가상화** 리스트입니다. 화면에 보이는 아이템만 만들고, 스크롤할 때 그 아이템을 재사용합니다.

### 배치와 기능

리스트의 형태는 모두 이 컴포넌트 하나의 설정으로 정합니다. 배치는 다음 중에서 고릅니다.

* 세로
* 가로
* 그리드
* 메이슨리

여기에 다음 기능을 더할 수 있습니다.

* 루프
* 스냅 (가장 가까운 아이템으로, 또는 한 번에 한 칸씩)
* 자동 스크롤
* 끝 유지
* 섹션 헤더 고정

이 기능들로 캐러셀, 페이지, 배너, 휠 피커, 채팅, 아코디언, 탭, 접히는 헤더, 당겨서 새로고침을 만들 수 있습니다.

### 아이템 연출

연출은 모두 아이템 `KeyTween`의 상태로 만듭니다.

* Stagger를 준 진입
* 등장
* 뷰포트 안에서 아이템의 위치에 따라 움직이는 스크럽 상태

### 코드에서 연결하기

```csharp
// UI Toolkit의 ListView처럼 연결. ItemView는 아이템 Prefab에 붙은 아무 컴포넌트
list.SetData(items, (ItemView view, Item item, int index) => view.Set(item));
list.SetCount(100, (ItemView view, int index) => view.Set(items[index]),
                   (ItemView view, int index) => view.Clear());          // unbind는 선택

list.prefabOf = i => items[i].isHeader ? 1 : 0;   // 여러 종류의 아이템 Prefab(SetCount 전에 설정)
list.SetCount(items.Count);                        // 개수만 바꿈: 스크롤 위치를 유지하고 새 아이템만 등장
list.ScrollTo(20, KeyTweenScrollList.Alignment.Center, 0.3f);
list.CurrentIndexChanged += i => indicator.text = $"{i + 1} / {list.Count}";

list.RemoveItem(5);                                // 데이터에서 지운 뒤 호출: 아래 아이템이 올라옴
await list.ResizeItem(3, 0f);                      // 아코디언: 아이템 하나의 크기를 애니메이션한 뒤 지움
```

### 도우미 컴포넌트

* `KeyTweenPageIndicator`
* `KeyTweenScrollScrub`
* `KeyTweenPullToRefresh`

**UI Components** 샘플에서 18가지 예시를 볼 수 있습니다. 자세한 내용은 [ScrollList](Docs~/ScrollList.md)를 참고하세요.

## 화면 전환

KeyTween 패키지에는 Page, Modal, Sheet 컨테이너가 들어 있으며, UnityScreenNavigator와 같은 역할을 합니다. 모든 동작은 `Awaitable`을 돌려줍니다.

### 화면 열고 닫기

```csharp
await pages.Push(loadingPage, stack: false);   // 다음 페이지가 들어오면 기록에서 빠지고 파괴됨
await pages.Push(homePage);
await pages.Pop();

await modals.Push(confirmPopup);               // 배경이 함께 생김. 상태 "Enter" / "Exit"
await modals.Pop();

int shop = sheets.Register(shopTabPrefab);     // 탭
await sheets.Show(shop);
```

### 전환 상태

전환 연출은 화면 Prefab에 있는 `KeyTween`의 상태이고, 이름으로 찾습니다. 해당 상태가 없으면 기본 전환을 씁니다.

| 컨테이너 | 화면의 상태 | 기본 전환 (0.3초) |
|---|---|---|
| `KeyTweenPageContainer` | `PushEnter`, `PushExit`, `PopEnter`, `PopExit` | 한 화면 폭 슬라이드 |
| `KeyTweenModalContainer` | `Enter`, `Exit` (배경도 같은 이름) | 크기 0.3 → 1 + 페이드 |
| `KeyTweenSheetContainer` | `Enter`, `Exit`, `EnterBackward`, `ExitBackward` | 페이드 |

어느 컨테이너든 전환 중일 때는 입력이 막힙니다.

### 전환 중에 코드 실행하기

화면의 컴포넌트에서 `IKeyTweenScreenLifecycle`을 구현합니다. 인터페이스의 메서드는 모두 선택 사항입니다.

```csharp
public sealed class HomePage : MonoBehaviour, IKeyTweenScreenLifecycle
{
    public async Awaitable WillEnter(KeyTweenTransition t) => await LoadAsync();   // 애니메이션 전에 끝날 때까지 기다림
    public void DidEnter(KeyTweenTransition t) { }
    public Awaitable WillExit(KeyTweenTransition t) => KeyTweenScreen.Completed();
    public void DidExit(KeyTweenTransition t) { }
    Awaitable LoadAsync() => KeyTweenScreen.Completed();
}
```

### 공유 요소 전환

공유 요소 전환에서는 목록의 초상화가 상세 페이지의 초상화 자리로 날아갑니다.

* 움직임은 에디터에서 Position 트랙과 Size 트랙으로 만듭니다.
* 실행 중에는 코드가 상대 요소의 화면상 사각형만 채워 넣습니다.

```csharp
public Awaitable WillEnter(KeyTweenTransition t)
{
    KeyTweenSharedElement.SetPartnerKeys(GetComponent<KeyTween>(), "PushEnter", portrait, card, atStart: true);
    return KeyTweenScreen.Completed();
}
```

자세한 내용은 [Screens](Docs~/Screens.md)를 참고하세요.

## 연동 (UniTask, R3, UIEffect)

KeyTween은 서드파티 라이브러리 없이 동작합니다. 아래 라이브러리가 설치되어 있으면 asmdef의 version defines를 통해 연동 코드가 자동으로 컴파일되므로, 따로 설정할 것은 없습니다.

### 지원하는 라이브러리

| 라이브러리 | 패키지 | 추가되는 것 |
|---|---|---|
| UniTask | `com.cysharp.unitask` 2.0 이상 | `PlayUniTaskAsync`, `PlayReversedUniTaskAsync`, `WaitEventUniTaskAsync` |
| R3 | `com.cysharp.r3` 1.0 이상 | `PlayAsObservable`, `EventsAsObservable`, `EventAsObservable` |
| UIEffect | `com.coffee.ui-effect` 5.0 이상 | 트랙 14종 |

### UniTask로 기다리기

UniTask 메서드의 완료와 취소 규칙은 `PlayAsync`와 같습니다.

```csharp
// UniTask (com.cysharp.unitask 2.0 이상)
await UniTask.WhenAll(popup.PlayUniTaskAsync("Open"), dim.PlayUniTaskAsync("Show"));
bool canceled = await popup.PlayUniTaskAsync("Close").SuppressCancellationThrow();
KeyTweenEventInfo info = await anim.WaitEventUniTaskAsync("Shown");
```

### R3로 구독하기

`PlayAsObservable`은 구독하면 재생하고, 끝나면 한 번 내보냅니다. 구독을 끊으면 멈춥니다.

```csharp
// R3 (com.cysharp.r3 1.0 이상)
open.OnClickAsObservable().ThrottleFirst(TimeSpan.FromSeconds(0.5))
    .SelectMany(_ => popup.PlayAsObservable("Open"))
    .Subscribe(_ => Debug.Log("opened")).AddTo(this);
popup.EventAsObservable("Shown").Subscribe(e => Debug.Log(e.Time)).AddTo(this);
```

### UIEffect 트랙

UIEffect가 있으면 다음 종류의 트랙 14종이 추가됩니다.

* 톤
* 색
* 샘플링
* 트랜지션 (디졸브 등)
* 엣지 샤인
* 그라데이션
* 디테일

자세한 내용은 [Integrations](Docs~/Integrations.md)를 참고하세요.

## 샘플

KeyTween에는 샘플 두 개가 들어 있습니다. Package Manager에서 KeyTween을 선택하고 **Samples** 탭에서 Import합니다.

### 들어 있는 샘플

| 샘플 | 내용 | 필요 |
|---|---|---|
| **RPG Sample** | 모바일 RPG UI 한 벌 | Input System |
| **UI Components** | 버튼, 글자 효과, 스크롤 리스트 예시 | Input System |

### RPG Sample

* 모바일 RPG UI 한 벌입니다.
* 화면: 타이틀, 로비, 영웅과 강화, 가방, 소환, 모험, 전투, 퀘스트, 상점, 우편, 길드 채팅, 랭킹, 스토리, 설정
* 모든 움직임은 Prefab의 상태입니다. 스크립트는 화면을 열고 데이터를 채우기만 합니다.
* 씬: `Scenes/RPG.unity`

아래는 RPG 샘플의 실제 화면입니다.

| 영웅 목록 → 상세(공유 요소 전환) → 강화 | 모험 → 편성 → 전투 → 승리 |
|---|---|
| ![영웅](Docs~/media/rpg/rpg-heroes.gif) | ![전투](Docs~/media/rpg/rpg-battle.gif) |
| **랭킹: 긴 목록 스크롤** | **우편 받기와 출석 보상** |
| ![랭킹](Docs~/media/rpg/rpg-ranking.gif) | ![우편](Docs~/media/rpg/rpg-mail.gif) |

### UI Components

예시가 씬마다 하나씩 들어 있습니다.

* 버튼
* 호버
* 토글
* 팝업
* 글자 효과
* 스크롤 리스트 예시 18가지

### TextMeshPro 리소스

프로젝트에 TMP Essential Resources(TextMeshPro의 설정과 셰이더)가 없으면 샘플을 Import할 때 자동으로 함께 가져옵니다.

UniTask와 R3 예제는 [Integrations](Docs~/Integrations.md)에 있습니다.

## 성능

KeyTween과 다른 애니메이션 도구의 비용을 비교한 결과입니다.

### 측정 결과

* IL2CPP 릴리스로 빌드한 Windows 플레이어
* 오브젝트 2,000개
* 괄호 안의 수치는 같은 값을 스크립트에서 직접 넣을 때보다 더 걸린 시간입니다. 그 방식 자체의 비용을 뜻합니다.

| | KeyTween | DOTween Pro | LitMotion | Animator | AnimationUI |
|---|---:|---:|---:|---:|---:|
| 맥박 반복 (ms) | 7.69 (+0.38) | 7.71 (+0.40) | 7.66 (+0.34) | 10.16 (+2.85) | 9.31 (+2.00) |
| 목록 등장 (ms) | 1.70 (+0.04) | 1.59 (−0.06) | 1.53 (−0.13) | 5.23 (+3.57) | 2.18 (+0.52) |
| 글자 물결 100개 (ms) | **0.61** | 2.11 | 2.05 | 지원 안 함 | 지원 안 함 |

### 요약

* 재생 중 GC 할당이 없습니다.
* 비용은 코드 트윈 라이브러리인 DOTween Pro, LitMotion과 비슷하고 Animator와 AnimationUI보다 가볍습니다.
* 글자 효과는 비교 대상 중 가장 빠릅니다.
* 약점은 재생을 시작하는 비용입니다. 2,000개를 한 번에 다시 재생하면 2.31ms가 걸리며, LitMotion은 1.34ms입니다.

측정 방법과 전체 결과 표는 [Performance](Docs~/Performance.md)와 [Benchmarks](Docs~/Benchmarks.md)에 있습니다.

## 문서

전체 문서는 [문서 사이트](https://unialgames.github.io/KeyTween/articles/ko/overview.html)에 한국어와 영어로 있습니다.

| 문서 | 내용 |
|---|---|
| [설치](https://unialgames.github.io/KeyTween/articles/ko/installation.html) | 요구 사항, Package Manager 설치, 버전 고정, 샘플 가져오기 |
| [빠른 시작](https://unialgames.github.io/KeyTween/articles/ko/quick-start.html) | 에디터에서 첫 호버 연출 만들기, 코드에서 재생하기 |
| [에디터](https://unialgames.github.io/KeyTween/articles/ko/concepts.html) | KeyTween 창, 상태·트랙·키, Play On, 중첩 Prefab, 미리보기, 저장, 녹화 |
| [코드에서 쓰기](https://unialgames.github.io/KeyTween/articles/ko/playing-and-awaiting.html) | 상태 재생과 기다리기, 실행 중 끝 값, 애니메이션 이벤트, 값 읽고 쓰기 |
| [스크롤 리스트](https://unialgames.github.io/KeyTween/articles/ko/scroll-list.html) | 가상화 스크롤 리스트, 아이템 연출, 도우미 컴포넌트 |
| [화면 전환](https://unialgames.github.io/KeyTween/articles/ko/screens.html) | 페이지, 모달, 시트 컨테이너, 생명주기, 공유 요소 |
| [연동](https://unialgames.github.io/KeyTween/articles/ko/unitask.html) | UniTask, R3, UIEffect |
| [성능](https://unialgames.github.io/KeyTween/articles/ko/performance.html) · [벤치마크](https://unialgames.github.io/KeyTween/articles/ko/benchmarks.html) | 다른 도구와의 측정 비교, 사용할 때 알아 둘 점, 전체 결과 표 |
| [비교](https://unialgames.github.io/KeyTween/articles/ko/comparison.html) | 다른 도구와 제작 편의 비교 |
| [API 레퍼런스](https://unialgames.github.io/KeyTween/api/KeyTweening.html) | 코드의 XML 주석으로 만든 모든 공개 형식과 멤버 |

## 라이선스

KeyTween과 샘플 리소스의 라이선스입니다.

* KeyTween: [MIT](LICENSE.md)
* RPG 샘플의 아이콘: [game-icons.net](https://game-icons.net)의 것으로 CC BY 3.0 라이선스를 따릅니다. 작가 목록은 `Samples/RPG/Art/CREDITS.md`에 있습니다.
* 샘플의 글꼴: Passion One (SIL OFL 1.1)
