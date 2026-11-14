# 선택 연동 — UniTask · R3 · UIEffect

KeyTween 패키지는 서드파티 라이브러리 없이 동작합니다. 아래 라이브러리가 프로젝트에 **설치돼 있으면** 해당 연동 코드가 자동으로 컴파일됩니다. 설치돼 있지 않으면 연동 코드는 컴파일에서 빠지며, 따로 설정할 것은 없습니다.

LitMotion과 VContainer가 UniTask 연동을 제공하는 방식과 같습니다.

| 라이브러리 | 켜지는 조건 (asmdef versionDefines) | 추가되는 것 |
|---|---|---|
| UniTask | `com.cysharp.unitask` 2.0.0 이상 → `KEYTWEEN_UNITASK` | 확장 메서드 (재생·이벤트를 UniTask로) |
| R3 | `com.cysharp.r3` 1.0.0 이상 → `KEYTWEEN_R3` | 확장 메서드 (재생·이벤트·상태 알림을 Observable로) |
| UIEffect | `com.coffee.ui-effect` 5.0.0 이상 → `KEYTWEEN_UIEFFECT` | 트랙 14종 (어셈블리 `KeyTween.UIEffect`) |

## UniTask

UniTask(`com.cysharp.unitask` 2.0.0 이상)를 설치하면, 재생과 이벤트를 UniTask로 기다리는 확장 메서드가 추가됩니다.

| 메서드 | 내용 |
|---|---|
| `PlayUniTaskAsync(stateName, ct)` | `PlayAsync`의 UniTask 버전입니다. |
| `PlayUniTaskAsync(stateName, time, ct)` | 그 시간(초)부터 재생합니다. |
| `PlayReversedUniTaskAsync(stateName, ct)`, `(stateName, time, ct)` | 상태를 역재생합니다. |
| `play.PlayUniTaskAsync(ct)`, `play.PlayReversedUniTaskAsync(ct)` | `With`로 설정한 재생(`KeyTweenPlay`)을 UniTask로 기다립니다. |
| `WaitEventUniTaskAsync(eventName, ct)` | 다음 애니메이션 이벤트가 발생할 때까지 기다립니다. |

### 여러 재생을 함께 기다리기

팝업을 열 때는 `WhenAll`로 팝업과 배경을 함께 재생합니다.

```csharp
await UniTask.WhenAll(popup.PlayUniTaskAsync("Open"), dim.PlayUniTaskAsync("Show"));
```

완료와 취소 규칙은 `PlayAsync`와 같습니다([API](API.md#끝나는-경우)). 내부에서는 `Awaitable`을 `AsUniTask()`로 변환합니다.

### 취소를 값으로 받기

팝업을 닫을 때는 `SuppressCancellationThrow`로 취소를 예외 대신 값으로 받습니다.

```csharp
var canceled = await popup.PlayUniTaskAsync("Close").SuppressCancellationThrow();
```

### 시간 제한 걸기

오래 걸리는 상태는 `Timeout`을 건 뒤, 시간이 지나면 `Complete`를 부릅니다.

```csharp
try { await anim.PlayUniTaskAsync("Intro").Timeout(TimeSpan.FromSeconds(3)); }
catch (TimeoutException) { anim.Complete("Intro"); }
```

### 이벤트 기다리기

애니메이션 이벤트는 `WaitEventUniTaskAsync`로 기다립니다.

```csharp
var info = await anim.WaitEventUniTaskAsync("Shown");
```

### 오브젝트와 함께 정리하기

어느 경우든 `destroyCancellationToken`을 넘겨 두면, 오브젝트가 사라질 때 함께 정리됩니다.

```csharp
await popup.PlayUniTaskAsync("Open", destroyCancellationToken);
```

### 중간부터 재생, With, Build

시간을 주거나 `With`로 설정한 재생도 UniTask로 기다립니다. `Build`로 만든 상태는 `.With()`로 이어서 씁니다.

```csharp
await anim.PlayUniTaskAsync("Intro", 1.5f, destroyCancellationToken);   // 1.5초부터
await popup.With("Open").WithEase(panel, KeyTweenProperty.Scale, KeyTweenEase.OutBack).PlayUniTaskAsync(destroyCancellationToken);
await anim.Build("Pop").Track(icon.transform, KeyTweenProperty.Scale).Key(0f, Vector3.zero).Key(0.2f, Vector3.one).With().PlayUniTaskAsync();
```

## R3

R3(`com.cysharp.r3` 1.0.0 이상)를 설치하면, 재생과 애니메이션 이벤트를 Observable로 다루는 확장 메서드가 추가됩니다.

| 메서드 | 내용 |
|---|---|
| `PlayAsObservable(stateName)` | 구독하면 재생을 시작하고, 재생이 끝나면 `Unit` 하나를 내보낸 뒤 완료됩니다. |
| `EventsAsObservable()` | 모든 애니메이션 이벤트를 내보냅니다. |
| `EventAsObservable(eventName)` | 이름이 같은 이벤트만 내보냅니다. |
| `PlayAsObservable(stateName, time)`, `play.PlayAsObservable()` | 그 시간부터 재생, `With`로 설정한 재생(`KeyTweenPlay`). 끝나는 규칙은 `PlayAsObservable(stateName)`과 같습니다. |
| `StateStartedAsObservable()`, `StateCompletedAsObservable()`, `StateCanceledAsObservable()` | 상태가 시작하거나, 끝에 닿거나, 끝까지 못 갔을 때 그 상태 이름을 내보냅니다. |

### R3 설치하기

R3는 Unity 패키지와 R3 본체 DLL이 모두 필요합니다.

1. Package Manager에서 다음 Git 주소로 Unity 패키지를 설치합니다.

   ```text
   https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity
   ```

2. NuGet에서 R3 본체 DLL을 설치합니다. NuGetForUnity 같은 도구를 쓸 수 있습니다.

### 재생 끝 받기

`PlayAsObservable`은 재생이 끝나면 `Unit` 하나를 내보냅니다. 연타는 `ThrottleFirst`로 막습니다.

```csharp
open.OnClickAsObservable().ThrottleFirst(TimeSpan.FromSeconds(0.5))
    .SelectMany(_ => popup.PlayAsObservable("Open"))
    .Subscribe(_ => Debug.Log("열림")).AddTo(this);
```

* 끝나지 못하면 `OperationCanceledException`으로 완료됩니다([API](API.md#끝나는-경우)).
* 구독을 해제하면 재생이 그 자리에서 멈춥니다.

### 마지막 재생만 남기기

닫기 버튼에서는 `Switch`로 마지막 재생만 남깁니다.

```csharp
close.OnClickAsObservable()
    .Select(_ => popup.PlayAsObservable("Close"))
    .Switch()
    .Subscribe().AddTo(this);
```

### 이벤트 받기

`EventAsObservable`은 이름이 같은 이벤트만 내보냅니다. 모든 이벤트를 받으려면 `EventsAsObservable`을 씁니다.

```csharp
popup.EventAsObservable("Shown").Subscribe(e => Debug.Log(e.Time)).AddTo(this);
```

### With 재생과 상태 알림

`With`로 설정한 재생도 구독하면 재생을 시작합니다. 구독을 해제하면 재생이 그 자리에서 멈춥니다.

```csharp
var open = popup.With("Open").WithEase(panel, KeyTweenProperty.Scale, KeyTweenEase.OutBack);
openButton.OnClickAsObservable().SelectMany(_ => open.PlayAsObservable()).Subscribe().AddTo(this);
```

어느 상태가 시작하고 끝났는지는 상태 알림으로 받습니다. 상태 이름이 값으로 옵니다.

```csharp
popup.StateCompletedAsObservable()
    .Where(state => state == "Open")
    .Subscribe(_ => EnableInput()).AddTo(this);
```

## UIEffect

UIEffect(`com.coffee.ui-effect` 5.x)를 설치하면, UIEffect가 붙은 오브젝트의 트랙 추가 창에 **UIEffect 트랙 14종**이 나옵니다. 트랙 이름은 `UIEffect · …` 형식이며, UIEffect가 제공하는 Timeline 트랙과 같은 목록입니다.

### 추가되는 트랙

| 트랙 | 범위 |
|---|---|
| Tone Intensity, Color Intensity, Sampling Intensity, Transition Rate(디졸브 등), Edge Shiny Rate, Gradation Intensity, Detail Intensity | 0 ~ 1 |
| Gradation Offset | −1 ~ 1 |
| Gradation Rotation | 0 ~ 360 |
| Gradation Scale | 0.01 ~ 10 |
| Color, Transition Color, Edge Color, Detail Color | 색 |

### 트랙 편집하기

다른 트랙과 똑같이 다음 기능을 모두 쓸 수 있습니다.

* 키
* 이징
* Start(Current 등)
* 미리보기
* 역재생
* 반복

숫자 트랙은 범위에 맞는 슬라이더로, 색 트랙은 색 칸으로 편집합니다.

### UIEffect를 제거하면

UIEffect를 제거하면 이 트랙들은 목록에 나오지 않습니다. 이미 저장된 트랙은 아무 동작도 하지 않으며, 오류도 나지 않습니다.

### 연동 구조

코어 어셈블리는 UIEffect를 참조하지 않습니다.

* `KeyTweenProperty`에는 속성 이름만 있습니다.
* 읽기와 쓰기, 범위는 연동 어셈블리가 `KeyTweenExternalProperties.Register`로 등록합니다.

다른 라이브러리도 같은 방식으로 연동할 수 있습니다. 자세한 내용은 Design을 참고하세요.

## 개발 저장소에서의 확인

연동 코드가 라이브러리 없이도, 라이브러리와 함께도 동작하는지 두 가지로 확인합니다.

### main 브랜치

`main` 브랜치에는 이 라이브러리들을 설치하지 않습니다. 그래서 CI는 연동 코드가 빠진 상태로도 컴파일되는지 확인합니다.

### 연동 브랜치

라이브러리를 설치한 상태의 테스트는 다음 브랜치에서 실행합니다.

* `integration/unitask`
* `integration/r3`
* `integration/uieffect`

이 브랜치들은 라이브러리의 새 버전과 `main`의 변경을 매일 자동으로 따라갑니다. 자세한 내용은 Development를 참고하세요.
