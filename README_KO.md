# KeyTween

Unity uGUI용 키프레임 UI 애니메이션입니다. 타임라인에서 만들고, Prefab에 저장하고, 이름으로 재생합니다.

[![Unity 6000.0+](https://img.shields.io/badge/unity-6000.0%2B-000.svg)](https://unity.com/releases/editor/archive)
[![Release](https://img.shields.io/github/v/release/UnialGames/KeyTween)](https://github.com/UnialGames/KeyTween/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE.md)

[Document](https://unialgames.github.io/KeyTween/articles/ko/overview.html) · [English README](README.md)

<img src="Docs~/media/rpg/rpg-summon.gif" width="640" alt="RPG 샘플: 소환">

## 개요

KeyTween은 Animator 클립을 만들듯 타임라인에 키를 놓아 UI 애니메이션을 만듭니다. 애니메이션은 **Prefab 안에** `Hover`, `Open`, `Close` 같은 이름의 상태로 저장되므로, 클립이나 컨트롤러 에셋이 따로 없습니다.

Play 모드에 들어가지 않고 미리 보고, 상태를 언제 재생할지 코드 없이 정합니다. 코드가 필요하면 한 줄로 상태를 재생하고 끝날 때까지 기다립니다.

영상: [RPG 샘플](https://youtu.be/79Aw-O2kEgA) · [에디터에서 버튼 호버 만들기](https://youtu.be/13JJNE4_FwI) · [팝업 안의 버튼만 바꾸기](https://youtu.be/rXO_kNexiGc)

## 문서

* [설치](https://unialgames.github.io/KeyTween/articles/ko/installation.html): 요구 사항, Package Manager 설치, 버전 고정, 샘플 가져오기
* [빠른 시작](https://unialgames.github.io/KeyTween/articles/ko/quick-start.html): 에디터에서 첫 호버 연출 만들기, 코드에서 재생하기
* [에디터](https://unialgames.github.io/KeyTween/articles/ko/concepts.html): KeyTween 창, 상태·트랙·키, Play On, 중첩 Prefab, 미리보기, 저장, 녹화
* [코드에서 쓰기](https://unialgames.github.io/KeyTween/articles/ko/playing-and-awaiting.html): 상태 재생과 기다리기, 실행 중 끝 값, 애니메이션 이벤트
* [스크롤 리스트](https://unialgames.github.io/KeyTween/articles/ko/scroll-list.html): 아이템 연출과 도우미 컴포넌트가 있는 가상화 목록
* [화면 전환](https://unialgames.github.io/KeyTween/articles/ko/screens.html): 페이지, 모달, 시트 컨테이너, 생명주기, 공유 요소
* [연동](https://unialgames.github.io/KeyTween/articles/ko/unitask.html): UniTask, R3, UIEffect
* [성능](https://unialgames.github.io/KeyTween/articles/ko/performance.html): 다른 도구와의 측정 비교, 사용할 때 알아 둘 점
* [API 레퍼런스](https://unialgames.github.io/KeyTween/api/KeyTweening.html): 코드의 XML 주석으로 만든 모든 공개 형식과 멤버

## 기능

* 타임라인 에디터 창, 트랙 추가 창, 키 인스펙터, 녹화
* 편집 모드, Prefab 모드, Play 모드에서의 미리보기
* Prefab에 저장되는 상태, Prefab의 컴포넌트를 직접 가리키는 트랙
* 코드 없이 재생하는 Play On (포인터 진입, 클릭, 활성화 등)
* `await anim.PlayAsync("Open")` 한 줄 재생 (Unity `Awaitable`)과 끝나지 못한 재생의 처리 규칙
* 중첩 Prefab: 자식 Prefab의 상태 호출, 바깥 Prefab 안에서만 적용되는 트랙 덮어쓰기
* TextMeshPro 텍스트 효과: 타자기, 숫자 세기, 글자별 트랙
* 스크롤 리스트, 화면 전환(페이지, 모달, 시트) 컴포넌트
* 선택 연동: UniTask, R3, UIEffect
* 쉴 때 비용 0, 재생 중 GC 할당 0, Burst로 계산하는 이징 트랙

## 설치

KeyTween을 프로젝트에 설치하는 방법을 설명합니다.

### 요구 사항

* Unity 6000.0 이상
* 함께 설치되는 패키지
  * `com.unity.ugui` 2.0.0 (TextMeshPro 포함)
  * `com.unity.mathematics` 1.3.2
  * `com.unity.burst` 1.8.25

### Package Manager로 설치 (권장)

Package Manager에서 Git 주소로 설치합니다.

1. **Window > Package Manager**를 엽니다.
2. **+** 버튼을 누르고 **Add package from git URL...** 항목을 고릅니다.
3. 다음 주소를 입력합니다.

```text
https://github.com/UnialGames/KeyTween.git
```

`Packages/manifest.json`의 `dependencies`에 직접 추가할 수도 있습니다.

```json
{
  "dependencies": {
    "com.unialgames.keytween": "https://github.com/UnialGames/KeyTween.git"
  }
}
```

### 업데이트

위 주소는 항상 최신 릴리스를 가리킵니다. 새 버전이 나오면 Package Manager에서 KeyTween을 고르고 **Update**를 누릅니다.

### 특정 버전으로 설치하기

주소 뒤에 `#`과 버전 태그를 붙이면 그 버전에 고정됩니다. 버전 목록은 [Releases](https://github.com/UnialGames/KeyTween/releases)에 있습니다.

```text
https://github.com/UnialGames/KeyTween.git#v0.1.3
```

* 고정한 버전은 **Update**를 눌러도 바뀌지 않습니다.
* 버전을 바꾸려면 `Packages/manifest.json`의 태그를 고칩니다.
* 최신 릴리스를 다시 따라가려면 주소에서 `#v…`를 지웁니다.

### 샘플 가져오기

1. Package Manager에서 KeyTween을 고릅니다.
2. **Samples** 탭에서 원하는 샘플의 **Import**를 누릅니다.

샘플은 프로젝트 안으로 복사됩니다. 패키지를 업데이트한 뒤에는 샘플을 다시 Import해야 새 버전이 반영됩니다.

## 시작하기

*상태*는 `Hover`처럼 이름이 붙은 애니메이션이며, Prefab의 `KeyTween` 컴포넌트에 저장됩니다.

### 에디터에서 호버 연출 만들기

포인터가 올라가면 버튼이 1.1배로 커지고 밝아지는 연출을 코드 없이 만듭니다.

1. 버튼 Prefab을 열고 **Window > KeyTween Editor**를 엽니다.
2. **Add KeyTween to 'Button'** 버튼을 누릅니다.
3. 상태 탭의 **+** 버튼을 누르고 새 상태의 이름을 `Hover`로 정합니다.
4. 타임라인을 우클릭해 **Add Track...** 항목을 고르고, **Scale**과 **Color**를 골라 **Add 2** 버튼을 누릅니다.
5. Scale의 끝 키를 `1.1`로, Color의 끝 키를 밝은 색으로 정합니다.
6. 눈금자를 끌거나 **▶** 버튼을 눌러 미리 봅니다.
7. 상태의 **Play On**을 `PointerEnter`로 정합니다.

![Hover와 Unhover 상태가 있는 KeyTween 창](Docs~/media/editor/editor-window.png)

단계별 자세한 내용은 [트랙 추가](https://unialgames.github.io/KeyTween/articles/ko/adding-tracks.html), [키 편집](https://unialgames.github.io/KeyTween/articles/ko/editing-keys.html), [Play On](https://unialgames.github.io/KeyTween/articles/ko/play-on.html)을 참고하세요.

### 코드에서 재생하기

`KeyTween` 컴포넌트를 참조하고 상태 이름으로 재생합니다.

```csharp
using System;
using KeyTweening;
using UnityEngine;

public class Popup : MonoBehaviour
{
    [SerializeField] KeyTween anim;

    public async Awaitable Open() => await anim.PlayAsync("Open");

    public async Awaitable Close()
    {
        try { await anim.PlayAsync("Close", cancellationToken: destroyCancellationToken); }
        catch (OperationCanceledException) { return; }   // 닫는 중에 다시 열림: 팝업 유지
        Destroy(gameObject);
    }
}
```

자세한 내용은 [재생과 기다리기](https://unialgames.github.io/KeyTween/articles/ko/playing-and-awaiting.html)를 참고하세요.

## 샘플

Package Manager에서 KeyTween의 **Samples** 탭으로 가져옵니다. 두 샘플 모두 Input System이 필요합니다.

* **RPG Sample**: 완성된 모바일 RPG UI. 모든 연출이 Prefab의 상태입니다.
* **UI Components**: 버튼, 호버, 토글, 팝업, 텍스트 효과, 스크롤 리스트 예제 18가지

| 영웅 → 상세 → 강화 | 모험 → 전투 → 승리 |
|---|---|
| ![영웅](Docs~/media/rpg/rpg-heroes.gif) | ![전투](Docs~/media/rpg/rpg-battle.gif) |
| **랭킹: 긴 목록** | **우편과 출석 보상** |
| ![랭킹](Docs~/media/rpg/rpg-ranking.gif) | ![우편](Docs~/media/rpg/rpg-mail.gif) |

## 성능

IL2CPP 릴리스 빌드, 오브젝트 2,000개. 괄호 안은 같은 값을 스크립트로 직접 넣을 때보다 더 든 시간입니다.

| | KeyTween | DOTween Pro | LitMotion | Animator | AnimationUI |
|---|---:|---:|---:|---:|---:|
| 맥박 반복 (ms) | 7.69 (+0.38) | 7.71 (+0.40) | 7.66 (+0.34) | 10.16 (+2.85) | 9.31 (+2.00) |
| 목록 등장 (ms) | 1.70 (+0.04) | 1.59 (−0.06) | 1.53 (−0.13) | 5.23 (+3.57) | 2.18 (+0.52) |
| 글자 물결 100개 (ms) | **0.61** | 2.11 | 2.05 | 지원 안 함 | 지원 안 함 |

자세한 내용은 [성능](https://unialgames.github.io/KeyTween/articles/ko/performance.html)을 참고하세요.

## 라이선스

* KeyTween: [MIT 라이선스](LICENSE.md)
* RPG 샘플 아이콘: [game-icons.net](https://game-icons.net), CC BY 3.0 (작성자는 `Samples/RPG/Art/CREDITS.md`)
* 샘플 폰트: Passion One, SIL OFL 1.1
