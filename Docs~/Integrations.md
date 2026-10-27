# 선택 연동 — UniTask · R3 · UIEffect

패키지는 서드파티 없이 동작합니다. 아래 라이브러리가 **설치돼 있으면** 연동 코드가 자동으로 컴파일됩니다. 설치하지 않으면 연동 코드는 컴파일에서 빠지고, 아무 설정도 필요 없습니다(LitMotion·VContainer의 UniTask 연동과 같은 방식).

| 라이브러리 | 켜지는 조건 (asmdef versionDefines) | 추가되는 것 |
|---|---|---|
| UniTask | `com.cysharp.unitask` 2.0.0 이상 → `KEYTWEEN_UNITASK` | 확장 메서드 3개 |
| R3 | `com.cysharp.r3` 1.0.0 이상 → `KEYTWEEN_R3` | 확장 메서드 3개 |
| UIEffect | `com.coffee.ui-effect` 5.0.0 이상 → `KEYTWEEN_UIEFFECT` | 트랙 14종 (어셈블리 `KeyTween.UIEffect`) |

## UniTask

```csharp
await UniTask.WhenAll(popup.PlayUniTaskAsync("Open"), dim.PlayUniTaskAsync("Show"));
var info = await anim.WaitEventUniTaskAsync("Shown");
var canceled = await popup.PlayUniTaskAsync("Close").SuppressCancellationThrow();
```

| 메서드 | 내용 |
|---|---|
| `PlayUniTaskAsync(stateName, ct)` | `PlayAsync`의 UniTask판 |
| `PlayReversedUniTaskAsync(stateName, ct)` | 역재생 |
| `WaitEventUniTaskAsync(eventName, ct)` | 다음 애니메이션 이벤트까지 |

완료·취소 규칙은 `PlayAsync`와 같습니다([API](API.md#끝나는-경우)). 내부적으로 `Awaitable`을 `AsUniTask()`로 바꿉니다.

쓰임 예: 열기는 `WhenAll`(팝업과 배경을 함께), 닫기는 `SuppressCancellationThrow`, 느린 상태는 `Timeout` 뒤 `Complete`, 이벤트 기다리기는 `WaitEventUniTaskAsync`. 모두 `destroyCancellationToken`으로 취소하면 오브젝트가 사라질 때 함께 정리됩니다.

## R3

```csharp
open.OnClickAsObservable().ThrottleFirst(TimeSpan.FromSeconds(0.5))
    .SelectMany(_ => popup.PlayAsObservable("Open"))
    .Subscribe(_ => Debug.Log("열림")).AddTo(this);
popup.EventAsObservable("Shown").Subscribe(e => Debug.Log(e.Time)).AddTo(this);
```

| 메서드 | 내용 |
|---|---|
| `PlayAsObservable(stateName)` | 구독하면 재생, 끝나면 `Unit` 하나를 내보내고 완료. 끝나지 못하면 `OperationCanceledException`으로 완료. 구독을 끊으면 그 자리에서 멈춤 |
| `EventsAsObservable()` | 모든 애니메이션 이벤트 |
| `EventAsObservable(eventName)` | 이름이 같은 이벤트만 |

쓰임 예: 연타는 `ThrottleFirst`로 막고, 닫기 버튼은 `Switch`로 마지막 재생만 남기고, `EventAsObservable("Shown")`으로 이벤트를 로그에 남깁니다.
R3 설치에는 Unity 패키지(`https://github.com/Cysharp/R3.git?path=src/R3.Unity/Assets/R3.Unity`)와 NuGet의 R3 본체 DLL(NuGetForUnity 등)이 필요합니다.

## UIEffect

UIEffect(`com.coffee.ui-effect` 5.x)가 붙은 오브젝트의 트랙 추가 창에 **UIEffect 14종**이 나옵니다. 이름은 `UIEffect · …`이고, UIEffect의 Timeline 트랙과 같은 목록입니다.

| 트랙 | 범위 |
|---|---|
| Tone Intensity, Color Intensity, Sampling Intensity, Transition Rate(디졸브 등), Edge Shiny Rate, Gradation Intensity, Detail Intensity | 0 ~ 1 |
| Gradation Offset | −1 ~ 1 |
| Gradation Rotation | 0 ~ 360 |
| Gradation Scale | 0.01 ~ 10 |
| Color, Transition Color, Edge Color, Detail Color | 색 |

- 다른 트랙과 똑같이 키·이징·Start(Current 등)·미리보기·역재생·반복이 됩니다. 숫자는 그 범위의 슬라이더, 색은 색 칸입니다.
- UIEffect를 지우면 이 트랙들은 목록에 나오지 않고, 저장돼 있던 트랙은 아무것도 하지 않습니다(오류 없음).
- 구조: 코어는 UIEffect를 참조하지 않습니다. `KeyTweenProperty`에 이름만 있고, 읽기·쓰기·범위는 연동 어셈블리가 `KeyTweenExternalProperties.Register`로 등록합니다. 다른 라이브러리도 같은 방식으로 붙일 수 있습니다(Design).

## 개발 저장소에서의 확인

`main`에는 이 라이브러리들을 설치하지 않아, 연동 코드가 빠진 상태로 컴파일되는지 CI가 확인합니다. 라이브러리를 설치한 상태의 테스트는 `integration/unitask`, `integration/r3`, `integration/uieffect` 브랜치에서 하고, 새 버전과 `main` 변경을 매일 자동으로 따라갑니다(Development).
