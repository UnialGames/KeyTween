# UI Components

자주 쓰는 UI 연출을 KeyTween으로 만든 샘플입니다. 버튼·호버·토글·팝업·글자 효과와 스크롤 리스트 18종이 있고, 연출은 모두 각 Prefab의 **KeyTween 상태**입니다. Prefab을 열고 KeyTween 창(`Window > KeyTween Editor`)에서 상태를 보거나 고칠 수 있습니다.

- 장면: `Scenes/`의 씬 6개 (1920×1080 기준). 씬을 열고 Play를 누릅니다.
- 필요: **Input System** 패키지(장면의 EventSystem이 `InputSystemUIInputModule`), TextMeshPro(uGUI 2.x에 포함)
- 스크립트(`Scripts/`)에는 asmdef가 없어 프로젝트의 기본 어셈블리로 컴파일됩니다.
- 글꼴: Montserrat SemiBold (`Fonts/Montserrat/OFL.txt`, SIL Open Font License)

| 씬 | 내용 |
|---|---|
| `Buttons` | Button1(색 호버, 누르면 크기·글자 축소), Button2(오버레이 호버, 누른 자리에서 원이 퍼지는 리플: Start = Pointer) |
| `Hover` | 호버 6종 |
| `Toggles` | Toggle1(체크 → 0.025초 뒤 채우기), Toggle2(손잡이 이동 + 채우기) |
| `Text` | TextDemo: Typewriter, Scramble, Flip, PopUp, Punch, Shake (글자 하나하나 움직이는 트랙) |
| `Popup` | Popup1(+ 중첩된 Button1): Open(이벤트 `Shown`, 사운드, 닫기 버튼 상태 호출, 호버를 이 팝업에서만 2배 빠르게 오버라이드) / Close. `Close>Open` 버튼은 닫는 중 다시 열어 취소를 재현 |
| `Scroll` | `KeyTweenScrollList` 형태 18가지(리스트·그리드·메이슨리·캐러셀·무한 루프·페이지·배너·휠 피커·섹션 헤더·채팅·아코디언 등)를 한 장씩 넘겨 봄. 위쪽 막대로 아이템 수(0~200)와 추가·삭제, **Edit Animation**으로 그 페이지의 상태를 KeyTween 창에서 열기 |

Buttons·Text·Toggle은 [UGUIAnimationSamples](https://github.com/annulusgames/UGUIAnimationSamples)를 같은 시간·이징으로 다시 만든 것입니다.
