# 코드에서 쓰기 — API

네임스페이스는 `KeyTweening`, 컴포넌트는 `KeyTween`입니다. 연출 자체는 에디터에서 만들고([Guide](Guide.md)), 코드는 **상태 이름으로 재생하고 기다리는 일**만 합니다.

## 재생

```csharp
public sealed class KeyTween : MonoBehaviour
{
    public void Play(string stateName);              // 인자 하나: Button OnClick 같은 UnityEvent 목록에도 나옴
    public void Play(string stateName, PointerEventData pointer);   // Start = Pointer 트랙용(누른 자리)
    public Awaitable PlayAsync(string stateName, CancellationToken cancellationToken = default);
    public void PlayReversed(string stateName);      // UnityEvent 목록에도 나옴
    public Awaitable PlayReversedAsync(string stateName, CancellationToken cancellationToken = default);

    public void Complete(string stateName);         // 재생 중인 상태를 끝으로 건너뜀
    public void Stop(string stateName);             // 그 자리에서 멈춤
    public void Sample(string stateName, float time);   // 재생하지 않고 그 시간의 모습만

    public bool IsPlaying(string stateName);
    public bool HasState(string stateName);
    public float Duration(string stateName);        // 오버라이드 반영 길이, 없는 상태면 0
}
```

| 메서드 | 동작 |
|---|---|
| `Play` | 재생을 시작하고 첫 모습(0초)을 **바로** 씁니다. 없는 상태 이름은 경고 로그만 남깁니다. `pointer`는 Start = Pointer 트랙(누른 자리)에 쓰입니다. |
| `PlayAsync` | 끝에 닿거나 `Complete`되면 완료됩니다. 끝나지 못하면 `OperationCanceledException`으로 끝납니다(아래 표). 없는 상태면 바로 완료됩니다. Unity `Awaitable`이라 **한 번만** await합니다. |
| `PlayReversed` | 끝에서 시작으로 재생합니다. 이벤트·사운드·자식 호출도 거꾸로 한 번씩 발생하고, 자식 상태도 역재생됩니다. Start = Current Value 트랙은 마지막 정방향 재생이 잡은 값을 다시 써서, 뒤로 가기가 열기를 시작한 자리로 정확히 돌아갑니다. |
| `Complete` | 재생 중일 때만. 끝 모습으로 이동하고, 남은 이벤트·자식 호출을 순서대로 발생시킨 뒤(사운드는 울리지 않음) `PlayAsync`를 정상 완료합니다. 역재생 중이면 시작 모습으로 |
| `Stop` | 그 자리에서 멈춥니다. 기다리던 `PlayAsync`는 취소됩니다. 반복 상태를 끌 때 씁니다. |
| `Sample` | 시간을 0~길이로 잘라 그 모습을 씁니다. 타이머·이벤트·사운드는 없습니다. 스크롤 위치처럼 시간 아닌 값으로 모습을 정할 때 씁니다. 재생 중이면 멈춥니다. |

- 시간은 실시간입니다(`Time.timeScale`을 따르지 않음).
- 반복 상태(Loop)의 `PlayAsync`는 `Complete`나 `Stop` 전에는 끝나지 않습니다.
- 재생 중인 다른 상태가 같은 속성을 움직이고 있으면, 새 상태가 그 속성만 그 자리에서 가져갑니다([Guide §11](Guide.md#11-여러-상태가-겹칠-때)). 앞 상태를 끝 모습까지 마치게 하려면 먼저 `Complete("앞 상태")`를 부릅니다.

### 끝나는 경우

| 경우 | `PlayAsync` 결과 | 남은 이벤트 | 남은 사운드 |
|---|---|---|---|
| 끝에 도달 | 완료 | 모두 발생 | 모두 재생 |
| `Complete` 호출 | 완료 | 순서대로 발생 | 재생 안 함 |
| 다른 상태가 속성을 모두 가져감 | 취소 | 발생 안 함 | 재생 안 함 |
| 같은 상태를 다시 재생 | 이전 재생 취소 | 발생 안 함 | 재생 안 함 |
| 오브젝트 비활성화 | 취소 | 발생 안 함 | 재생 안 함 |
| `Stop` 호출, 토큰 취소 | 취소 | 발생 안 함 | 재생 안 함 |

끝나지 않은 재생을 **취소**로 끝내는 이유는 "끝나야만 다음 동작"이 기본이 되게 하기 위해서입니다.

### 예: 팝업 닫기

닫히는 도중 다시 열리면 Destroy하지 않습니다.

```csharp
try { await popup.PlayAsync("Close", cancellationToken: popup.destroyCancellationToken); }
catch (OperationCanceledException) { return; }   // Open이 Close의 속성을 가져감 → 팝업 유지
Destroy(popup.gameObject);
```

### 예: 대사창

같은 TMP의 text를 바꿔 가며 재생하고, 건너뛰기는 `Complete`입니다. 재생할 때 글자 수를 새로 읽으므로 새 대사 기준으로 동작합니다.

```csharp
dialogue.text = nextLine;
var typing = anim.PlayAsync("Typewriter");
// 건너뛰기 버튼: anim.Complete("Typewriter");
await typing;
```

## 런타임에 끝 값 바꾸기

재화·전투력처럼 끝 값이 실행 중에 정해지는 연출은, 에디터에서 연출(시간·이징)을 만들고 코드는 끝 값만 넘깁니다.

```csharp
public void SetEndValue(string stateName, Component target, KeyTweenProperty property, Vector4 value);
public void SetEndValue(string stateName, Component target, KeyTweenProperty property, float value);   // x만
```

- 그 트랙의 **마지막 키 값**을 바꿉니다. 다음 재생부터 계속 적용되고, 저장된 데이터는 바뀌지 않습니다. 재생 전에 부릅니다.
- 키 값과 똑같이 해석됩니다(Relative To Rest, Size Relative 반영). 맞는 트랙이 없으면 경고 로그를 남깁니다.

```csharp
// 금화 라벨의 "Count" 상태: Number (Count) 트랙, Start = Current Value, 0.6초 OutQuad (에디터에서 만듦)
count.SetEndValue("Count", goldLabel, KeyTweenProperty.TextNumber, gold);
count.Play("Count");
```

## 애니메이션 이벤트

에디터의 이벤트 트랙에 놓은 이벤트를 받습니다.

```csharp
[KeyTweenEvent("Popup Sound")]                    // 에디터의 이벤트 형식 목록에 나옴
public struct PopupSoundEvent { public string clip; public float volume; }

anim.EventRaised += (in KeyTweenEventInfo e) => Debug.Log($"{e.State}:{e.Name} @ {e.Time}");
using var sub = anim.AddListener<PopupSoundEvent>((in KeyTweenEventInfo e, in PopupSoundEvent data) => Play(data.clip));
var info = await anim.WaitEventAsync("Shown");   // 다음에 이 이름의 이벤트가 나올 때까지
```

| API | 내용 |
|---|---|
| `event KeyTweenEventHandler EventRaised` | 재생이 이벤트를 지날 때. `Complete`는 건너뛴 이벤트를 순서대로 발생시키고, 취소는 발생시키지 않습니다. |
| `IDisposable AddListener<T>(KeyTweenEventHandler<T> handler)` | 데이터 형식이 `T`인 이벤트만. `Dispose`로 해제 |
| `Awaitable<KeyTweenEventInfo> WaitEventAsync(string eventName, CancellationToken)` | 어느 상태든 그 이름의 이벤트가 다음에 나올 때 완료 |
| `KeyTweenEventInfo` (readonly struct, `in`으로 전달) | `Animation`, `State`, `Name`, `Time`, `TryGetData<T>(out T)` |

- `[KeyTweenEvent]` struct의 public 필드는 JSON으로 저장됩니다(오브젝트 참조는 안 됨). 형식 목록에는 플레이어 빌드에 들어가는 struct만 나옵니다.
- 코드 없이 받기: `Add Component > KeyTween > Event Listener`에서 이벤트 이름 → UnityEvent를 연결합니다(데이터는 넘기지 않음).

## 실행 중에 상태 추가

```csharp
public void AddState(KeyTweenState state);   // 같은 이름이 있으면 바꿈
```

화면 전환 컨테이너가 기본 전환을 넣을 때 씁니다([Screens](Screens.md)). 일반적인 연출은 에디터에서 만듭니다.

## 값 읽고 쓰기

```csharp
public static Vector4 KeyTween.Read(Component target, KeyTweenProperty property);
public static void KeyTween.Write(Component target, KeyTweenProperty property, Vector4 value);
```

트랙과 같은 방식으로 속성 하나를 읽고 씁니다. 한 값짜리 속성은 `x`를 씁니다. `Rotation`은 −180~180으로 읽고, `TextNumber`는 첫 정수를 읽고 `1,234` 형식으로 씁니다. 글자별 속성은 쓸 수 없습니다(재생 중 메시에 직접 적용됨).

## 사운드 소스

`KeyTween.audioSource`(인스펙터의 Audio Source)를 지정하면 그 소스로, 비우면 숨은 공용 2D AudioSource 하나로 재생합니다(`PlayOneShot`). 믹서 그룹이나 3D 사운드가 필요할 때만 지정합니다. 사운드 트랙마다 따로 지정할 수도 있습니다.

## 선택 연동

| 라이브러리 | 확장 메서드 |
|---|---|
| UniTask | `PlayUniTaskAsync`, `PlayReversedUniTaskAsync`, `WaitEventUniTaskAsync` |
| R3 | `PlayAsObservable`, `EventsAsObservable`, `EventAsObservable` |

설치하면 자동으로 켜집니다. 자세한 내용: [Integrations](Integrations.md)

## 그 밖의 컴포넌트

| 컴포넌트 | 문서 |
|---|---|
| `KeyTweenTrigger` (Play On) | 에디터에서 Play On을 고르면 자동으로 붙음. [Guide §4](Guide.md#4-play-on--재생-시점) |
| `KeyTweenScrollList`, `KeyTweenPageIndicator`, `KeyTweenScrollScrub`, `KeyTweenPullToRefresh` | [ScrollList](ScrollList.md) |
| `KeyTweenScreen`, `KeyTweenPageContainer`, `KeyTweenModalContainer`, `KeyTweenSheetContainer`, `KeyTweenSharedElement` | [Screens](Screens.md) |
| `KeyTweenEventListener` | 위 "애니메이션 이벤트" |
