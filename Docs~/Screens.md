# 화면 전환 — Page · Modal · Sheet

UnityScreenNavigator와 같은 역할(화면 쌓기·팝업·탭)을 패키지 안에서 합니다. 전환 연출은 **화면 Prefab의 `KeyTween` 상태**이고, 그 이름의 상태가 없으면 기본 전환을 씁니다. 전부 Unity `Awaitable`이라 서드파티 없이 `await`할 수 있습니다.

## 1. 화면 — `KeyTweenScreen`

화면 Prefab의 루트에 붙입니다(`Add Component > KeyTween > Screen`). `RectTransform`, `CanvasGroup`, `KeyTween`이 함께 필요합니다.

- `renderingOrder`: 페이지 컨테이너에서 높은 페이지가 위에 그려지고, 같으면 넣은 순서대로 쌓입니다(모달·시트 컨테이너는 넣은 순서대로 위).
- 컨테이너가 만든 화면은 컨테이너를 꽉 채웁니다.
- `animated: false`로 전환하면 상태의 끝 모습으로 바로 갑니다.

## 2. 컨테이너

| 컨테이너 | API | 화면의 상태 이름 | 기본 전환 (0.3초) |
|---|---|---|---|
| `KeyTweenPageContainer` | `Push(prefab, animated, stack)`, `Pop(animated)`, `Pages`, `Current` | `PushEnter`, `PushExit`, `PopEnter`, `PopExit` | 한 화면 폭 슬라이드(Size Relative), OutQuart |
| `KeyTweenModalContainer` | `Push(prefab, animated)`, `Pop(animated)`, `Modals`, `Current`, `backdropPrefab` | `Enter`, `Exit` (배경도 `Enter`/`Exit`) | 크기 0.3 → 1 + 알파(OutQuart), 배경은 알파 페이드(Linear) |
| `KeyTweenSheetContainer` | `Register(prefab)`, `Show(index, animated)`, `Sheets`, `ActiveIndex`, `Active` | `Enter`, `Exit`, 낮은 번호로 갈 때 `EnterBackward`, `ExitBackward`(없으면 `Enter`/`Exit`) | 알파 페이드(Linear) |

기본 전환 값은 UnityScreenNavigator의 기본값과 같고, 코드에서 `KeyTweenDefaultTransitions`(PagePushEnter 등, `Slide`, `FadeIn`/`FadeOut`)로 직접 쓸 수도 있습니다.

```csharp
await pages.Push(loadingPage, stack: false);   // 로딩: 다음 페이지가 들어오면 기록에서 빠지고 파괴됨
await pages.Push(homePage);                     // Home의 WillEnter에서 준비하는 동안 로딩이 보임
await modals.Push(confirmPopup);
await modals.Pop();
var tab = sheets.Register(shopTabPrefab);
await sheets.Show(tab);                         // 높은 번호로: Enter, 낮은 번호로: EnterBackward
```

**Page**
- `Push`: 새 페이지를 만들고, 들어오는 페이지와 나가는 페이지의 `Will*`을 기다린 뒤 `PushEnter`와 `PushExit`를 **동시에** 재생합니다. 끝나면 나간 페이지를 끕니다.
- `stack: false`로 넣은 페이지는 다음 페이지가 들어오면 기록에서 빠지고 파괴됩니다(로딩 화면).
- `Pop`: 이전 페이지를 다시 켜고 `PopEnter`·`PopExit`를 재생한 뒤 현재 페이지를 파괴합니다. 페이지가 없으면 아무것도 하지 않습니다.

**Modal**
- 모달마다 배경을 하나씩 만듭니다. `backdropPrefab`을 비우면 검정 75% 배경을 만듭니다.
- 아래에 있는 모달은 움직이지 않고 생명주기도 불리지 않습니다.
- `Pop`은 `Exit`를 재생한 뒤 모달과 배경을 파괴합니다.

**Sheet**
- `Register`로 시트를 만들어 숨겨 두고 번호를 받습니다. `Show`는 같은 번호이거나 범위를 벗어나면 무시합니다. 나간 시트는 꺼집니다.

## 3. 입력 막기

전환 중에는 **모든 컨테이너의 터치가 막힙니다**(`KeyTweenScreenContainer.AnyInTransition`, 컨테이너별 `IsInTransition`, 각 컨테이너의 `CanvasGroup.blocksRaycasts`). 그래서 연타로 전환이 겹치지 않습니다. `interactable`은 건드리지 않아서 버튼이 전환 중에 비활성 색으로 바뀌지 않습니다. 컨테이너에는 `RectTransform`과 `CanvasGroup`이 필요합니다.

## 4. 생명주기 — `IKeyTweenScreenLifecycle`

화면 루트의 아무 컴포넌트가 이 인터페이스를 구현하면 불립니다. 모든 메서드에 기본 구현이 있어서 필요한 것만 씁니다.

```csharp
public sealed class HomePage : MonoBehaviour, IKeyTweenScreenLifecycle
{
    public async Awaitable WillEnter(KeyTweenTransition t) { await LoadAsync(); }   // 애니메이션 전, 기다림
    public void DidEnter(KeyTweenTransition t) { }                                  // 애니메이션 후
    public Awaitable WillExit(KeyTweenTransition t) => KeyTweenScreen.Completed();    // 기다릴 게 없을 때
    public void DidExit(KeyTweenTransition t) { }
}
```

`KeyTweenTransition`: `Kind`(Push / Pop / Show / Hide), `Partner`(상대 화면), `Backward`(뒤로 가는 전환: 페이지·모달의 Pop, 시트의 낮은 번호로), `Animated`

## 5. 공유 요소 전환 — `KeyTweenSharedElement`

목록의 초상화가 상세 화면의 자리로 이어져 커지는 연출입니다.
- 연출 자체는 움직이는 이미지의 **Position·Size 트랙**으로 에디터에서 만듭니다.
- 실행 중에만 알 수 있는 상대 쪽 자리(첫 키 또는 마지막 키)만 `Will*`에서 채웁니다.

```csharp
// RPG 샘플 RpgHeroDetail: 영웅 카드의 초상화(card)가 상세 화면의 초상화(portrait) 자리로
public Awaitable WillEnter(KeyTweenTransition t)
{
    KeyTweenSharedElement.SetPartnerKeys(GetComponent<KeyTween>(), "PushEnter", portrait.rectTransform, card, atStart: true);
    card.GetComponent<Image>().enabled = false;   // 카드 쪽 그림은 숨기고, 상세에서 돌아온 뒤 다시 보이게
    return KeyTweenScreen.Completed();
}

public Awaitable WillExit(KeyTweenTransition t)
{
    if (t.Kind == KeyTweenTransitionKind.Pop)       // 돌아갈 때: 마지막 키를 카드 자리로
        KeyTweenSharedElement.SetPartnerKeys(GetComponent<KeyTween>(), "PopExit", portrait.rectTransform, card, atStart: false);
    return KeyTweenScreen.Completed();
}
```

- `SetPartnerKeys(animation, state, moving, partner, atStart)`: `partner`의 화면상 사각형을 `moving`의 위치·크기로 바꿔 Position·Size 트랙의 첫 키(`atStart`) 또는 마지막 키에 씁니다. 앵커가 한 점(stretch 아님)인 `moving`을 가정합니다.
- 예: RPG 샘플의 영웅 카드 → 상세, 가방 슬롯 → 아이템 정보 팝업, ScreenNavigator 재현의 초상화 뷰어
