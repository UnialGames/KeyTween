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

## AI 에이전트 (Unity CLI)

Claude Code, Cursor, Codex 같은 코딩 에이전트가 KeyTween 애니메이션을 만들고 고칠 수 있습니다. KeyTween에는 AI 모델이 들어 있지 않습니다. 에이전트가 애니메이션 데이터를 읽고 쓰는 명령과, 그 형식을 설명하는 계약 문서를 제공합니다.

에이전트가 만든 결과는 사람이 만든 것과 같은 상태, 트랙, 키입니다. 그래서 KeyTween 창에서 열어 키를 끌고, 이징을 바꾸고, 미리 보며 이어서 고칩니다.

### 준비하기

1. Unity CLI 패키지를 설치합니다. 프로젝트 폴더에서 `unity pipeline install`을 실행합니다(Unity 6, 패키지 `com.unity.pipeline`). 설치되면 KeyTween의 명령이 자동으로 컴파일됩니다.
2. **Tools > KeyTween > Set Up Agent Access**를 고릅니다. 프로젝트 폴더의 `AGENTS.md`와 `CLAUDE.md`에 KeyTween 구역이 들어가고, 에이전트는 거기서 명령과 계약 문서를 읽는 방법을 알게 됩니다. 다시 골라도 구역은 하나만 남습니다. 패키지를 업데이트한 뒤에는 한 번 더 골라 계약 문서의 파일 경로를 새로 적습니다(명령 `keytween_contract`는 업데이트와 상관없이 동작합니다).
3. Unity 에디터를 연 채로 에이전트에게 요청합니다.

```text
Assets/UI/Button.prefab에 호버 연출을 넣어 줘. 살짝 커지면서 밝아지고, 포인터를 빼면 돌아오게.
```

MCP 클라이언트에서 쓰려면 `unity mcp configure <클라이언트>`로 Unity CLI를 MCP 서버로 등록합니다. 지원하는 클라이언트는 `unity mcp configure --list`로 봅니다.

### 명령

읽기와 두 가지 쓰기가 따로 있어서, 읽기만 허용하거나 씬 수정만 허용할 수 있습니다.

| 명령 | 바꾸는 것 | 설명 |
|---|---|---|
| `keytween_contract` | 없음 | 계약 문서(형식과 규칙)를 설치된 버전 그대로 돌려줍니다. 에이전트가 처음 쓰기 전에 읽습니다. |
| `keytween_read` | 없음 | 대상의 상태(트랙, 키, Play On)와, 움직일 수 있는 오브젝트·트랙 목록을 JSON 문서로 돌려줍니다. 대상을 주지 않으면 열린 씬과 Prefab 모드의 KeyTween 목록을 돌려줍니다. |
| `keytween_write_scene` | 열린 씬의 오브젝트 | 메모리에서만 바꿉니다. 씬은 저장되지 않은 상태로 표시되고, 이 명령은 씬을 저장하지 않습니다. |
| `keytween_write_prefab` | Prefab 애셋 | 그 Prefab의 모든 인스턴스에 적용되고 저장됩니다. Prefab 모드에 열려 있으면 열린 오브젝트를 바꾸고, 저장은 KeyTween 창의 Auto Save 또는 Save를 따릅니다. |

```text
unity command keytween_read --target Assets/UI/Button.prefab
unity command keytween_write_prefab --file Temp/hover.json --dry_run true
unity command keytween_write_prefab --file Temp/hover.json
```

대상은 `KeyTween`이 있는(또는 붙일) 오브젝트입니다. Prefab 애셋은 `Assets/UI/Button.prefab`, 그 안의 오브젝트는 `Assets/UI/Popup.prefab:Panel/OkButton`, 열린 씬의 오브젝트는 `/Canvas/Button`으로 적습니다.

### 안전 장치

* **읽은 뒤에 바뀌었으면 거부합니다.** 읽기는 토큰을 돌려주고, 쓰기는 그 토큰을 함께 보냅니다. 그 사이에 사람이 고쳤거나 다른 쓰기가 있었으면 쓰기를 거부하므로, 에이전트는 다시 읽고 그 문서에서 다시 고칩니다.
* **전부 적용하거나 전혀 적용하지 않습니다.** 없는 경로, 없는 트랙, 개수가 틀린 값, 없는 이징 이름을 한 번에 모두 알려 주고 아무것도 바꾸지 않습니다.
* **Ctrl+Z 한 번으로 되돌립니다.** 컴포넌트 추가와 Play On 설정까지 하나의 Undo 단계입니다.
* **확인만 할 수 있습니다.** `--dry_run true`는 문서를 검사하고 무엇이 바뀔지만 알려 줍니다.
* **Play 모드에서는 쓰지 않습니다.** 열려 있지 않은 씬을 대신 열지도 않습니다.

### 문서로 쓸 수 있는 것

* 쓸 수 있는 것: 상태 만들기·바꾸기·지우기, 반복, Play On, 트랙(내장, Material, 커스텀), 키와 도착 방식, 애니메이션 이벤트
* 그대로 남기는 것: 사운드, 자식 호출, 콜백. 상태를 바꿔 써도 지워지지 않습니다.
* 지원하지 않는 것: 오버라이드, 상태 이름 바꾸기

형식과 규칙은 패키지의 `Editor/Agent/KEYTWEEN-CONTRACT.md`에 있고, `unity command keytween_contract`로도 읽습니다. 명령은 `KeyTween`과 Play On용 `KeyTweenTrigger` 말고 다른 컴포넌트를 추가하지 않습니다. 그래서 팝업 전체를 한 번에 페이드하려면 그 오브젝트에 `CanvasGroup`을 미리 붙여 둡니다. 필드 이름을 잘못 적으면 그 필드는 무시되고 기본값이 쓰이므로, 에이전트에게는 `--dry_run true`로 먼저 확인하게 하는 편이 안전합니다.

### 다른 경로에서 부르기

명령은 `KeyTweenAgent`의 공개 메서드를 감싼 것입니다. Unity CLI를 쓰지 않는 도구는 에디터 코드에서 직접 부를 수 있습니다.

```csharp
using KeyTweening.Editor;

KeyTweenAgentDocument doc = KeyTweenAgent.Read("Assets/UI/Button.prefab");
KeyTweenAgentResult result = KeyTweenAgent.Write(json, KeyTweenAgentScope.Prefab, dryRun: true);
```

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
