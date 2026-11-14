# 자주 묻는 질문과 문제 해결

처음 쓸 때 자주 나오는 질문, 잘 안 될 때 확인할 것, 다른 도구에서 옮겨 올 때의 대응, 버전과 지원 범위를 모았습니다.

## 자주 묻는 질문

### Unity 2022 LTS에서 쓸 수 있나요?

쓸 수 없습니다. Unity 6(6000.0) 이상이 필요합니다. 이유는 [버전과 지원 범위](#버전과-지원-범위)에 있습니다.

### UI Toolkit에서도 쓸 수 있나요?

KeyTween은 uGUI(Canvas)용입니다. UI Toolkit 요소를 움직이는 트랙은 없습니다. UI가 아닌 오브젝트는 움직일 수 있습니다. Transform, SpriteRenderer, 재질 속성, Light, Camera, AudioSource 트랙이 있고, 다른 컴포넌트는 [커스텀 트랙](API.md#커스텀-트랙-만들기)으로 더합니다.

### DOTween이나 LitMotion과 함께 써도 되나요?

됩니다. KeyTween은 다른 트윈 라이브러리에 의존하지 않고, 서로 영향을 주지 않습니다. 다만 같은 오브젝트의 같은 속성을 두 쪽에서 동시에 움직이면 프레임마다 나중에 쓴 값이 보입니다. 한 속성은 한 쪽에서만 움직이세요.

### 애니메이션은 어디에 저장되나요?

Prefab(또는 씬 오브젝트)의 `KeyTween` 컴포넌트 안에 저장됩니다. 클립이나 컨트롤러 같은 별도 에셋은 없습니다. 버전 관리에서는 그 Prefab 파일의 변경으로 보입니다.

### 코드를 쓰지 않고도 쓸 수 있나요?

상태마다 [Play On](Guide.md#4-play-on)에서 재생 시점(포인터 진입, 클릭, 켜질 때 등)을 고르면 코드 없이 재생됩니다. Button의 `OnClick` 같은 UnityEvent에서 `KeyTween.Play (string)`을 고르는 방법도 있습니다.

### 상태 이름을 문자열로 쓰는데, 오타가 나면 어떻게 되나요?

없는 이름을 재생하면 콘솔에 경고(`[KeyTween] '오브젝트' has no state '이름'.`)가 남고, `PlayAsync`는 바로 완료됩니다. 이름을 여러 곳에서 쓴다면 상수로 두는 편이 안전합니다. 상태가 있는지는 `anim.HasState("이름")`으로 확인합니다.

### 게임을 일시 정지(timeScale 0)하면 UI 연출도 멈춥니다.

기본값이 게임 시간을 따르기 때문입니다. 일시 정지 메뉴처럼 멈춘 동안에도 움직여야 하는 UI는 그 `KeyTween`의 **Time**을 `Unscaled`로 바꿉니다.

### 어떤 플랫폼에서 확인했나요?

개발, 테스트, 성능 측정은 Windows(에디터와 IL2CPP 플레이어)에서 했습니다. 다른 플랫폼에서는 확인하지 않았습니다. 플랫폼별 코드는 없고, 이징 계산에 Burst를 씁니다.

### 샘플을 가져오려면 무엇이 필요한가요?

Input System 패키지가 필요합니다. TextMeshPro의 Essential Resources는 프로젝트에 없으면 샘플을 가져올 때 함께 가져옵니다.

## 문제 해결

### 재생했는데 아무 일도 일어나지 않습니다

* **콘솔에 경고가 있는지 봅니다.** 상태 이름이 틀리면 `has no state` 경고가 남습니다.
* **Time 설정을 봅니다.** `Time.timeScale`이 0이면 Time이 `Scaled`인 KeyTween은 멈춰 있습니다.
* **트랙의 대상을 봅니다.** 대상 오브젝트가 지워졌거나 비어 있는 트랙은 아무것도 쓰지 않습니다.
* **다른 상태를 봅니다.** 나중에 재생한 상태가 같은 속성을 가져갑니다. 자세한 내용은 [여러 상태가 겹칠 때](Guide.md#11-여러-상태가-겹칠-때)를 참고하세요.
* **레이아웃 그룹을 봅니다.** Layout Group 안의 오브젝트는 위치와 크기를 레이아웃이 다시 정합니다. 그 안에 자식을 하나 두고 그 자식을 움직이거나, Scale 트랙을 씁니다.

### 에디터 미리보기는 되는데 Play 모드에서 재생되지 않습니다

* 상태에 Play On이 없으면 코드에서 재생해야 합니다.
* 포인터로 재생하는 상태는 씬에 EventSystem이 있고, 그 오브젝트의 그래픽에 Raycast Target이 켜져 있어야 합니다.
* 같은 오브젝트의 Selectable(Button 등)이 `interactable`이 아니면 입력으로 재생하는 Play On은 무시됩니다.

### `PlayAsync`가 `OperationCanceledException`을 던집니다

재생이 끝까지 가지 못했다는 뜻입니다. 같은 상태를 다시 재생했거나, 다른 상태가 속성을 모두 가져갔거나, 오브젝트가 꺼졌거나, `Stop`을 불렀을 때입니다. 전체 경우는 [끝나는 경우](API.md#끝나는-경우)에 있습니다.

* 끝까지 갔을 때만 다음 코드를 실행하려는 것이라면 정상 동작입니다. `try` / `catch (OperationCanceledException)`로 받습니다.
* 앞 상태를 끝 모습으로 마무리하고 넘어가려면 다음 상태를 재생하기 전에 `Complete`를 부릅니다.

### 연출이 중간에 끊기면 값이 튑니다

그 트랙의 **Start**가 `Key Value`이면 재생할 때마다 첫 키의 값에서 시작합니다. 포인터로 끊길 수 있는 연출(호버, 누르기)은 Start를 `Current Value`로 둡니다. 그러면 지금 값에서 이어집니다.

### 많은 오브젝트를 한꺼번에 재생하면 그 프레임이 끊깁니다

재생을 시작할 때 드는 비용이 있습니다. 수천 개를 같은 프레임에 시작한다면 몇 프레임에 나눠 시작합니다. 수치는 [성능](Performance.md)에 있습니다.

### 커스텀 트랙이 `(missing)`으로 나오거나 빌드에서 움직이지 않습니다

커스텀 트랙은 저장된 클래스 이름으로 찾습니다.

* 클래스의 이름, 네임스페이스, 어셈블리를 바꾸면 예전 트랙이 클래스를 찾지 못합니다.
* 빌드에서 코드가 제거되는 설정(IL2CPP, Managed Stripping Level이 Minimal보다 높음)이면 클래스에 `[Preserve]`를 붙입니다.

### Prefab 모드에서 고친 내용이 실행 중인 화면에 보이지 않습니다

이미 만들어진 실행 중 복사본에는 반영되지 않고, 새로 만들어지는 것부터 반영됩니다. Play 모드에서 그 복사본을 골라 직접 고치면 바로 보이고 Prefab에도 저장됩니다.

### `Sample`이 폐기되었다는 경고가 나옵니다

`Stop(state, time)`으로 바꿉니다. 하는 일은 같습니다.

### 에이전트의 쓰기가 `Stale token`으로 거부됩니다

읽은 뒤에 데이터가 바뀌었다는 뜻입니다. 다시 읽고, 그 문서에서 다시 고쳐서 씁니다. 자세한 내용은 [Integrations](Integrations.md)를 참고하세요.

## 다른 도구에서 옮겨 오기

자동 변환 도구는 없습니다. 연출을 KeyTween 창에서 다시 만듭니다. 화면이나 Prefab 하나씩 옮기면 되고, 옮기는 동안 예전 도구와 함께 써도 됩니다.

### DOTween, LitMotion에서

| 코드 트윈 | KeyTween |
|---|---|
| 트윈 하나, 시퀀스 하나 | 상태 하나 |
| `DOScale`, `DOColor`, `BindToLocalScale` … | 그 속성의 트랙과 키 |
| `SetEase`, `WithEase` | 키의 Arrival(Ease) |
| 시퀀스의 `Append`, `Insert`, `SetDelay` | 키를 놓는 시간. 여러 트랙이 한 타임라인을 씀 |
| `SetLoops(-1)`, `WithLoops(-1)` | 상태의 Loop |
| 지금 값에서 시작 | 트랙의 Start = `Current Value` |
| `From` | 트랙의 Start = `Key Value` |
| `DOPunch…`, `DOShake…`, `LMotion.Punch`, `LMotion.Shake` | 키의 Arrival = Punch / Shake |
| 글자를 하나씩 보이기 | Reveal (Typewriter) 트랙 |
| 숫자 세기 | Number (Count) 트랙, 끝 값은 `With(...).WithEndValue(...)` |
| `OnComplete` | `await anim.PlayAsync(...)`, 상태의 On Complete, `WithOnComplete` |
| `Kill`, `Cancel` | `Stop` |
| `Complete` | `Complete` |
| `Goto`, `handle.Time` | `Play(state, time)`, `Stop(state, time)` |
| `SetUpdate(true)` (일시 정지 무시) | KeyTween의 Time = `Unscaled` |

코드에서 계속 만들고 싶은 연출은 `Build(...)`로 만들 수 있습니다. 다만 KeyTween의 장점은 연출을 에디터에 두는 데 있으므로, 실행 중에야 알 수 있는 값만 `With`로 넘기는 방식을 권합니다.

### Animator에서

| Animator | KeyTween |
|---|---|
| 클립 | 상태 |
| 컨트롤러의 상태와 전환, `SetTrigger` | 코드의 `Play("이름")` 또는 Play On |
| 클립의 Loop Time | 상태의 Loop |
| Animation Event | 이벤트 트랙 |
| Override Controller | 자식 상태의 트랙 오버라이드 |
| Update Mode = Unscaled Time | KeyTween의 Time = `Unscaled` |

* 전환 그래프가 없습니다. 어느 상태를 언제 재생할지는 코드나 Play On이 정합니다.
* 클립의 키는 그대로 가져올 수 없습니다. [녹화](Guide.md#15-녹화-)를 쓰면 Scene 뷰와 Inspector에서 값을 바꾸는 것만으로 키가 들어갑니다.

## 버전과 지원 범위

### 지원하는 Unity

Unity 6(6000.0) 이상입니다.

* 재생을 기다리는 `PlayAsync`가 Unity의 `Awaitable`을 돌려주고, uGUI 패키지 2.0(TextMeshPro 포함)에 의존합니다. 둘 다 Unity 6에 들어 있어서 2022 LTS에서는 컴파일되지 않습니다.
* 6000.0.85f1에서 설치와 컴파일을 확인했습니다. 개발과 전체 테스트는 6000.6에서 합니다.

### 버전 규칙

[Semantic Versioning](https://semver.org)을 따릅니다. 1.0.0 전(0.x)에는 다음과 같이 올립니다.

* **MINOR** (0.3 → 0.4): 기능 추가, 그리고 API 변경이 있을 수 있습니다.
* **PATCH** (0.3.1 → 0.3.2): 버그 수정과, 재생 동작을 바꾸지 않는 에디터 개선입니다.
* 없애는 API는 먼저 `[Obsolete]`로 표시하고 대신 쓸 것을 알려 준 뒤, 그다음 MINOR 버전에서 지웁니다.
* Prefab에 저장된 데이터는 새 버전에서 불러올 때 자동으로 변환됩니다.

버전마다 바뀐 것은 [CHANGELOG](../CHANGELOG.md)에 있습니다. 쓰는 버전을 고정하려면 설치 주소 뒤에 `#v0.4.0`처럼 태그를 붙입니다.

### 문제를 알리려면

[GitHub Issues](https://github.com/UnialGames/KeyTween/issues)에 올립니다. Unity 버전, KeyTween 버전, 다시 일으키는 순서를 함께 적으면 확인이 빠릅니다.
