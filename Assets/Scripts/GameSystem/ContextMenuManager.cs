using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ContextMenuManager : MonoBehaviour
{
    [Header("컨텍스트 메뉴 UI")]
    public Canvas canvas;
    public GameObject menuPrefab;
    public GameObject buttonPrefab;
    public GameObject separatorPrefab;

    [Header("폰트 설정")]
    public TMP_FontAsset dungGeunMoFont;

    [Header("메뉴 설정")]
    public Vector2 menuSize = new Vector2(120f, 180f);
    public float fadeSpeed = 6f;
    public float buttonHeight = 24f;
    public float separatorHeight = 6f;

    [Header("오브젝트 기준 메뉴 위치 설정")]
    public Vector2 objectMenuOffset = new Vector2(100f, 0f); // 오브젝트 우측으로 100 픽셀로 줄임
    public bool useObjectPosition = true; // 오브젝트 위치 기준 사용 여부

    private Camera mainCamera;
    private GameObject currentMenu;
    private bool isMenuVisible = false;

    // 메뉴 아이템 데이터 구조
    [System.Serializable]
    public class ContextMenuItem
    {
        public enum ItemType { Button, Separator }
        public ItemType itemType;
        public string itemText;
        public Sprite itemIcon;
        public System.Action onClick;
    }

    // 싱글톤
    public static ContextMenuManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        mainCamera = Camera.main;

        if (canvas == null)
            canvas = FindObjectOfType<Canvas>();

        // DungGeunMo 폰트 자동 로드 시도
        LoadDungGeunMoFont();

        // 프리팹들이 없으면 런타임에 생성
        CreatePrefabsIfNeeded();

        Debug.Log("Unity UI 컨텍스트 메뉴 시스템 초기화 완료");
        DebugLogger.LogToFile("Unity UI 컨텍스트 메뉴 시스템 초기화 완료");
    }
    void LoadDungGeunMoFont()
    {
        // Inspector에서 할당되었는지 확인
        if (dungGeunMoFont != null)
        {
            Debug.Log($"DungGeunMo 폰트 로드 성공: {dungGeunMoFont.name}");
            DebugLogger.LogToFile($"DungGeunMo 폰트 로드 성공: {dungGeunMoFont.name}");
            return;
        }

        // Resources 폴더에서 찾기 (빌드에서도 작동) - 최우선
        Debug.Log("Resources 폴더에서 DungGeunMo 폰트 검색 중...");

        // 정확한 경로로 로드 시도
        string[] resourcePaths = {
            "Font/DungGeunMo SDF",    // Assets/Resources/Font/DungGeunMo SDF.asset
            "Font/DungGeunMo",        // 혹시 다른 이름으로 저장된 경우
            "DungGeunMo SDF",         // Resources 루트에 있는 경우
            "DungGeunMo"              // 기본 이름
        };

        foreach (string path in resourcePaths)
        {
            try
            {
                TMP_FontAsset font = Resources.Load<TMP_FontAsset>(path);
                if (font != null)
                {
                    dungGeunMoFont = font;
                    Debug.Log($"DungGeunMo 폰트 Resources에서 로드 성공: {path}");
                    DebugLogger.LogToFile($"DungGeunMo 폰트 Resources에서 로드 성공: {path}");
                    return;
                }
                else
                {
                    Debug.Log($"Resources.Load 실패: {path} - null 반환");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Resources.Load 예외 발생: {path} - {e.Message}");
            }
        }

#if UNITY_EDITOR
        // 에디터에서만 자동 찾기
        Debug.Log("에디터 모드: AssetDatabase로 폰트 검색 중...");

        string[] fontPaths = {
            "Assets/Resources/Font/DungGeunMo SDF",
            "Assets/Font/DungGeunMo SDF",
            "Assets/Fonts/DungGeunMo SDF"
        };

        foreach (string path in fontPaths)
        {
            try
            {
                TMP_FontAsset font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path + ".asset");
                if (font != null)
                {
                    dungGeunMoFont = font;
                    Debug.Log($"DungGeunMo 폰트 AssetDatabase에서 로드 성공: {path}");
                    DebugLogger.LogToFile($"DungGeunMo 폰트 AssetDatabase에서 로드 성공: {path}");
                    return;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"AssetDatabase.LoadAssetAtPath 예외: {path} - {e.Message}");
            }
        }

        // GUID로 검색
        try
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("DungGeunMo t:TMP_FontAsset");
            Debug.Log($"GUID 검색 결과: {guids.Length}개 발견");

            if (guids.Length > 0)
            {
                string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                TMP_FontAsset font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
                if (font != null)
                {
                    dungGeunMoFont = font;
                    Debug.Log($"DungGeunMo 폰트 GUID 검색으로 로드 성공: {assetPath}");
                    DebugLogger.LogToFile($"DungGeunMo 폰트 GUID 검색으로 로드 성공: {assetPath}");
                    return;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"GUID 검색 예외: {e.Message}");
        }
#endif

        // 모든 시도가 실패한 경우 - 경고만 출력하고 기본 폰트 사용
        Debug.LogWarning("DungGeunMo 폰트를 찾을 수 없습니다. 기본 TextMeshPro 폰트를 사용합니다.");
        DebugLogger.LogToFile("DungGeunMo 폰트를 찾을 수 없습니다. 기본 TextMeshPro 폰트를 사용합니다.");

        // 기본 TextMeshPro 폰트 사용 시도
        try
        {
            // TextMeshPro 기본 폰트들 시도
            string[] defaultFonts = {
                "Fonts & Materials/LiberationSans SDF",
                "LiberationSans SDF",
                "Arial SDF"
            };

            foreach (string defaultFont in defaultFonts)
            {
                TMP_FontAsset font = Resources.Load<TMP_FontAsset>(defaultFont);
                if (font != null)
                {
                    dungGeunMoFont = font;
                    Debug.Log($"기본 폰트 사용: {defaultFont}");
                    DebugLogger.LogToFile($"기본 폰트 사용: {defaultFont}");
                    return;
                }
            }

            // 마지막 수단: TMP의 기본 폰트
            dungGeunMoFont = TMP_Settings.defaultFontAsset;
            if (dungGeunMoFont != null)
            {
                Debug.Log("TMP 기본 폰트 사용");
                DebugLogger.LogToFile("TMP 기본 폰트 사용");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"기본 폰트 로드 실패: {e.Message}");
            // dungGeunMoFont는 null로 남겨두고, ApplyFont에서 처리
        }
    }

    // 폰트 적용 메서드 (안전성 개선)
    void ApplyFont(TextMeshProUGUI textComponent)
    {
        if (textComponent == null)
        {
            Debug.LogWarning("TextMeshProUGUI 컴포넌트가 null입니다.");
            return;
        }

        if (dungGeunMoFont != null)
        {
            try
            {
                textComponent.font = dungGeunMoFont;
                Debug.Log($"폰트 적용 완료: {dungGeunMoFont.name}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"폰트 적용 실패: {e.Message}");
                // 기본 폰트 사용
                if (textComponent.font == null)
                {
                    textComponent.font = TMP_Settings.defaultFontAsset;
                }
            }
        }
        else
        {
            Debug.LogWarning("DungGeunMo 폰트가 없어서 기본 폰트 사용");
            // 기본 폰트가 설정되어 있지 않은 경우에만 설정
            if (textComponent.font == null)
            {
                textComponent.font = TMP_Settings.defaultFontAsset;
            }
        }
    }

    void CreatePrefabsIfNeeded()
    {
        Debug.Log("CreatePrefabsIfNeeded 시작");

        if (menuPrefab == null)
        {
            Debug.Log("MenuPrefab 생성 중...");
            menuPrefab = CreateMenuPrefab();
            Debug.Log($"MenuPrefab 생성 완료: {(menuPrefab != null ? "성공" : "실패")}");
        }

        if (buttonPrefab == null)
        {
            Debug.Log("ButtonPrefab 생성 중...");
            buttonPrefab = CreateButtonPrefab();
            Debug.Log($"ButtonPrefab 생성 완료: {(buttonPrefab != null ? "성공" : "실패")}");
        }

        if (separatorPrefab == null)
        {
            Debug.Log("SeparatorPrefab 생성 중...");
            separatorPrefab = CreateSeparatorPrefab();
            Debug.Log($"SeparatorPrefab 생성 완료: {(separatorPrefab != null ? "성공" : "실패")}");
        }

        Debug.Log("CreatePrefabsIfNeeded 완료");
    }

    GameObject CreateMenuPrefab()
    {
        // 메뉴 패널 생성
        GameObject menu = new GameObject("ContextMenu");
        menu.transform.SetParent(canvas.transform, false);

        // 컴포넌트 추가
        RectTransform rectTransform = menu.AddComponent<RectTransform>();
        Image background = menu.AddComponent<Image>();
        CanvasGroup canvasGroup = menu.AddComponent<CanvasGroup>();
        VerticalLayoutGroup layoutGroup = menu.AddComponent<VerticalLayoutGroup>();
        ContentSizeFitter sizeFitter = menu.AddComponent<ContentSizeFitter>();

        // 설정 (픽셀 게임 스타일)
        rectTransform.sizeDelta = menuSize;
        background.color = new Color(217 / 255f, 174 / 255f, 160 / 255f, 0.95f); // 분홍색

        // 레이아웃 설정
        layoutGroup.childAlignment = TextAnchor.UpperCenter;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.spacing = 1f;
        layoutGroup.padding = new RectOffset(8, 8, 8, 8);

        // 크기 자동 조정
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 픽셀 게임 스타일 테두리
        var outline = menu.AddComponent<Outline>();
        outline.effectColor = new Color(0.2f, 0.4f, 0.8f, 1f);
        outline.effectDistance = new Vector2(1, -1);

        menu.SetActive(false);
        return menu;
    }

    GameObject CreateButtonPrefab()
    {
        // 버튼 생성
        GameObject button = new GameObject("MenuButton");

        // 컴포넌트 추가
        RectTransform rectTransform = button.AddComponent<RectTransform>();
        Image background = button.AddComponent<Image>();
        Button buttonComponent = button.AddComponent<Button>();
        LayoutElement layoutElement = button.AddComponent<LayoutElement>();

        // 설정 (픽셀 게임 스타일)
        rectTransform.sizeDelta = new Vector2(0, buttonHeight);
        background.color = new Color(0.15f, 0.15f, 0.25f, 1f);
        layoutElement.minHeight = buttonHeight;
        layoutElement.preferredHeight = buttonHeight;

        // 버튼 색상 설정 (픽셀 게임 스타일)
        ColorBlock colors = buttonComponent.colors;
        colors.normalColor = new Color(0.15f, 0.15f, 0.25f, 1f);
        colors.highlightedColor = new Color(0.2f, 0.3f, 0.5f, 1f);
        colors.pressedColor = new Color(0.1f, 0.1f, 0.2f, 1f);
        colors.selectedColor = new Color(0.18f, 0.25f, 0.4f, 1f);
        buttonComponent.colors = colors;

        // 텍스트 생성
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(button.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();

        // 텍스트 설정
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8, 0); // 좌우 여백 줄임
        textRect.offsetMax = new Vector2(-8, 0);

        text.text = "메뉴 아이템";
        text.fontSize = 10f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;

        // DungGeunMo 폰트 적용 시도
        ApplyFont(text);

        return button;
    }

    GameObject CreateSeparatorPrefab()
    {
        // 구분선 컨테이너 생성
        GameObject separator = new GameObject("Separator");

        // 컴포넌트 추가
        RectTransform rectTransform = separator.AddComponent<RectTransform>();
        LayoutElement layoutElement = separator.AddComponent<LayoutElement>();

        // 컨테이너 설정
        rectTransform.sizeDelta = new Vector2(0, separatorHeight);
        layoutElement.minHeight = separatorHeight;
        layoutElement.preferredHeight = separatorHeight;

        // 실제 구분선 (얇은 라인)
        GameObject line = new GameObject("Line");
        line.transform.SetParent(separator.transform, false);

        RectTransform lineRect = line.AddComponent<RectTransform>();
        Image lineImage = line.AddComponent<Image>();

        // 라인 설정 (픽셀 게임 스타일)
        lineRect.anchorMin = new Vector2(0.1f, 0.5f);
        lineRect.anchorMax = new Vector2(0.9f, 0.5f);
        lineRect.sizeDelta = new Vector2(0, 2f);
        lineImage.color = new Color(0.4f, 0.6f, 1f, 0.8f);

        return separator;
    }


    void Update()
    {
        // ESC 키로 메뉴 닫기
        if (Input.GetKeyDown(KeyCode.Escape) && isMenuVisible)
        {
            Debug.Log("ESC 키로 메뉴 닫기");
            HideMenu();
        }

        // 좌클릭으로 메뉴 닫기 (메뉴 영역 외부 클릭 시)
        if (Input.GetMouseButtonDown(0) && isMenuVisible)
        {
            if (!IsMouseOverMenu())
            {
                Debug.Log("메뉴 외부 클릭으로 메뉴 닫기");
                HideMenu();
            }
        }

        // 우클릭 시에도 메뉴 닫기 (새 메뉴가 열리기 전에)
        if (Input.GetMouseButtonDown(1) && isMenuVisible)
        {
            Debug.Log("우클릭으로 이전 메뉴 닫기");
            HideMenu();
        }
    }

    bool IsMouseOverMenu()
    {
        if (currentMenu == null || !isMenuVisible)
        {
            Debug.Log("IsMouseOverMenu: 메뉴가 없거나 보이지 않음");
            return false;
        }

        RectTransform menuRect = currentMenu.GetComponent<RectTransform>();
        if (menuRect == null)
        {
            Debug.LogWarning("IsMouseOverMenu: RectTransform을 찾을 수 없음");
            return false;
        }

        bool isOver = RectTransformUtility.RectangleContainsScreenPoint(menuRect, Input.mousePosition, canvas.worldCamera);
        Debug.Log($"IsMouseOverMenu: {isOver} (마우스: {Input.mousePosition})");
        return isOver;
    }

    // 오브젝트 위치 기준으로 우측에 메뉴 표시하는 메서드 추가
    public void ShowCatMenu(Vector3 objectWorldPosition)
    {
        Debug.Log($"ShowCatMenu 호출됨! 오브젝트 위치: {objectWorldPosition}");
        DebugLogger.LogToFile($"ShowCatMenu 호출됨! 오브젝트 위치: {objectWorldPosition}");

        var catMenuItems = new List<ContextMenuItem>
        {
            new ContextMenuItem { itemType = ContextMenuItem.ItemType.Button, itemText = "고양이 상태보기", onClick = OnCatStatusClicked },
            new ContextMenuItem { itemType = ContextMenuItem.ItemType.Separator },
            new ContextMenuItem { itemType = ContextMenuItem.ItemType.Button, itemText = "먹이주기 (1 츄르)", onClick = OnFeedCatClicked },
            new ContextMenuItem { itemType = ContextMenuItem.ItemType.Button, itemText = "쓰다듬기", onClick = OnPetCatClicked }
        };

        // 오브젝트 기준 위치 계산
        Vector3 menuWorldPosition = CalculateMenuPosition(objectWorldPosition);
        ShowMenu(menuWorldPosition, catMenuItems);

        Debug.Log("고양이 컨텍스트 메뉴 표시 완료");
        DebugLogger.LogToFile("고양이 컨텍스트 메뉴 표시 완료");
    }

    public void ShowTowerMenu(Vector3 objectWorldPosition)
    {
        Debug.Log($"ShowTowerMenu 호출됨! 오브젝트 위치: {objectWorldPosition}");
        DebugLogger.LogToFile($"ShowTowerMenu 호출됨! 오브젝트 위치: {objectWorldPosition}");

        var towerMenuItems = new List<ContextMenuItem>
        {
            new ContextMenuItem { itemType = ContextMenuItem.ItemType.Button, itemText = "타워 정보", onClick = OnTowerInfoClicked },
            new ContextMenuItem { itemType = ContextMenuItem.ItemType.Separator },
            new ContextMenuItem { itemType = ContextMenuItem.ItemType.Button, itemText = "업그레이드", onClick = OnUpgradeClicked },
            new ContextMenuItem { itemType = ContextMenuItem.ItemType.Separator },
            new ContextMenuItem { itemType = ContextMenuItem.ItemType.Button, itemText = "츄르 수집", onClick = OnCollectClicked },
            new ContextMenuItem { itemType = ContextMenuItem.ItemType.Button, itemText = "생산 정보", onClick = OnTowerInfoClicked }
        };

        // 오브젝트 기준 위치 계산
        Vector3 menuWorldPosition = CalculateMenuPosition(objectWorldPosition);
        ShowMenu(menuWorldPosition, towerMenuItems);

        Debug.Log("타워 컨텍스트 메뉴 표시 완료");
        DebugLogger.LogToFile("타워 컨텍스트 메뉴 표시 완료");
    }

    // 오브젝트 위치 기준으로 메뉴 위치 계산
    Vector3 CalculateMenuPosition(Vector3 objectWorldPosition)
    {
        if (!useObjectPosition)
        {
            // 기존 방식 (마우스 위치)
            return objectWorldPosition;
        }

        // 오브젝트를 스크린 좌표로 변환
        Vector3 objectScreenPos = mainCamera.WorldToScreenPoint(objectWorldPosition);

        // 화면 좌표에서 오프셋 적용 (오브젝트 우측으로)
        Vector3 menuScreenPos = objectScreenPos + new Vector3(objectMenuOffset.x, objectMenuOffset.y, 0);

        // 화면 경계 체크
        menuScreenPos.x = Mathf.Clamp(menuScreenPos.x, menuSize.x * 0.5f, Screen.width - menuSize.x * 0.5f);
        menuScreenPos.y = Mathf.Clamp(menuScreenPos.y, menuSize.y * 0.5f, Screen.height - menuSize.y * 0.5f);

        // 다시 월드 좌표로 변환
        Vector3 menuWorldPos = mainCamera.ScreenToWorldPoint(menuScreenPos);

        Debug.Log($"오브젝트 위치: {objectWorldPosition} → 메뉴 위치: {menuWorldPos}");
        Debug.Log($"스크린 좌표: 오브젝트 {objectScreenPos} → 메뉴 {menuScreenPos}");

        return menuWorldPos;
    }

    void ShowMenu(Vector3 worldPosition, List<ContextMenuItem> menuItems)
    {
        Debug.Log($"ShowMenu 시작 - 현재 메뉴 상태: {(currentMenu != null ? "있음" : "없음")}, 메뉴 보임: {isMenuVisible}");

        // 기존 메뉴 완전히 정리
        ForceCleanupMenu();

        // 메뉴 표시 중에는 click-through 비활성화
        if (CompatibilityWindowManager.Instance != null)
        {
            CompatibilityWindowManager.Instance.DisableClickThrough();
            Debug.Log("컨텍스트 메뉴 표시로 인한 click-through 비활성화");
        }

        if (canvas == null)
        {
            Debug.LogError("Canvas가 없습니다!");
            return;
        }

        if (menuPrefab == null)
        {
            Debug.LogError("MenuPrefab이 없습니다! 새로 생성합니다.");
            CreatePrefabsIfNeeded();
            if (menuPrefab == null)
            {
                Debug.LogError("MenuPrefab 생성 실패!");
                return;
            }
        }

        // 새 메뉴 생성
        currentMenu = Instantiate(menuPrefab, canvas.transform);
        currentMenu.name = "ContextMenu_Active";
        Debug.Log($"새 메뉴 생성 완료: {currentMenu.name}");

        // 월드 좌표를 스크린 좌표로 변환
        Vector3 screenPosition = mainCamera.WorldToScreenPoint(worldPosition);
        Debug.Log($"월드 좌표 {worldPosition} → 스크린 좌표 {screenPosition}");

        // UI 좌표로 변환
        Vector2 uiPosition;
        bool converted = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            screenPosition,
            canvas.worldCamera,
            out uiPosition
        );

        Debug.Log($"스크린 → UI 좌표 변환: {converted}, 결과: {uiPosition}");

        // 메뉴 위치 설정
        RectTransform menuRect = currentMenu.GetComponent<RectTransform>();
        menuRect.localPosition = uiPosition;
        Debug.Log($"메뉴 위치 설정 완료: {menuRect.localPosition}");

        // 화면 경계 체크 및 조정 (UI 좌표계에서)
        ClampMenuToScreen(menuRect);

        // 메뉴 아이템들 생성
        Debug.Log($"메뉴 아이템 생성 시작 - 개수: {menuItems.Count}");
        CreateMenuItems(menuItems);

        // 메뉴 표시
        currentMenu.SetActive(true);
        isMenuVisible = true;
        Debug.Log("메뉴 활성화 완료 - ShowMenu 완료!");
    }

    void ForceCleanupMenu()
    {
        Debug.Log("ForceCleanupMenu 시작");

        // 모든 코루틴 정지
        StopAllCoroutines();

        // 기존 메뉴 찾아서 모두 제거
        GameObject[] existingMenus = GameObject.FindGameObjectsWithTag("Untagged");
        foreach (GameObject obj in existingMenus)
        {
            if (obj.name.Contains("ContextMenu"))
            {
                Debug.Log($"기존 메뉴 발견하여 제거: {obj.name}");
                Destroy(obj);
            }
        }

        // currentMenu 강제 정리
        if (currentMenu != null)
        {
            Debug.Log($"currentMenu 강제 제거: {currentMenu.name}");
            Destroy(currentMenu);
        }

        currentMenu = null;
        isMenuVisible = false;

        // 강제 정리 시에도 click-through 상태 복원
        RestoreClickThroughState();

        Debug.Log("ForceCleanupMenu 완료");
    }

    void CreateMenuItems(List<ContextMenuItem> menuItems)
    {
        Debug.Log($"CreateMenuItems 시작 - 아이템 개수: {menuItems.Count}");

        // 동적으로 메뉴 아이템 텍스트 업데이트
        UpdateMenuItemTexts(menuItems);

        int itemIndex = 0;
        foreach (var item in menuItems)
        {
            Debug.Log($"아이템 {itemIndex} 생성 중 - 타입: {item.itemType}, 텍스트: {item.itemText}");

            GameObject menuItem;

            if (item.itemType == ContextMenuItem.ItemType.Button)
            {
                if (buttonPrefab == null)
                {
                    Debug.LogError("ButtonPrefab이 null입니다!");
                    continue;
                }

                menuItem = Instantiate(buttonPrefab, currentMenu.transform);
                Debug.Log($"버튼 아이템 생성 완료: {menuItem.name}");

                // 텍스트 설정
                TextMeshProUGUI text = menuItem.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null)
                {
                    text.text = item.itemText;
                    Debug.Log($"텍스트 설정 완료: {item.itemText}");

                    // DungGeunMo 폰트 적용
                    ApplyFont(text);
                }
                else
                {
                    Debug.LogError("TextMeshProUGUI 컴포넌트를 찾을 수 없습니다!");
                }

                // 클릭 이벤트 연결
                Button button = menuItem.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    if (item.onClick != null)
                    {
                        button.onClick.AddListener(() => {
                            Debug.Log($"버튼 클릭됨: {item.itemText}");
                            item.onClick.Invoke();
                            HideMenu();
                        });
                        Debug.Log($"클릭 이벤트 연결 완료: {item.itemText}");
                    }
                    else
                    {
                        Debug.LogWarning($"onClick이 null입니다: {item.itemText}");
                    }
                }
                else
                {
                    Debug.LogError("Button 컴포넌트를 찾을 수 없습니다!");
                }
            }
            else // Separator
            {
                if (separatorPrefab == null)
                {
                    Debug.LogError("SeparatorPrefab이 null입니다!");
                    continue;
                }

                menuItem = Instantiate(separatorPrefab, currentMenu.transform);
                Debug.Log($"구분선 생성 완료: {menuItem.name}");
            }

            itemIndex++;
        }

        Debug.Log("CreateMenuItems 완료!");
    }

    void UpdateMenuItemTexts(List<ContextMenuItem> menuItems)
    {
        // 실시간 데이터로 텍스트 업데이트
        foreach (var item in menuItems)
        {
            if (item.itemType == ContextMenuItem.ItemType.Button)
            {
                // 고양이 상태 업데이트
                if (item.itemText.Contains("고양이 상태") && GameDataManager.Instance != null)
                {
                    item.itemText = $"상태: {GameDataManager.Instance.HappinessStatus} ({GameDataManager.Instance.Happiness:F1}%)";
                }
                // 먹이주기 업데이트
                else if (item.itemText.Contains("먹이주기") && CatTower.Instance != null)
                {
                    bool canFeed = CatTower.Instance.ChurCount >= 1;
                    item.itemText = canFeed ?
                        $"먹이주기 (츄르: {CatTower.Instance.ChurCount}개)" :
                        "먹이주기 (츄르 부족)";
                }
                // 타워 정보 업데이트
                else if (item.itemText.Contains("타워 정보") && CatTower.Instance != null)
                {
                    item.itemText = $"레벨 {CatTower.Instance.Level} 타워 (츄르: {CatTower.Instance.ChurCount}개)";
                }
                // 업그레이드 업데이트
                else if (item.itemText.Contains("업그레이드") && CatTower.Instance != null)
                {
                    if (CatTower.Instance.CanUpgrade())
                    {
                        item.itemText = $"업그레이드 ({CatTower.Instance.GetUpgradeCost()} 츄르)";
                    }
                    else if (CatTower.Instance.Level >= 3)
                    {
                        item.itemText = "업그레이드 (최대 레벨)";
                    }
                    else
                    {
                        item.itemText = "업그레이드 (츄르 부족)";
                    }
                }
            }
        }
    }

    void ClampMenuToScreen(RectTransform menuRect)
    {
        // Canvas의 RenderMode에 따라 다르게 처리
        Canvas canvasComponent = canvas.GetComponent<Canvas>();
        if (canvasComponent.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // Screen Space - Overlay 모드
            Vector3[] corners = new Vector3[4];
            menuRect.GetWorldCorners(corners);

            // 화면 경계 계산
            float menuWidth = corners[2].x - corners[0].x;
            float menuHeight = corners[2].y - corners[0].y;

            Vector3 pos = menuRect.localPosition;

            // 오른쪽 경계 체크
            if (corners[2].x > Screen.width)
            {
                pos.x -= menuWidth;
                Debug.Log("메뉴가 오른쪽 경계를 벗어나서 왼쪽으로 이동");
            }

            // 왼쪽 경계 체크
            if (corners[0].x < 0)
            {
                pos.x += Mathf.Abs(corners[0].x);
                Debug.Log("메뉴가 왼쪽 경계를 벗어나서 오른쪽으로 이동");
            }

            // 아래쪽 경계 체크
            if (corners[0].y < 0)
            {
                pos.y += Mathf.Abs(corners[0].y);
                Debug.Log("메뉴가 아래쪽 경계를 벗어나서 위로 이동");
            }

            // 위쪽 경계 체크
            if (corners[2].y > Screen.height)
            {
                pos.y -= (corners[2].y - Screen.height);
                Debug.Log("메뉴가 위쪽 경계를 벗어나서 아래로 이동");
            }

            menuRect.localPosition = pos;
            Debug.Log($"메뉴 위치 조정 완료: {pos}");
        }
    }

    IEnumerator FadeMenu(bool fadeIn)
    {
        if (currentMenu == null) yield break;

        CanvasGroup canvasGroup = currentMenu.GetComponent<CanvasGroup>();
        if (canvasGroup == null) yield break;

        float startAlpha = fadeIn ? 0f : 1f;
        float endAlpha = fadeIn ? 1f : 0f;
        float elapsedTime = 0f;
        float duration = 1f / fadeSpeed;

        canvasGroup.alpha = startAlpha;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsedTime / duration);
            yield return null;
        }

        canvasGroup.alpha = endAlpha;

        if (!fadeIn)
        {
            if (currentMenu != null)
            {
                Destroy(currentMenu);
                currentMenu = null;
            }
        }
    }

    public void HideMenu()
    {
        Debug.Log($"HideMenu 호출됨 - 현재 메뉴: {(currentMenu != null ? currentMenu.name : "null")}, 메뉴 보임: {isMenuVisible}");

        if (currentMenu != null && isMenuVisible)
        {
            isMenuVisible = false;
            Debug.Log("메뉴 숨기기 시작 - 페이드 아웃 애니메이션 없이 즉시 제거");

            // 페이드 애니메이션 없이 즉시 제거 (디버그용)
            Destroy(currentMenu);
            currentMenu = null;

            // 메뉴가 사라진 후 click-through 상태 복원
            RestoreClickThroughState();

            Debug.Log("메뉴 즉시 제거 완료");
        }
        else
        {
            Debug.Log("숨길 메뉴가 없거나 이미 숨겨짐");
        }
    }

    // click-through 상태를 현재 마우스 위치에 맞게 복원
    void RestoreClickThroughState()
    {
        if (CompatibilityWindowManager.Instance == null || mainCamera == null)
        {
            Debug.Log("CompatibilityWindowManager 또는 Camera가 없어서 click-through 상태 복원 불가");
            return;
        }

        // 현재 마우스 위치 확인
        Vector2 mousePos = CompatibilityWindowManager.Instance.GetMousePositionInWindow();
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, mainCamera.nearClipPlane));

        // 상호작용 가능한 오브젝트 확인 (ClickDetector와 동일한 로직)
        LayerMask interactableLayer = LayerMask.GetMask("Default"); // Layer 8
        LayerMask towerLayer = LayerMask.GetMask("Default"); // Layer 9

        // Layer 확인을 위해 Physics2D 사용
        Collider2D catCollider = Physics2D.OverlapPoint(mouseWorldPos, 1 << 8); // Layer 8
        Collider2D towerCollider = Physics2D.OverlapPoint(mouseWorldPos, 1 << 9); // Layer 9

        bool isOverInteractableObject = (catCollider != null || towerCollider != null);

        if (isOverInteractableObject)
        {
            // 상호작용 가능한 오브젝트 위에 있으면 click-through 비활성화 유지
            CompatibilityWindowManager.Instance.DisableClickThrough();
            Debug.Log("상호작용 가능한 오브젝트 위에 있어서 click-through 비활성화 유지");
        }
        else
        {
            // 빈 공간에 있으면 click-through 활성화
            CompatibilityWindowManager.Instance.EnableClickThrough();
            Debug.Log("빈 공간에 있어서 click-through 활성화");
        }
    }

    // 메뉴 아이템 클릭 이벤트들
    void OnCatStatusClicked()
    {
        Debug.Log("고양이 상태 확인!");
        if (GameDataManager.Instance != null)
        {
            Debug.Log($"현재 행복도: {GameDataManager.Instance.Happiness:F1}% - {GameDataManager.Instance.HappinessStatus}");
        }
    }

    void OnFeedCatClicked()
    {
        Debug.Log("먹이주기 클릭!");
        if (CatTower.Instance != null && GameDataManager.Instance != null)
        {
            if (CatTower.Instance.SpendChur(1))
            {
                GameDataManager.Instance.FeedCat(1);
                Debug.Log("고양이에게 츄르 1개를 주었습니다!");
                DebugLogger.LogToFile("고양이에게 츄르 1개를 주었습니다!");
            }
            else
            {
                Debug.Log("츄르가 부족합니다!");
            }
        }
    }

    void OnPetCatClicked()
    {
        Debug.Log("쓰다듬기 클릭!");
        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.happiness += 2f;
            GameDataManager.Instance.happiness = Mathf.Clamp(GameDataManager.Instance.happiness, 0f, 100f);
            Debug.Log("고양이를 쓰다듬었습니다! +2 행복도");
        }
    }

    void OnTowerInfoClicked()
    {
        Debug.Log("타워 정보 클릭!");
        if (CatTower.Instance != null)
        {
            Debug.Log($"타워 레벨: {CatTower.Instance.Level}");
            Debug.Log($"보유 츄르: {CatTower.Instance.ChurCount}개");
            Debug.Log($"생산 정보: {CatTower.Instance.ProductionInfo}");
        }
    }

    void OnUpgradeClicked()
    {
        Debug.Log("업그레이드 클릭!");
        if (CatTower.Instance != null)
        {
            if (CatTower.Instance.CanUpgrade())
            {
                CatTower.Instance.Upgrade();
                Debug.Log("타워 업그레이드 성공!");
                DebugLogger.LogToFile("타워 업그레이드 성공!");
            }
            else
            {
                Debug.Log("업그레이드할 수 없습니다!");
            }
        }
    }

    void OnCollectClicked()
    {
        Debug.Log("츄르 수집 클릭!");
        if (CatTower.Instance != null)
        {
            Debug.Log($"현재 {CatTower.Instance.ChurCount}개의 츄르를 보유하고 있습니다!");
        }
    }

    public bool IsMenuVisible => isMenuVisible;
}