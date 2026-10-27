# 사용 설명서 — KeyTween Editor

애니메이션을 만들고, 미리보고, 저장하는 방법입니다. 에디터 UI는 영문이고, 이 문서는 화면에 보이는 이름 그대로 적습니다.

## 1. 개념

| 이름 | 뜻 |
|---|---|
| `KeyTween` | 애니메이션을 담는 컴포넌트. Prefab(또는 씬 오브젝트)에 붙이고, 그 아래 오브젝트들을 움직입니다. |
| 상태 (State) | 이름이 있는 연출 하나. `Hover`, `Open`, `Close` 같은 단위로, 코드도 이 이름으로 재생합니다. |
| 트랙 (Track) | 오브젝트 하나의 속성 하나(예: 닫기 버튼의 Scale). 상태 하나에 여러 개 |
| 키 (Key) | 트랙의 한 시점의 값. Unity Animation 창처럼 1 → 1.2 → 1은 키 3개 |
| 도착 방식 (Arrival) | 이전 키에서 이 키로 오는 방식: 이징, 커브, Punch, Shake |
| Play On | 상태가 저절로 재생되는 시점(포인터 진입, 클릭, 켜질 때 등). 고르지 않으면 코드에서 재생 |

애니메이션 데이터는 별도 에셋 없이 `KeyTween` 컴포넌트에 직렬화됩니다. 그래서 Prefab을 복제하거나 옮겨도 연출이 함께 갑니다.

## 2. 창 열기와 구성

**여는 방법**
- 메뉴 `Window > KeyTween Editor` (창 제목 "KeyTween")
- `KeyTween` 인스펙터의 **Open KeyTween Editor** 버튼
- 다른 컴포넌트의 상태 줄에 있는 **Edit** / **Create** 버튼(스크롤 리스트 등, [ScrollList](ScrollList.md))

창은 선택한 오브젝트(또는 그 부모)의 `KeyTween`을 편집합니다. 편집 중인 대상이 없을 때 RectTransform이 있는 오브젝트를 고르면 **Add KeyTween to '이름'** 버튼이 나옵니다. 이미 다른 `KeyTween`을 편집 중이면 대상이 그대로 남으므로, `Add Component`로 붙이면 됩니다. 인스펙터의 **Raw Data**를 펼치면 직렬화된 데이터를 그대로 볼 수 있습니다.

| 영역 | 내용 |
|---|---|
| 상단 바 | 대상 이름, **▶** 재생/일시정지, **■** 정지(원래 값으로), **⟲** 반복, **●** 녹화, 속도(0.1x · 0.25x · 0.5x · 1x), 현재 시간 / 길이, **Auto Save**, **Save**, 주황 점(저장 안 된 값이 화면에 있을 때) |
| 상태 탭 | 상태마다 탭 하나. 탭 툴팁에 Play On. **+** 탭으로 상태 추가 |
| 타임라인 (왼쪽) | 오브젝트 계층으로 묶인 트랙 줄, 눈금자, 상태 끝(`End`) 표시 |
| 인스펙터 (오른쪽) | 고른 것(상태 / 트랙 / 키 / 이벤트 / 사운드 / 자식 호출)의 설정 |

## 3. 상태

- **추가**: 상태 탭의 **+**. 이름은 `State`, `State 1`, …로 생깁니다.
- **상태 인스펙터** (트랙·키를 고르지 않았을 때)
  - **Name**: 이름 바꾸기. Play On 연결도 새 이름으로 따라갑니다.
  - **Loop**: 끝에 닿으면 0초부터 이음매 없이 이어서 반복합니다(Animator 클립의 Loop Time과 같은 개념). 이벤트·사운드·자식 호출은 바퀴마다 다시 울립니다.
  - 아래에 길이와 트랙 수가 나옵니다(`Duration 0.80s · 3 track(s)`).
  - **Play On**: `None` 또는 이벤트(§4). 이벤트를 고르면 `KeyTweenTrigger` 컴포넌트가 자동으로 붙습니다.
  - **Delete State**: 확인 후 삭제
- 상태 길이는 가장 늦게 끝나는 것(트랙의 마지막 키, 이벤트, 사운드 끝, 자식 호출 끝)으로 정해집니다.

## 4. Play On — 재생 시점

| Play On | 재생되는 때 |
|---|---|
| `None` | 자동 재생 안 함. 코드에서 `Play`/`PlayAsync` ([API](API.md)) |
| `OnEnable` | 컴포넌트가 켜질 때 |
| `Start` | 처음 켜진 뒤 한 번 |
| `PointerEnter` / `PointerExit` / `PointerDown` / `PointerUp` / `Click` | 포인터 이벤트 |
| `Select` / `Deselect` | 선택 이벤트 |
| `ToggleOn` / `ToggleOff` | 같은 오브젝트 `Toggle`의 값 변경 |

- 같은 순간에 재생되는 상태가 둘이면 경고가 나옵니다. 예: `OnEnable`과 `Start`는 Instantiate할 때 둘 다 호출됩니다.
- `interactable`이 꺼진 Selectable은 입력 이벤트를 무시합니다(`OnEnable`·`Start`는 그래도 재생).
- Button의 OnClick 같은 UnityEvent에서는 `KeyTween.Play (string)`(역재생은 `PlayReversed (string)`)을 고르고 상태 이름을 적습니다.

## 5. 트랙 추가

타임라인 빈 곳을 우클릭 → **Add Track...**으로 트랙 추가 창을 엽니다.
- 왼쪽: Prefab 안 오브젝트 트리(검색 가능). 오른쪽: 그 오브젝트에서 만들 수 있는 트랙 카드와 현재 값
- 카드를 클릭해 여러 개 고른 뒤 **Add N**, 또는 더블클릭으로 바로 추가. 오브젝트를 옮겨 다니며 골라도 선택이 유지됩니다. 이미 있는 트랙에는 `Added` 표시
- 새 트랙은 0초 키와 상태 끝 시간의 키(상태가 비었으면 0.2초)로 생기고, 두 키 모두 지금 값입니다.
- 새 트랙의 Start는 **Current Value**입니다(텍스트 트랙은 Key Value). 새 글자별 트랙의 Stagger는 0.05초입니다.

| 대상 | 트랙 (창에 보이는 이름) |
|---|---|
| 모든 오브젝트 | Scale, Rotation, Active |
| RectTransform | Size, Position, Anchor Min, Anchor Max, Pivot |
| CanvasGroup | Group Alpha (화면·팝업 전체 페이드) |
| Graphic (Image, TMP 등) | Color, Alpha |
| Image (Image Type = Filled) | Fill Amount |
| TMP_Text | Font Size, Character Spacing, Reveal (Typewriter), Number (Count) |
| TMP_Text 글자별 | Char Scale, Char Position, Char Rotation, Char Color, Char Alpha |
| Camera | Camera Background, Camera Size |
| UIEffect (설치 시) | UIEffect 14종 ([Integrations](Integrations.md)) |
| 루트 | Animation Events, Sound |
| AudioSource가 있는 오브젝트 | Sound (그 AudioSource로 재생) |

- `Rotation`은 오일러 각(도)이고, 읽을 때 −180~180으로 읽어서 −30°에서 0°로 갈 때 먼 쪽으로 돌지 않습니다.
- `Active`는 키 사이를 보간하지 않고 키 시간에 켜고 끕니다.
- 트랙 대상 바꾸기: 트랙 인스펙터 맨 위의 `경로 · 속성 ▾` 버튼

## 6. 키 편집

**타임라인에서**
| 동작 | 방법 |
|---|---|
| 선택 | 클릭. Shift·Ctrl(Mac Cmd) 클릭으로 추가 선택, 빈 곳 드래그로 박스 선택 |
| 이동 | 선택한 키 드래그(0.01초 단위, 0초 아래로는 안 감) |
| 추가 | 줄 빈 곳 더블클릭(그 시간의 보간 값으로), 트랙 머리의 **◆**(재생 막대 위치), 우클릭 **Add Key Here** |
| 삭제 | Delete / Backspace, 우클릭 **Delete Selected Keys** |
| 재생 막대 | 눈금자 클릭·드래그 |
| 확대·축소 | 눈금자 위 휠, 줄 위 Ctrl+휠 또는 Alt+휠 (포인터 기준) |
| 좌우 이동 | Shift+휠, 가로 휠, 휠 버튼 드래그 |
| 전체 보기 | `F` 또는 **⤢** |
| 접기 | 오브젝트 줄 클릭. 접으면 그 아래 키가 작은 다이아몬드로 요약됨 |

- **모든 트랙은 0초 키를 가집니다.** 0초 키가 없는 트랙에 키를 추가하면 0초 키가 먼저 생깁니다.
- 눈금자에 상태 끝(`End 0.80s`)이 표시되고, 그 뒤는 어둡게 칠해집니다.

**키 인스펙터** — 어떤 키든 Key → Arrival 순서의 같은 구성입니다. Key 섹션에서 해당하지 않는 칸은 숨기지 않고 비활성 + 이유를 보여 줍니다. Arrival 섹션은 Type에 맞는 칸만 보입니다.
- **Key**: Time (s), 값, **Capture Current Value**(Scene에서 오브젝트를 원하는 모습으로 두고 누르면 지금 값을 키에 넣음. 절댓값 트랙만)
  - 값 칸 이름은 뜻에 따라 `Value` / `Value (rest +)` / `Value (press point +)` / `Value (× own size)` / `Strength`(Punch·Shake)
  - Scale은 하나로 묶인 슬라이더(0~2)와 축별 편집 전환 버튼, 색은 색 칸, Active는 체크
  - 재생을 시작할 때 정해지는 값(Start = Current의 첫 키 등)은 비활성
- **Arrival**: Type(Ease / Curve / Punch / Shake), Ease(Curve가 아닐 때), Frequency·Damping(Punch·Shake일 때), Animation Curve(Curve일 때), 곡선 그래프. 첫 키는 도착하는 구간이 없어서 비활성
- 키를 여러 개 고르면 Arrival을 한꺼번에 바꿀 수 있습니다.

## 7. 도착 방식

| Type | 동작 |
|---|---|
| `Ease` | 이징 프리셋 36종: Linear, Sine·Quad·Cubic·Quart·Quint·Expo·Circ·Elastic·Back·Bounce의 In/Out/InOut, CustomAnimationCurve(Linear과 같음), OutBackQuad·Cubic·Quart·Quint(Unity-AnimationUI의 OutBack). 앞 32종은 LitMotion `Ease`와 번호·공식이 같습니다. |
| `Curve` | AnimationCurve. 0 = 이전 키 값, 1 = 이 키 값. 1을 넘으면 지나쳤다 돌아옴 |
| `Punch` | 이전 값을 중심으로 흔들리다 이전 값으로 돌아옵니다. 키 값은 목적지가 아니라 **세기**입니다. |
| `Shake` | Punch와 같지만 축마다 매 프레임 무작위로 흔들립니다. |

Punch·Shake의 `Frequency`는 초당 흔들림 수, `Damping`은 줄어드는 정도입니다(LitMotion과 같은 공식).

## 8. 트랙 옵션 (트랙 인스펙터)

키프레임 트랙(텍스트 트랙 제외)의 옵션입니다. Reveal 트랙은 Scramble, 글자별 트랙은 Stagger·Duration per Char만 있고(§9), Start는 항상 Key Value입니다.

| 옵션 | 뜻 |
|---|---|
| **Start** | 첫 키 값을 어디서 가져올지. `Key Value`: 키에 적힌 값 · `Current Value`: 재생을 시작하는 순간의 값(중간에 끊겨도 값이 튀지 않음. 호버/언호버, 열기/닫기에 씀. 첫 키를 0초 뒤에 두면 그만큼 기다렸다 출발) · `Pointer (press position)`: Position 트랙 전용. 상태를 재생시킨 포인터를 누른 위치. 뒤 키들은 누른 자리 기준이라 `(0,0)`이 "그 자리"(버튼 리플) |
| **Relative To Rest** | 키 값을 원래 값에 더합니다. 원래 값은 실행 중엔 Awake 때, 미리보기에선 시작할 때의 값입니다. 예: 누르면 크기 −(34, 17) |
| **Size Relative** | Position·Size 트랙 전용. 키 값이 대상 자기 크기의 배수입니다(x = 1이면 자기 폭만큼). 해상도가 달라도 같은 비율로 움직입니다. 예: 화면이 오른쪽 한 화면(x = 1)에서 0으로 들어오기 |

## 9. 텍스트 트랙

- **Reveal (Typewriter)**: 보이는 글자 수를 0~1 비율로 지정합니다. 리치 텍스트 태그는 글자 수에 넣지 않습니다.
  - **Scramble**: 아직 안 나온 글자를 무작위 문자(Uppercase / Lowercase / Numerals / All)로 채웁니다. 원문의 공백은 유지해 줄바꿈이 튀지 않습니다.
- **Number (Count)**: 글자 안의 **첫 숫자**만 세어 바꾸고 나머지 글자는 그대로 둡니다(`21/100`, `Lv. 3`). 정수, 천 단위 쉼표(`1,234`). Start를 Current Value로 두면 지금 보이는 숫자에서 시작합니다. 끝 숫자를 실행 중에 정하려면 코드에서 `SetEndValue`([API](API.md#런타임에-끝-값-바꾸기))를 부르고, 창의 마지막 키 값은 미리보기용 값이 됩니다.
- **글자별 트랙** (Char Scale · Position · Rotation · Color · Alpha)
  - 키는 **글자 하나의 움직임**입니다.
  - **Stagger (s)**: i번째 글자가 i × stagger초 늦게 시작합니다. 타임라인의 마지막 키 뒤 연한 꼬리가 뒤 글자들이 재생되는 구간이고, 꼬리 끝을 끌면 Stagger가 바뀝니다.
  - **Duration per Char (s)**: 0보다 크면 i번째 글자가 키 전체를 i × 이 값(초) 동안 재생합니다.
  - 같은 텍스트의 글자 트랙 여러 개는 합쳐집니다(크기는 곱하고, 위치·회전은 더하고, 색·알파는 덮어씀).
- 텍스트 트랙이 있는 상태는 재생할 때 글자 수를 새로 읽습니다. text를 바꾼 뒤 재생하면 길이와 간격이 새 텍스트 기준입니다.

## 10. 이벤트와 사운드

**애니메이션 이벤트** (트랙 추가 창의 `Animation Events`)
- 트랙 머리의 **+** 또는 줄 더블클릭으로 이벤트를 추가하고, 드래그로 옮깁니다.
- 인스펙터: Name, Time, Type. Type은 `Name Only` 또는 코드에 `[KeyTweenEvent]`를 붙인 struct이고, struct 필드(float, int, bool, string, Vector2/3, Color, enum)를 바로 편집합니다.
- 받는 방법: 코드([API](API.md#애니메이션-이벤트)) 또는 `KeyTween Event Listener` 컴포넌트(이벤트 이름 → UnityEvent)

**사운드** (트랙 추가 창의 `Sound`)
- 트랙 머리의 **+**, 줄 더블클릭, 또는 AudioClip을 줄에 끌어다 놓아 추가합니다. 막대 길이가 클립 길이입니다.
- 인스펙터: Clip, Volume, Time, **▶ Preview**
- 재생 소스: 트랙의 AudioSource → `KeyTween`의 Audio Source → 둘 다 없으면 숨은 공용 2D AudioSource. UI 오브젝트마다 AudioSource를 붙일 필요가 없습니다.
- 상태 길이에 사운드 끝까지 포함되므로, 닫기 연출이 끝난 뒤 Destroy해도 소리가 끊기지 않습니다.

## 11. 여러 상태가 겹칠 때

여러 상태가 동시에 재생될 수 있습니다. 새 상태는 **자기가 움직이는 속성만** 이전 상태에서 그 자리에서 가져오고, 나머지는 계속 재생됩니다(호버 중에 누르기).

- 속성을 모두 잃은 상태는 멈추고, 그 `PlayAsync`는 취소됩니다.
- Start = Current Value 트랙은 멈춘 자리에서 이어집니다. 그래서 호버가 중간에 끊겨도 튀지 않습니다.
- 앞 상태를 끝 모습까지 맞추고 이벤트도 발생시킨 뒤 넘어가야 하면, 코드에서 `Complete("앞 상태")`를 먼저 부르고 재생합니다([API](API.md#재생)).

## 12. 자식 호출과 오버라이드 (중첩 Prefab)

공용 Prefab(예: 버튼)을 다른 Prefab(예: 팝업) 안에 넣었을 때, 팝업의 상태가 버튼의 상태를 부르고, 그 연출을 팝업 안에서만 바꿀 수 있습니다.

**자식 호출**
- 오브젝트 줄을 더블클릭하거나 우클릭 → **Add Child Call ▸ 자식 ▸ 상태**. 이미 호출 중인 상태는 체크되고 비활성입니다.
- 부모 시간축에 막대로 보이고, 드래그로 시간을 옮깁니다. 인스펙터: Child, State, Time
- 부모 상태 길이에 `호출 시간 + 자식 상태 길이`가 포함됩니다. 부모를 `Complete`하면 자식도 끝 값이 되고, 부모가 취소돼도 이미 시작한 자식은 계속 재생됩니다. 서로 부르는 순환은 깊이 8에서 끊습니다.

**오버라이드 — 현재 Prefab에서만, 트랙 단위**
- 자식 호출 아래에 자식 상태의 트랙이 호출 시간만큼 밀려서 보입니다. 원본 트랙은 흐리고 읽기 전용입니다.
- 흐린 트랙을 클릭하면 **그 트랙만** 오버라이드됩니다. 이후 편집은 오버라이드에 들어가고 원본 Prefab은 바뀌지 않습니다.
- 오버라이드는 바깥 Prefab 자신의 `KeyTween`에 저장됩니다. 바꾼 트랙만 저장하므로, 원본에서 다른 트랙을 고치면 팝업에도 반영됩니다.

| 원본 Prefab에서 | 오버라이드한 트랙 | 오버라이드 안 한 트랙 |
|---|---|---|
| 키 값·시간 수정 | 팝업 값 유지 | 팝업도 따라감 |
| 트랙 추가 | — | 팝업에도 생김 |
| 트랙 삭제 | 팝업에 남음 | 팝업에서도 사라짐 |

- 여러 단계(화면 > 팝업 > 버튼)가 같은 트랙을 오버라이드하면 **가장 바깥이 이깁니다**(Unity 중첩 Prefab 규칙과 같음). 오버라이드된 트랙은 파란 이름이고, 툴팁에 덮어쓴 Prefab이 나옵니다.
- 되돌리기: 트랙의 **×**(그 트랙의 가장 바깥 단계 하나), 자식 호출 제목의 **↺**(가장 바깥 단계의 오버라이드 전체, Unity의 Revert All). 씬 인스턴스에서 만든 오버라이드는 Inspector › Overrides › Revert도 됩니다.

## 13. 미리보기

**▶**로 재생, 눈금자를 끌어 스크럽합니다. 런타임과 같은 계산을 쓰므로 미리보기와 실제 재생 결과가 같고, 자식 호출과 오버라이드도 반영됩니다.

| 어디서 | 동작 |
|---|---|
| 씬 (편집 모드) | 바뀌는 속성을 Unity `AnimationMode`에 맡겨, 멈추면 원래 값으로 돌아가고 씬에 저장되지 않습니다. |
| Prefab 모드 | 시작할 때의 값을 기억했다가 멈추면 되돌립니다. Prefab이 저장될 때(Ctrl+S, Unity의 Prefab 모드 자동 저장)는 원래 값을 넣어 저장한 뒤 같은 시간을 다시 보여 줍니다. |
| Project 창에서 고른 Prefab | 원본은 어느 화면에도 그려지지 않으므로, 재생하거나 스크럽하면 그 Prefab을 Prefab 모드로 열고 같은 상태를 미리봅니다. |
| Play 모드 | 실행 중인 오브젝트에 값을 바로 쓰고, 멈추면 되돌립니다. |

- **시작 모습 자동 결정**: 다른 상태가 끝난 모습에서 시작해야 움직임이 보이는 상태는 그 모습에서 미리봅니다. Play On 짝(PointerExit←PointerEnter, PointerUp←PointerDown, Deselect←Select, ToggleOff←ToggleOn)과 이름 짝(Open↔Close, Show↔Hide, In↔Out)
- **반복**: **⟲** 또는 상태의 Loop. **속도**: 0.1x~1x
- **사운드**: 재생 중에 지나간 사운드를 들려줍니다. 스크럽할 때는 소리 없음
- **자동으로 멈추는 때**: ■, 대상·상태 탭 바꾸기, 상태 삭제, Play 모드 진입·종료, 스크립트 다시 불러오기, 씬 저장·닫기, 창 닫기

## 14. 저장 — Auto Save · Save

상단 바의 **Auto Save**(기본 켜짐)와 **Save**(창에 포커스가 있을 때 Ctrl+S, Mac Cmd+S)는 고친 내용을 그 `KeyTween`이 사는 곳에 저장합니다. 저장되는 곳은 두 컨트롤의 툴팁에 나옵니다.

| 편집하는 것 | 저장되는 곳 |
|---|---|
| Prefab 모드의 오브젝트 | 그 Prefab |
| Project 창에서 고른 Prefab 원본 | 그 Prefab |
| 씬 오브젝트 (편집 모드) | 두 컨트롤이 꺼지고 이유가 툴팁에 나옵니다. 고친 값은 오브젝트에 바로 들어가고, Unity 씬 저장(Ctrl+S)으로 저장됩니다. |
| Play 중, 실행 중에 만든 Prefab 복사본 (화면·목록 아이템) | 원본 Prefab. 같은 Prefab에서 나온 다른 실행 중 복사본도 바로 같은 값이 됩니다. |
| Play 중, 씬에 놓여 있던 오브젝트 | Play가 끝날 때 그 씬 오브젝트에 반영(Unity는 원래 버림). 씬 파일은 씬을 저장할 때 써집니다. |

- Auto Save는 마지막 수정 0.4초 뒤에 저장합니다(값을 끄는 동안 매 프레임 저장하지 않도록). Play를 끝낼 때 남은 것도 저장합니다. Ctrl+Z도 같은 방식으로 저장됩니다.
- Auto Save를 끄면 Save를 눌러야 저장됩니다. Play 중 Auto Save가 꺼진 채 Save하지 않은 수정은 Play가 끝나면 사라집니다.
- **주황 점**: 씬의 `*`처럼, 화면 값이 저장되지 않은 동안(미리보기 중, 저장 안 한 수정) 보입니다. 무엇인지는 점의 툴팁에 나옵니다.
- 중첩 Prefab의 부분을 Play 중 고치면 바깥 Prefab에 오버라이드로 저장되고, 안쪽 Prefab은 그대로입니다(§12).
- 한 번도 저장하지 않은 씬(Untitled)의 오브젝트는 Play가 끝난 뒤 다시 찾을 수 없어서 반영되지 않습니다.

## 15. 녹화 (●)

Unity Animation 창의 녹화와 같습니다. **●**를 켜고(버튼이 빨갛게 바뀜) 재생 막대를 원하는 시간에 둔 뒤, Scene 뷰나 Inspector에서 오브젝트를 바꾸면 그 값이 키가 됩니다.
- **트랙이 있는 속성**: 재생 막대 위치에 키를 넣습니다(그 시간에 키가 있으면 값만 바꿈). Relative To Rest·Size Relative 트랙은 그 기준에 맞춘 값으로 넣습니다. 0초에 녹화하면 첫 키가 그 값을 쓰도록 Start가 Key Value로 바뀝니다.
- **트랙이 없는 속성**: 트랙을 새로 만들고 0초 키(바꾸기 전 값)와 재생 막대 키(바꾼 값)를 넣습니다.
- **녹화되는 속성**: Scale, Rotation, Position, Size, Anchor Min/Max, Pivot, Active, Color(알파만 바꿨고 Alpha 트랙이 있으면 Alpha), Group Alpha, Fill Amount, Font Size, Character Spacing
- **녹화되지 않는 것**: Camera, Reveal·Number, 글자별 트랙, UIEffect, Pointer 시작 트랙, 창의 `KeyTween` 밖 오브젝트
- 오브젝트 자체는 바꾸기 전 값으로 돌아가고, 화면은 미리보기로 새 키를 보여 줍니다. 한 번의 변경이 Undo 한 번입니다.
- 녹화한 키도 Auto Save · Save로 저장됩니다. Play 중에도 녹화되고, 실행 중 복사본이면 원본 Prefab에 남습니다.

## 16. 우클릭 메뉴와 단축키

| 어디서 | 메뉴 |
|---|---|
| 타임라인 빈 곳 | Add Track..., Add Child Call ▸ 자식 ▸ 상태 |
| 키프레임 트랙 머리 | Add Key at Playhead, Remove Track (오버라이드 트랙: Revert Track to Original) |
| 키프레임 줄 | Add Key Here, Delete Selected Keys, Remove Track |
| 이벤트·사운드 트랙 머리 | Add Event / Sound at Playhead, Remove Track |
| 이벤트·사운드 마커 | Delete |
| 자식 호출 | Revert All (outermost overrides), Remove Child Call |

| 키 | 동작 |
|---|---|
| Ctrl+S (Cmd+S) | Save (편집 모드 씬 오브젝트면 Unity 씬 저장). `Edit > Shortcuts`의 `KeyTween/Save`에서 바꿀 수 있음 |
| Delete / Backspace | 고른 키·마커 삭제 |
| F | 전체 보기 |
| Esc | 우클릭 메뉴 닫기 |

모든 편집은 Unity Undo로 되돌릴 수 있습니다.

## 17. 지원하지 않는 것

- 키 복사·붙여넣기
- 상태 탭에서 바로 이름 바꾸기·복제·순서 바꾸기 (이름은 상태 인스펙터의 Name에서)
- `Time.timeScale` 반영 — 재생은 실시간 기준입니다(Design).
