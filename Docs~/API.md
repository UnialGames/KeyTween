# 코드에서 쓰기 — API

KeyTween의 네임스페이스는 `KeyTweening`이고, 게임 오브젝트에 붙이는 컴포넌트는 `KeyTween`입니다.

연출은 에디터에서 상태(State) 단위로 만듭니다([Guide](Guide.md)). 상태는 이름이 붙은 연출 하나이고, 트랙과 키로 이루어집니다. 코드가 하는 일은 **상태를 이름으로 재생하고, 끝날 때까지 기다리는 것**입니다.

## 재생

`KeyTween` 컴포넌트는 상태를 이름으로 재생하고 제어하는 메서드를 제공합니다.

```csharp
public sealed class KeyTween : MonoBehaviour
{
    public void Play(string stateName);              // 인자가 하나이므로 Button OnClick 같은 UnityEvent 목록에도 나옵니다
    public void Play(string stateName, PointerEventData pointer);   // Start = Pointer 트랙(누른 자리)에 씁니다
    public void Play(string stateName, float time);  // 그 시간(초)부터 재생합니다
    public Awaitable PlayAsync(string stateName, CancellationToken cancellationToken = default);
    public Awaitable PlayAsync(string stateName, float time, CancellationToken cancellationToken = default);
    public void PlayReversed(string stateName);      // UnityEvent 목록에도 나옵니다
    public void PlayReversed(string stateName, float time);   // 그 시간에서 0초로
    public Awaitable PlayReversedAsync(string stateName, CancellationToken cancellationToken = default);
    public Awaitable PlayReversedAsync(string stateName, float time, CancellationToken cancellationToken = default);

    public void Complete(string stateName);         // 재생 중인 상태를 끝으로 건너뜁니다
    public void Stop(string stateName);             // 그 자리에서 멈춥니다
    public void Stop(string stateName, float time); // 멈추고 그 시간의 모습으로 둡니다 (재생하지 않음)

    public KeyTweenPlay With(string stateName);             // 재생 하나를 코드에서 설정 ("코드에서 재생 설정하기")
    public KeyTweenStateBuilder Build(string stateName);    // 코드로 상태 만들기 ("코드로 상태 만들기")

    public bool IsPlaying(string stateName);
    public bool HasState(string stateName);
    public float Duration(string stateName);        // 오버라이드를 반영한 길이이며, 없는 상태면 0입니다
}
```

| 메서드 | 동작 |
|---|---|
| `Play` | 재생을 시작합니다. 시간을 넘기면 그 시간부터 재생합니다. |
| `PlayAsync` | 재생을 시작하고, 끝날 때까지 기다립니다. |
| `PlayReversed` | 끝(또는 넘긴 시간)에서 시작 쪽으로 재생합니다. |
| `Complete` | 재생 중인 상태를 끝 모습으로 건너뜁니다. |
| `Stop` | 그 자리에서 멈춥니다. 시간을 넘기면 멈춘 뒤 그 시간의 모습으로 둡니다. |

시간은 모두 **초**이고, 상태 타임라인의 시간입니다(`Duration`과 같은 단위). 0보다 작거나 길이보다 크면 0이나 끝으로 자릅니다.

### 상태 재생하기

`Play`는 재생을 시작하고, 첫 모습(0초)을 **바로** 씁니다. 없는 상태 이름을 넘기면 경고 로그만 남깁니다.

```csharp
anim.Play("Open");
```

Start = Pointer 트랙이 있으면 `pointer`를 함께 넘깁니다. 트랙은 이 값으로 누른 자리를 압니다.

```csharp
public void OnPointerDown(PointerEventData eventData) => anim.Play("Press", eventData);
```

### 끝날 때까지 기다리기

`PlayAsync`는 재생이 끝에 닿거나 `Complete`가 호출되면 완료됩니다.

```csharp
await anim.PlayAsync("Open", destroyCancellationToken);
```

* 끝나지 못하면 `OperationCanceledException`으로 끝납니다. 자세한 내용은 [끝나는 경우](#끝나는-경우)를 참고하세요.
* 없는 상태면 바로 완료됩니다.
* Unity `Awaitable`이므로 **한 번만** await할 수 있습니다.
* 반복(Loop) 상태의 `PlayAsync`는 `Complete`나 `Stop`을 부르기 전에는 끝나지 않습니다.

### 중간부터 재생하기

`Play`, `PlayAsync`에 시간을 넘기면 그 시간부터 재생합니다. 그 시간의 모습을 바로 쓰고, 남은 시간만 재생합니다.

```csharp
anim.Play("Intro", 1.5f);                     // 1.5초부터
await anim.PlayAsync("Intro", 1.5f, destroyCancellationToken);
```

* 그 시간보다 앞에 있는 이벤트, 사운드, 자식 호출은 발생하지 않습니다. 시작 시간과 같은 시간의 것은 발생합니다(0초부터 재생할 때 0초의 것이 발생하는 것과 같음).
* `PlayReversed`, `PlayReversedAsync`에 시간을 넘기면 그 시간에서 0초 쪽으로 재생합니다.

### 역재생하기

`PlayReversed`는 끝에서 시작 쪽으로 재생합니다. 끝날 때까지 기다리려면 `PlayReversedAsync`를 씁니다.

```csharp
await anim.PlayReversedAsync("Open");   // 열기를 시작한 자리로 돌아갑니다
```

* 이벤트, 사운드, 자식 호출도 거꾸로 한 번씩 발생합니다.
* 자식 상태도 역재생됩니다.
* Start = Current Value 트랙은 마지막 정방향 재생이 잡아 둔 값을 다시 씁니다. 그래서 되돌리기 연출이 열기를 시작한 자리로 정확히 돌아갑니다.
* 끝 값도 마지막 정방향 재생의 값을 되짚습니다. 정방향을 `With(...).WithEndValue(...)`로 재생했다면 그냥 `PlayReversed`는 그 끝 값에서 돌아옵니다. 역재생에 다른 끝 값을 주려면 `With(...).WithEndValue(...).PlayReversed()`를 씁니다.

### 끝으로 건너뛰기

`Complete`는 재생 중인 상태를 끝 모습으로 이동시키고, `PlayAsync`를 정상 완료합니다. 재생 중일 때만 동작합니다.

```csharp
anim.Complete("Open");
```

* 남은 이벤트와 자식 호출은 순서대로 발생합니다.
* 남은 사운드는 재생하지 않습니다.
* 역재생 중이면 시작 모습으로 이동합니다.

### 멈추기

`Stop`은 상태를 그 자리에서 멈춥니다. 기다리던 `PlayAsync`는 취소됩니다. 반복 상태를 끌 때 씁니다.

```csharp
anim.Stop("Idle");
```

### 특정 시간에서 멈추기

`Stop`에 시간을 넘기면, 재생 중이면 멈추고(기다리던 `PlayAsync`는 취소) 그 시간의 모습을 씁니다. 재생 중이 아니어도 그 모습을 씁니다.

```csharp
anim.Stop("Open", anim.Duration("Open"));                     // 애니메이션 없이 끝 모습으로
anim.Stop("Reveal", progress * anim.Duration("Reveal"));      // 스크롤 위치 같은 값으로 매 프레임 (progress: 0 ~ 1)
```

* 타이머, 이벤트, 사운드는 동작하지 않습니다.
* 스크롤 위치, 당긴 거리처럼 시간이 아닌 값으로 모습을 정할 때 매 프레임 불러도 됩니다.
* 예전 이름 `Sample(state, time)`은 같은 동작으로 남아 있지만 `[Obsolete]`이며 다음 버전에서 없어집니다.

### 상태 확인하기

`IsPlaying`, `HasState`, `Duration`으로 상태를 확인합니다. `Duration`은 오버라이드를 반영한 길이이며, 없는 상태면 0입니다.

```csharp
if (anim.HasState("Open") && !anim.IsPlaying("Open")) anim.Play("Open");
```

### 재생 시간

재생 시간이 `Time.timeScale`을 따를지는 KeyTween마다 **Time**(`timeMode`)으로 정합니다.

| Time | 시간 | 쓰는 곳 |
|---|---|---|
| `Scaled` (기본) | 게임 시간. `Time.timeScale`이 0이면 멈춥니다 | 게임 오브젝트, 전투 연출 |
| `Unscaled` | 실제 시간. 게임이 멈춰도 재생됩니다 | 일시 정지 메뉴, 로딩 화면 |

```csharp
pauseMenu.timeMode = KeyTweenTimeMode.Unscaled;   // 다음 재생부터 적용됩니다
```

* Prefab 인스턴스마다 덮어쓸 수 있습니다. 공용 버튼은 `Unscaled`로 두고, 게임 화면에 놓인 버튼만 `Scaled`로 바꾸는 식입니다.
* 자식 호출로 재생되는 상태는 자식 KeyTween의 **Time**을 따릅니다.
* Play 모드가 아닐 때(에디터에서 코드로 재생할 때)는 둘 다 실제 시간입니다.

### 다른 상태와 겹칠 때

재생 중인 다른 상태가 같은 속성을 움직이고 있으면, 새 상태가 그 속성만 현재 값에서 이어받습니다. 앞 상태를 끝 모습까지 마친 뒤 시작하려면 먼저 `Complete`를 부릅니다.

```csharp
anim.Complete("앞 상태");
anim.Play("다음 상태");
```

자세한 내용은 [Guide §11](Guide.md#11-여러-상태가-겹칠-때)을 참고하세요.

### 끝나는 경우

`PlayAsync`가 어떻게 끝나는지, 그리고 아직 지나지 않은 이벤트와 사운드가 어떻게 처리되는지는 다음과 같습니다.

| 경우 | `PlayAsync` 결과 | 콜백 | 남은 이벤트 | 남은 사운드 |
|---|---|---|---|---|
| 끝에 도달 | 완료 | On Complete | 모두 발생 | 모두 재생 |
| `Complete` 호출 | 완료 | On Complete | 순서대로 발생 | 재생 안 함 |
| 다른 상태가 속성을 모두 가져감 | 취소 | On Cancel | 발생 안 함 | 재생 안 함 |
| 같은 상태를 다시 재생 | 이전 재생 취소 | 이전 재생의 On Cancel | 발생 안 함 | 재생 안 함 |
| 오브젝트 비활성화 | 취소 | On Cancel | 발생 안 함 | 재생 안 함 |
| `Stop` 호출, 토큰 취소 | 취소 | On Cancel | 발생 안 함 | 재생 안 함 |

끝까지 가지 못한 재생은 **취소**로 끝납니다. 이렇게 하면 "재생이 실제로 끝났을 때만 다음 동작을 한다"는 흐름이 기본이 됩니다.

### 시작과 끝 콜백

`await`를 쓰지 않아도 재생의 시작과 끝을 받을 수 있습니다. 상태마다 에디터의 **Callbacks**(On Start, On Complete, On Cancel, [Guide](Guide.md#시작과-끝에-함수-부르기-callbacks))에 연결하거나, 코드에서 컴포넌트의 이벤트로 모든 상태를 받습니다.

```csharp
public event Action<string> StateStarted;     // 상태 이름. 첫 모습 다음, 시작 시간의 이벤트보다 먼저
public event Action<string> StateCompleted;   // 끝에 닿았거나 Complete. PlayAsync가 끝나기 전
public event Action<string> StateCanceled;    // 끝까지 못 감 (위 표)
```

```csharp
anim.StateCompleted += state => { if (state == "Close") gameObject.SetActive(false); };
```

* 재생 한 번은 `StateStarted` 다음에 `StateCompleted`와 `StateCanceled` 중 **정확히 하나**로 끝납니다.
* 같은 때에 상태의 UnityEvent(On Start 등)가 먼저, 컴포넌트의 이벤트가 다음에 불립니다.
* 콜백이 예외를 던지면 로그만 남고, 재생과 다른 콜백은 계속됩니다.
* On Start 안에서 그 상태를 `Stop`하면 재생은 시작하지 않고, `PlayAsync`는 취소됩니다.
* 재생하지 않고 모습만 쓰는 `Stop(state, time)`과 에디터 미리보기는 콜백을 부르지 않습니다.

### 예: 팝업 닫기

닫히는 도중에 팝업이 다시 열리면, 닫기 재생은 취소되고 팝업은 Destroy되지 않습니다.

```csharp
try { await popup.PlayAsync("Close", cancellationToken: popup.destroyCancellationToken); }
catch (OperationCanceledException) { return; }   // Open이 Close의 속성을 가져갔으므로 팝업을 유지합니다
Destroy(popup.gameObject);
```

### 예: 대사창

같은 TMP 텍스트의 내용을 바꿔 가며 같은 상태를 다시 재생합니다. 건너뛰기는 `Complete`로 처리합니다. 재생할 때마다 글자 수를 새로 읽으므로 연출은 새 대사에 맞춰 동작합니다.

```csharp
dialogue.text = nextLine;
var typing = anim.PlayAsync("Typewriter");
// 건너뛰기 버튼: anim.Complete("Typewriter");
await typing;
```

RPG 샘플의 스토리 화면은 이 방식으로 대사를 한 글자씩 보여 줍니다.

![RPG 샘플의 스토리 대화: 초상화와 한 글자씩 나타나는 대사](media/rpg/rpg-story.gif)

## 여러 KeyTween을 함께 기다리기

여러 오브젝트의 `KeyTween`을 동시에 재생하고, 모두 끝날 때까지 기다리는 방법입니다.

### 모두 시작한 뒤 차례로 기다리기

`PlayAsync`는 .NET `Task`가 아니라 Unity `Awaitable`을 돌려주므로 `Task.WhenAll`에 바로 넣을 수 없습니다. 대신 재생은 `PlayAsync`를 부르는 순간 시작됩니다. 전부 먼저 시작해 두고 차례로 기다리면 `WhenAll`과 같은 결과를 얻습니다.

```csharp
var plays = anims.Select(a => a.PlayAsync("Hover")).ToList();   // 여기서 모두 동시에 시작합니다
foreach (var play in plays) await play;                         // 모두 끝날 때까지 기다립니다
```

### Task.WhenAll로 기다리기

`Task.WhenAll`을 쓰려면 각 재생을 `Task`로 감쌉니다.

```csharp
static async Task AsTask(Awaitable awaitable) => await awaitable;

await Task.WhenAll(anims.Select(a => AsTask(a.PlayAsync("Hover"))));
```

### UniTask.WhenAll로 기다리기

UniTask를 설치했다면 [연동](Integrations.md)에서 제공하는 `PlayUniTaskAsync`를 쓸 수 있습니다.

```csharp
await UniTask.WhenAll(anims.Select(a => a.PlayUniTaskAsync("Hover")));
```

### 하나가 취소되면

재생 중 하나라도 취소되면([끝나는 경우](#끝나는-경우)) `OperationCanceledException`이 발생합니다. 예외가 발생하는 위치는 방법마다 다릅니다.

* 차례로 기다리기: 취소된 재생을 기다리는 `await`
* `Task.WhenAll`, `UniTask.WhenAll`: `WhenAll`

## 코드에서 재생 설정하기 (With)

움직임은 에디터에서 만들고, 재생할 때마다 달라지는 것만 코드에서 정합니다. 재화나 전투력의 목표 숫자, 상황에 따라 다른 도착 방식, 끝났을 때 할 일 같은 것입니다.

`With(state)`로 재생 하나를 설정하고, `Play` 계열로 재생합니다. LitMotion의 `WithEase`처럼 이어 씁니다.

```csharp
// 금화 라벨의 "Count" 상태: Number (Count) 트랙, Start = Current Value, 0.6초 OutQuad (에디터에서 만듦)
count.With("Count").WithEndValue(goldLabel, KeyTweenProperty.TextNumber, gold).Play();

// 팝업 열기: 이번에는 OutBack으로 도착하고, 다 열리면 입력을 받음
await popup.With("Open")
    .WithEase(panel, KeyTweenProperty.Scale, KeyTweenEase.OutBack)
    .WithOnComplete(EnableInput)
    .PlayAsync(destroyCancellationToken);
```

### 바꿀 수 있는 것

트랙은 상태 이름, 대상, 속성으로 찾고, 바꾸는 것은 그 트랙의 **마지막 키**(끝 값과 그 키로 오는 구간)입니다.

| 메서드 | 바꾸는 것 |
|---|---|
| `WithEndValue(target, property, value)` | 마지막 키의 값. `float`를 받는 것은 내장 속성용이고 x만 바꿉니다 |
| `WithEase(target, property, ease)` | 마지막 키의 도착 방식을 Ease로, 그 Ease |
| `WithCurve(target, property, curve)` | 도착 방식을 Curve로, 그 AnimationCurve |
| `WithPunch(target, property, frequency = 10, damping = 1)`, `WithShake(...)` | 도착 방식을 Punch / Shake로, 진동 값. 세기는 키에 적힌 값 그대로입니다 |
| `WithTime(time)` | 그 시간(초)부터 재생 (`Play(state, time)`과 같음) |
| `WithEventsBefore(raise = true)` | `WithTime`과 함께: 그 시간 이전의 애니메이션 이벤트도 시작할 때 순서대로 발생 (사운드, 자식 호출은 건너뜀) |
| `WithOnStart`, `WithOnComplete`, `WithOnCancel` | 이 재생에만 거는 콜백. 상태의 On Start 등 다음에 불림 |
| `WithOnLoop` | 반복 상태에서 한 바퀴가 끝날 때마다 |

* Material 트랙은 `WithEase(renderer, "_BaseColor", ease)`처럼 셰이더 속성 이름으로, 커스텀 트랙은 `WithEase<MyTrack>(target, ease)`처럼 트랙 클래스로 찾습니다.
* 넘긴 값은 키 값과 똑같이 해석됩니다. Relative To Rest와 Size Relative 설정이 반영됩니다.
* 맞는 트랙이 없으면 경고 로그를 남기고 그 항목만 건너뜁니다. 재생은 합니다.
* `With`에는 `PointerEventData`를 넘기는 재생이 없습니다. Start = Pointer 트랙이 있는 상태는 `Play(state, eventData)`로 재생합니다.

`WithTime`, `WithEventsBefore`, `WithOnLoop`는 이렇게 씁니다.

```csharp
// 저장한 게임을 불러올 때: 0.8초 지점부터 이어서 재생하고, 그 전의 이벤트("DoorOpened" 등)도 순서대로 받아 상태를 맞춤
door.With("Open").WithTime(0.8f).WithEventsBefore().Play();

// 반복 상태가 한 바퀴 돌 때마다 횟수를 셈
int laps = 0;
wheel.With("Spin").WithOnLoop(() => laps++).Play();
```

### 이번 재생에만 적용됩니다

바꾼 값은 재생을 시작할 때 들어가고, 그 재생이 끝나면(완료든 취소든) 원래대로 돌아옵니다. 다음 `Play`는 에디터에서 정한 값으로 재생합니다. 저장된 데이터는 바뀌지 않습니다.

`With`가 돌려준 것은 변수에 두고 몇 번이든 다시 재생할 수 있습니다. 다시 쓰면 재생할 때 GC 할당이 없습니다.

```csharp
KeyTweenPlay _open;

void Awake() => _open = popup.With("Open").WithEase(panel, KeyTweenProperty.Scale, KeyTweenEase.OutBack);
public void Show() => _open.Play();
```

### 다른 곳이 재생하는 상태의 끝 값

Play On이나 화면 전환처럼 내 코드가 아닌 곳이 재생하는 상태는 `With`를 쓸 수 없습니다. 이때는 `SetEndValue`로 끝 값을 미리 넣어 둡니다. 이 값은 그 뒤의 모든 재생에 계속 적용됩니다.

```csharp
public void SetEndValue(string stateName, Component target, KeyTweenProperty property, Vector4 value);
public void SetEndValue(string stateName, Component target, KeyTweenProperty property, float value);   // x만 바꿉니다
```

`SetEndValue`는 내장 속성 트랙만 찾습니다. Material 트랙과 커스텀 트랙의 끝 값은 `With`로 바꿉니다.

```csharp
// 결과 화면: 화면 전환이 "Enter"를 재생할 때 금화가 이 숫자까지 올라감
result.SetEndValue("Enter", goldLabel, KeyTweenProperty.TextNumber, reward);
```

## 애니메이션 이벤트

에디터의 이벤트 트랙에 놓은 이벤트를 코드에서 받는 방법입니다. 재생이 이벤트의 시간을 지나면 이벤트가 발생합니다.

### 이벤트 데이터 형식 만들기

struct에 `[KeyTweenEvent]`를 붙이면 에디터의 이벤트 형식 목록에 나옵니다.

```csharp
[KeyTweenEvent("Popup Sound")]
public struct PopupSoundEvent { public string clip; public float volume; }
```

* public 필드는 JSON으로 저장됩니다.
* 오브젝트 참조는 저장할 수 없습니다.
* 형식 목록에는 플레이어 빌드에 포함되는 struct만 나옵니다.

### 모든 이벤트 받기

`event KeyTweenEventHandler EventRaised`는 재생이 이벤트를 지날 때 발생합니다. `Complete`는 건너뛴 이벤트를 순서대로 발생시키고, 취소는 이벤트를 발생시키지 않습니다.

```csharp
anim.EventRaised += (in KeyTweenEventInfo e) => Debug.Log($"{e.State}:{e.Name} @ {e.Time}");
```

### 데이터 형식별로 받기

`AddListener<T>`는 데이터 형식이 `T`인 이벤트만 받습니다. 돌려받은 `IDisposable`의 `Dispose`로 해제합니다.

```csharp
using var sub = anim.AddListener<PopupSoundEvent>((in KeyTweenEventInfo e, in PopupSoundEvent data) => Play(data.clip));
```

### 이벤트 기다리기

`WaitEventAsync`는 어느 상태에서든 그 이름의 이벤트가 다음에 발생하면 완료됩니다. 결과는 `Awaitable<KeyTweenEventInfo>`입니다.

```csharp
var info = await anim.WaitEventAsync("Shown");
```

### 이벤트 정보 읽기

`KeyTweenEventInfo`는 readonly struct이며, `in`으로 전달됩니다. 다음 멤버를 제공합니다.

| 멤버 | 형식 | 내용 |
|---|---|---|
| `Animation` | `KeyTween` | 이벤트를 낸 KeyTween |
| `State` | `string` | 재생 중이던 상태 이름 |
| `Name` | `string` | 이벤트 이름 |
| `Time` | `float` | 상태 안에서 이벤트의 시간(초) |
| `TryGetData<T>(out T)` | `bool` | 이벤트 데이터가 `T`이면 꺼내고 `true` |

이벤트 하나를 여러 데이터 형식 중 하나로 처리할 때 `TryGetData`를 씁니다.

```csharp
anim.EventRaised += (in KeyTweenEventInfo e) =>
{
    if (e.TryGetData(out PopupSoundEvent sound)) audio.PlayOneShot(Load(sound.clip), sound.volume);
    else if (e.Name == "Shown") EnableInput();
};
```

### 코드 없이 받기

`KeyTweenEventListener` 컴포넌트로 코드 없이 이벤트를 받을 수 있습니다.

1. **Add Component > KeyTween > Event Listener**를 추가합니다.
2. 이벤트 이름에 UnityEvent를 연결합니다.

이때 이벤트 데이터는 넘어가지 않습니다.

## 코드로 상태 만들기 (Build)

상태는 보통 에디터에서 만들지만, 코드만으로 만들 수도 있습니다. 대상이 실행 중에야 정해지는 연출이나, 에디터를 쓰지 않는 작은 연출에 씁니다.

`Build(state)`로 트랙과 키를 쌓고, `Add`로 추가하거나 `Play` 계열로 추가와 재생을 함께 합니다.

```csharp
anim.Build("Pop")
    .Track(button.transform, KeyTweenProperty.Scale)       // 트랙 시작. 다음 Track 전까지의 Key는 이 트랙에
        .Key(0f, Vector3.one)
        .Key(0.15f, Vector3.one * 1.2f, KeyTweenEase.OutBack)  // 이 키로 오는 구간을 OutBack으로
        .Key(0.3f, Vector3.one, KeyTweenEase.InOutSine)
    .Track(label, KeyTweenProperty.GraphicAlpha)
        .Key(0f, 0f).Key(0.2f, 1f)
    .Event(0.15f, "Peak")
    .OnComplete(() => Debug.Log("popped"))
    .Play();
```

| 메서드 | 하는 일 |
|---|---|
| `Track(target, property)` | 내장 속성의 트랙을 시작합니다. Material 트랙은 `Track(renderer, "_BaseColor")`, 커스텀 트랙은 `Track<MyTrack>(target)` |
| `From(start)` | 트랙의 Start. 기본은 `KeyTweenStart.KeyValue`(첫 키)이고, `KeyTweenStart.Current`(재생할 때의 값), `KeyTweenStart.Pointer`를 고를 수 있습니다 |
| `Key(time, value, ease)` | 키 하나. `ease`는 이 키로 오는 구간의 도착 방식(기본 OutSine, 에디터와 같음). 값은 `Vector4`나 `float`이고 `Vector3`, `Vector2`, `Color`도 넘길 수 있습니다 |
| `KeyCurve(time, value, curve)`, `KeyPunch(time, strength, frequency = 10, damping = 1)`, `KeyShake(...)` | 도착 방식이 Curve, Punch, Shake인 키. 값은 `Vector4`(`Vector3`, `Vector2`, `Color`도 됨)이고, Punch와 Shake의 값은 도착할 곳이 아니라 흔들리는 세기입니다 |
| `Event(time, name)`, `Sound(time, clip, volume)` | 애니메이션 이벤트와 사운드 |
| `Loop()` | 반복 상태로 |
| `OnStart`, `OnComplete`, `OnCancel` | 상태의 콜백 (이 상태의 모든 재생) |
| `Add()` | 상태를 추가합니다. 이후 `anim.Play("Pop")`처럼 이름으로 재생합니다 |
| `Play()`, `PlayAsync()`, `PlayReversed()`, `PlayReversedAsync()` | 추가하고 재생합니다 |
| `With()` | 추가하고, 재생 설정(`With`)으로 이어 갑니다 |

* 만든 상태는 실행 중에만 있는 상태입니다. 저장되지 않고, 에디터에서 만든 상태와 똑같이 `Play`, `Stop`, `Complete`, `With`를 쓸 수 있습니다.
* 키는 넣은 순서와 상관없이 시간 순서로 놓입니다.
* 같은 이름의 상태가 있으면 바꿉니다. 그 상태가 재생 중이면 멈추고, 기다리던 `PlayAsync`는 취소됩니다.
* `Track` 없이 `Key`를 부르면 바로 예외가 납니다.

### 상태 객체로 추가하기

`KeyTweenState`를 직접 만들어 `AddState`로 넣을 수도 있습니다. `Build`는 내부에서 이 메서드를 씁니다. 화면 전환 컨테이너가 기본 전환 연출을 넣을 때도 씁니다([Screens](Screens.md)).

```csharp
public void AddState(KeyTweenState state);
```

이미 만들어진 상태 객체를 받는 곳에 씁니다. 예를 들어 기본 전환 연출을 이름을 붙여 다른 화면에도 넣을 수 있습니다.

```csharp
var fade = KeyTweenDefaultTransitions.FadeIn(screen);
fade.name = "FadeIn";
screen.Animation.AddState(fade);
screen.Animation.Play("FadeIn");
```

* 같은 이름의 상태가 있으면 바꿉니다. 그 상태가 재생 중이면 멈추고 On Cancel을 부릅니다.
* 다른 KeyTween의 상태를 바탕으로 만들 때는 `state.Clone()`으로 복사본을 넣습니다. 같은 객체를 두 곳에 넣으면 한쪽에서 고친 것이 다른 쪽에도 바뀝니다. 복사본의 트랙도 원래 대상(Component)을 그대로 가리킵니다.

## 값 읽고 쓰기

트랙이 움직이는 속성 하나를, 재생 없이 코드에서 직접 읽고 쓸 수 있습니다.

### 속성 읽기와 쓰기

`KeyTween.Read`와 `KeyTween.Write`는 정적 메서드입니다.

```csharp
public static Vector4 KeyTween.Read(Component target, KeyTweenProperty property);
public static void KeyTween.Write(Component target, KeyTweenProperty property, Vector4 value);
```

속성마다 다른 Unity API(`Image.color`, `RectTransform.anchoredPosition`, 텍스트 속 숫자 등)를 한 형식으로 다룰 때 씁니다. 예를 들어 저장해 둔 값을 되돌리거나, 커스텀 도구에서 트랙과 같은 값을 읽을 때입니다.

```csharp
Vector4 color = KeyTween.Read(image, KeyTweenProperty.GraphicColor);
KeyTween.Write(label, KeyTweenProperty.TextNumber, new Vector4(1234, 0, 0, 0));   // "1,234"
```

### 값의 해석

값은 트랙과 같은 방식으로 해석됩니다.

* 값이 하나인 속성은 `x`를 씁니다.
* `Rotation`은 −180~180 범위로 읽습니다.
* `TextNumber`는 텍스트의 첫 정수를 읽고, `1,234` 형식으로 씁니다.
* 글자별 속성은 쓸 수 없습니다. 이 속성은 재생 중에 메시에 직접 적용되기 때문입니다.

## 사운드 소스

사운드 트랙의 소리를 어느 AudioSource로 재생할지 정합니다.

### AudioSource 정하기

코드에서 `KeyTween.audioSource`로 정합니다. 인스펙터에서는 이 칸을 고칠 수 없습니다(Raw Data에만 보입니다).

* 지정하면 그 소스로 재생합니다.
* 비워 두면 숨겨진 공용 2D AudioSource 하나로 재생합니다(`PlayOneShot`).

믹서 그룹이나 3D 사운드가 필요할 때만 지정하면 됩니다. 사운드 트랙마다 소스를 따로 지정할 수도 있습니다.

## 선택 연동

다음 라이브러리를 설치하면 KeyTween에 확장 메서드가 추가됩니다.

| 라이브러리 | 확장 메서드 |
|---|---|
| UniTask | `PlayUniTaskAsync`, `PlayReversedUniTaskAsync`, `WaitEventUniTaskAsync` |
| R3 | `PlayAsObservable`, `EventsAsObservable`, `EventAsObservable` |

라이브러리를 설치하면 연동이 자동으로 켜지며, 따로 설정할 것은 없습니다. 시간을 지정한 재생과 `With`, `Build`의 재생은 `AsUniTask()`나 `Observable.FromAsync`로 감쌉니다. 자세한 내용은 [Integrations](Integrations.md)를 참고하세요.

## 그 밖의 컴포넌트

`KeyTween` 외에 패키지가 제공하는 컴포넌트와 해당 문서입니다.

| 컴포넌트 | 문서 |
|---|---|
| `KeyTweenTrigger` (Play On) | 에디터에서 Play On을 고르면 자동으로 붙습니다. [Guide §4](Guide.md#4-play-on) |
| `KeyTweenScrollList`, `KeyTweenPageIndicator`, `KeyTweenScrollScrub`, `KeyTweenPullToRefresh` | [ScrollList](ScrollList.md) |
| `KeyTweenScreen`, `KeyTweenPageContainer`, `KeyTweenModalContainer`, `KeyTweenSheetContainer`, `KeyTweenSharedElement` | [Screens](Screens.md) |
| `KeyTweenEventListener` | [애니메이션 이벤트](#애니메이션-이벤트) |

## 커스텀 트랙 만들기

내장 트랙이 없는 값도 트랙으로 만들 수 있습니다. 프로젝트의 컴포넌트를 움직이는 클래스를 하나 만들면, 그 컴포넌트가 붙은 오브젝트의 트랙 추가 창에 나옵니다.

### 트랙 클래스 만들기

`KeyTweenFloatTrack<T>`를 상속하고, 값을 읽는 `Get`과 쓰는 `Set`을 구현합니다. `T`는 트랙이 움직일 컴포넌트입니다.

```csharp
using KeyTweening;
using UnityEngine.Scripting;
using UnityEngine.UI;

[Preserve]
public sealed class ScrollbarSizeTrack : KeyTweenFloatTrack<Scrollbar>
{
    protected override float Get(Scrollbar target) => target.size;
    protected override void Set(Scrollbar target, float value) => target.size = value;
}
```

* 클래스는 `public`이고 매개변수 없는 생성자가 있어야 합니다.
* Editor 폴더가 아닌 런타임 어셈블리에 둡니다. 빌드에서도 재생되어야 하기 때문입니다.
* 트랙 이름은 클래스 이름에서 `Track`을 뺀 것입니다. 위 예는 `Scrollbar Size`로 보입니다.

### 값의 형식

값의 형식에 맞는 클래스를 상속합니다. 키 인스펙터의 입력 칸도 형식에 맞게 나옵니다.

| 상속할 클래스 | 값 | 키 입력 칸 |
|---|---|---|
| `KeyTweenFloatTrack<T>` | `float` | 숫자 |
| `KeyTweenVector2Track<T>` | `Vector2` | x, y |
| `KeyTweenVector3Track<T>` | `Vector3` | x, y, z |
| `KeyTweenColorTrack<T>` | `Color` | 색 |
| `KeyTweenCustomTrack<T>` | `Vector4` | x, y, z, w |

### 이름, 색, 조건 바꾸기

필요하면 다음 속성을 다시 정의합니다.

| 속성 | 쓰임 |
|---|---|
| `Name` | 트랙과 트랙 추가 창에 보이는 이름 |
| `TrackColor` | 타임라인에서 트랙의 색 |
| `Range` | 숫자 트랙의 슬라이더 범위 (예: `new Vector2(0, 1)`) |
| `Offers(Component)` | 이 트랙을 보여 줄 오브젝트의 조건 (예: 특정 설정일 때만) |

```csharp
public override string Name => "Handle Size";
public override Vector2 Range => new(0f, 1f);
```

### 미리보기와 녹화

미리보기를 멈추면 트랙이 바꾼 값은 미리보기 전에 읽은 값으로 다시 쓰여 돌아옵니다. 값이 저장되는 직렬화 필드를 알려 주면 녹화에서도 그 트랙으로 키가 찍힙니다.

```csharp
public override void SerializedFields(Component target, List<(Object owner, string path)> fields) =>
    fields.Add((target, "m_Size"));
```

### 빌드에서 코드가 제거될 때

트랙은 저장된 클래스 이름으로 만들어집니다. IL2CPP에서 Managed Stripping Level을 Minimal보다 높게 쓰면, 코드에서 직접 쓰지 않는 클래스가 제거될 수 있습니다. 그래서 클래스에 `[Preserve]`를 붙입니다.

### 클래스 이름을 바꿨을 때

트랙은 클래스의 전체 이름(네임스페이스, 어셈블리 포함)으로 저장됩니다.

* 클래스나 네임스페이스의 이름을 바꾸면, 그 트랙은 `(missing)`으로 보이고 아무 값도 쓰지 않습니다.
* 예전 이름으로 되돌리거나, 트랙을 새로 추가합니다.
