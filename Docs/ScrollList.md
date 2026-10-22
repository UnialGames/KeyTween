# 스크롤 리스트 — `KeyTweenScrollList`

`ScrollRect`에 붙이는 **가상화 리스트**입니다. 화면에 보이는 아이템만 만들고, 스크롤하면 그 오브젝트들을 재사용합니다.
스크롤 형태는 따로 컴포넌트를 두지 않고 이 리스트의 설정 조합으로 만들고, **연출은 전부 아이템(또는 점·헤더)의 `KeyTween` 상태**입니다.

추가: `Add Component > KeyTween > Scroll List` (ScrollRect가 있는 오브젝트에)

## 1. 아이템과 배치

인스펙터 구성은 Unity Layout Group과 같습니다.

| 항목 | 내용 |
|---|---|
| Item Prefab | 아이템 Prefab(0번). 그 크기가 기본 아이템 크기 |
| Extra Prefabs | 여러 종류 아이템(1번…). 코드의 `prefabOf(index)`로 고름. 종류마다 따로 재사용 |
| Measure Items | 아이템 크기를 레이아웃의 선호 크기로 잼(글이 길면 커지는 말풍선) |
| Layout | Vertical, Horizontal, Grid, Masonry |

| Layout | 설정 |
|---|---|
| Vertical / Horizontal | Padding, Spacing, Child Alignment, Reverse Arrangement, Control Child Size(Width 또는 Height: 스크롤 방향과 직각으로 늘림) |
| Grid | Padding, Cell Size, Spacing, Child Alignment, Constraint(Flexible / Fixed Column Count / Fixed Row Count), Constraint Count |
| Masonry | Grid와 같고 Cell Size는 Column Size(열 폭). 아이템은 가장 짧은 열에 쌓임 |

- 스크롤 방향은 Layout으로 정해집니다. Grid에서 Fixed Row Count면 가로 스크롤입니다.
- 스크롤 방향 아이템 크기: `sizeOf(index)` 콜백 → Measure Items → Prefab 크기 순. Grid는 Cell Size
- 내용이 뷰포트보다 짧으면 Child Alignment가 스크롤 방향에도 적용됩니다(채팅은 Lower로 아래부터 쌓음).
- 아이템 위치는 레이아웃이 바뀔 때 한 번 계산하고 `LayoutGroup`은 쓰지 않습니다. 스크롤하지 않을 때 비용이 없습니다.

## 2. 데이터 연결

UI Toolkit `ListView`처럼 콜백으로 연결합니다. 아이템 Prefab에 상속 제약은 없고, `ItemView`는 Prefab에 붙은 아무 컴포넌트입니다. 바인딩은 아이템이 보이게 될 때만 호출됩니다.

```csharp
list.SetCount(100, (ItemView view, int index) => view.Set(items[index]),
                   (ItemView view, int index) => view.Clear());          // unbind는 선택
list.SetData(items, (ItemView view, Item item, int index) => view.Set(item));
list.SetCount(48, (RectTransform item, int index) => ...);               // 여러 Prefab일 때
list.prefabOf = i => items[i].isHeader ? 1 : 0;                          // SetCount 전에

list.SetCount(10);      // 개수만 변경: 스크롤 위치 유지, 남은 아이템 그대로, 새로 보이는 것만 Appear
list.Refresh();         // 보이는 아이템 전부 다시 바인딩 (Measure Items면 크기도 다시)
list.RefreshItem(5);
```

- 바인딩과 함께 부르는 `SetCount`/`SetData`는 처음부터 다시 채웁니다(시작 위치로, Stick To End면 끝으로, Play On Fill이면 진입 연출).
- 개수만 바꾸는 `SetCount(count, stickDuration = 0.25f)`는 새 아이템만 잽니다. Stick To End이고 끝을 보고 있었으면 `stickDuration`초 동안 새 끝으로 미끄러집니다.

## 3. 위치와 이동

| API / 설정 | 내용 |
|---|---|
| `Count`, `VisibleRange`, `Offset`, `AtEnd`, `ItemSize` | 개수, 보이는 범위, 시작부터 스크롤한 픽셀, 끝에 있는지, 기본 크기 |
| `GetItem(index)`, `GetItem<TView>(index)` | 보이는 중이면 그 오브젝트, 아니면 null |
| `IndexAt(screenPoint, camera)` | 화면 좌표(포인터) 아래의 아이템, 없으면 −1 |
| `ItemPosition(index)`, `SizeOf(index)`, `NearestIndex(alignment)` | 아이템 위치·크기, 가장 가까운 아이템 |
| `ScrollTo(index, alignment = Start, duration = 0)` | 그 아이템을 시작·가운데·끝에 맞춤(OutCubic, duration 0이면 바로). 루프면 가까운 쪽으로 |
| `CurrentIndex`, `CurrentIndexChanged` | Snap Alignment 자리에 있는 아이템(페이지, 캐러셀·휠 선택) |
| `ItemShown` / `ItemHidden` | 아이템이 보이게 됨(바인딩 뒤) / 사라짐(해제 전) |
| `RemoveItem(index)` | 데이터에서 하나를 지운 뒤 호출. 그 아이템만 사라지고 아래 아이템은 오브젝트·연출 상태 그대로 한 칸씩 올라옴(다시 바인딩·등장 연출 없음) |
| `ResizeItem(index, size, duration)` | 한 아이템 크기를 애니메이션으로 바꾸고 뒤 아이템이 따라 움직임(아코디언). 끝나면 완료되는 `Awaitable`: `await list.ResizeItem(i, 0)` 뒤 데이터에서 지우기 |
| `PlayEntry()`, `Rebuild()` | 진입 연출 다시 재생, 배치 다시 계산 |

**Scrolling 설정**
| 설정 | 내용 |
|---|---|
| Loop | 끝 다음에 처음이 이어짐(Vertical·Horizontal만) |
| Snap: None / Nearest / One Per Swipe | 놓으면 가장 가까운 아이템에 맞춤 / 드래그 한 번에 정확히 한 칸(30px 미만은 제자리). Alignment, Duration |
| Auto Scroll (s) | 초마다 다음 아이템으로. 드래그 중엔 멈춤(배너) |
| Stick To End | 끝을 보고 있을 때 아이템이 늘면 끝 유지(채팅) |
| Sticky Header | 고른 Prefab 종류가 섹션 헤더. 지나간 헤더가 위에 고정되고 다음 헤더가 밀어 올림(Vertical·Horizontal만) |

- **중첩**: 리스트 축과 다른 방향 드래그는 감싼 `ScrollRect`로 넘깁니다(페이지 안의 세로 리스트, 세로 리스트 안의 가로 줄).

## 4. 아이템 애니메이션

상태는 아이템 Prefab의 `KeyTween`에서 고르고, 상태 줄의 **Edit**(그 상태를 KeyTween Editor로 엶) 또는 **Create**(없으면 `KeyTween`과 상태를 만들고 엶)로 바로 편집합니다. Prefab 에셋이면 Prefab 모드로 먼저 엽니다.

| 항목 | 동작 |
|---|---|
| Entry State + Stagger + Play On Fill | 처음 채울 때(또는 `PlayEntry()`) 보이는 아이템이 Stagger 간격으로 차례로 재생. 기다리는 아이템은 시작 모습, 끝난 뒤 들어오는 아이템은 끝 모습 |
| Appear State | 스크롤로 새로 보이게 된 아이템에 재생. 처음 채울 때와 다른 아이템의 크기 변경·삭제로 화면에 들어온 아이템은 재생하지 않음 |
| Scrub State | 아이템의 뷰포트 안 위치(0 = 시작 가장자리, 0.5 = 가운데, 1 = 끝)를 상태 시간으로 씀(`Sample`). 캐러셀 확대, 휠 피커 기울기, 페이지 패럴랙스 |

재사용되는 오브젝트는 이전 인덱스의 모습이 남아 있을 수 있어서, 보이게 될 때 진입 끝 모습으로 맞춘 뒤 등장 상태를 재생합니다.

## 5. 에디터 미리보기

인스펙터 **Preview**(편집 모드, Item Prefab이 있을 때): Items(1~200)개를 저장되지 않는 임시 오브젝트로 배치하고(**Show** / **Update**), **Scroll** 슬라이더로 위치(스크럽 상태 포함)를, **Play Entry**로 진입 연출을 Play 모드 없이 봅니다. **Clear**로 지웁니다.

## 6. 도우미 컴포넌트

| 컴포넌트 | 쓰임 | 상태 |
|---|---|---|
| `KeyTweenPageIndicator` | 페이지 점, 배너 점, 탭 바. 아이템 Prefab을 리스트 개수만큼 만들고(이 오브젝트의 Layout Group이 배치), 아이템의 Button을 누르면 그 아이템으로 이동. `labels`로 탭 글자 | On / Off |
| `KeyTweenScrollScrub` | 스크롤 거리(0 ~ Distance px)를 상태 시간으로. 접히는 헤더 | 아무 상태 1개 |
| `KeyTweenPullToRefresh` | 리스트와 같은 오브젝트에. 시작보다 더 당긴 거리로 Pull, Distance를 넘겨 놓으면 `onRefresh` + Refreshing 반복, 그동안 위쪽을 열어 둠(`Refreshing` = true). `EndRefresh()`로 닫힘. 세로 리스트만 | Pull / Refreshing |

세 컴포넌트의 상태 칸에도 **Edit** / **Create** 버튼이 있어 바로 KeyTween Editor로 엽니다.

## 7. 만들 수 있는 형태

샘플 **UI Components**(Package Manager › KeyTween › Samples)의 `Scenes/Scroll.unity`에 18가지 예시가 있습니다(Development).

| 형태 | 설정 / 상태 |
|---|---|
| 세로·가로 리스트, 그리드, 가로 그리드 | Layout, Entry `Show` + Appear `Appear` |
| 캐러셀 | Scrub `Focus`(가운데 확대), Snap Nearest Center |
| 페이징 | One Per Swipe, Scrub `Parallax`, Page Indicator |
| 무한 루프 | Loop + Scrub + Snap |
| 자동 배너 | Loop + One Per Swipe + Auto Scroll + Page Indicator |
| 휠 피커 | Scrub `Wheel`(Rotation X·투명도·크기), Snap Nearest Center |
| 섹션 헤더 | Prefab 2종, Sticky Header |
| 가변 크기, 채팅 | Measure Items (+ Stick To End, Lower 정렬) |
| 아코디언 | `Open`/`Close` 상태 + `ResizeItem` |
| 메이슨리 | Masonry |
| 중첩 스크롤 | 세로 리스트의 줄마다 가로 리스트 |
| 탭 | Page Indicator(On/Off 밑줄) + One Per Swipe |
| 접히는 헤더 | Scroll Scrub |
| 당겨서 새로고침 | Pull To Refresh |
