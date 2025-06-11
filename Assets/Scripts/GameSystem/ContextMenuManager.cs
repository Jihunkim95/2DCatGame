using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// 리팩터링된 컨텍스트 메뉴 매니저 
/// </summary>
public class ContextMenuManager : MonoBehaviour
{
    [Header("컨텍스트 메뉴 시스템")]
    public Canvas canvas;
    public float fadeSpeed = 6f;

    // 컴포넌트들
    private UIPrefabFactory prefabFactory;
    private MenuDataProvider dataProvider;
    private MenuPositionCalculator positionCalculator;

    private Camera mainCamera;
    private GameObject currentMenu;
    private bool isMenuVisible = false;

    // 프리팹들 (런타임에 생성)
    private GameObject menuPrefab;
    private GameObject buttonPrefab;
    private GameObject separatorPrefab;

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
        InitializeComponents();
        Debug.Log("리팩터링된 컨텍스트 메뉴 시스템 초기화 완료");
        DebugLogger.LogToFile("리팩터링된 컨텍스트 메뉴 시스템 초기화 완료");
    }

    void InitializeComponents()
    {
        mainCamera = Camera.main;

        if (canvas == null)
            canvas = FindObjectOfType<Canvas>();

        // 컴포넌트들 초기화
        InitializePrefabFactory();
        InitializeDataProvider();
        InitializePositionCalculator();

        // 프리팹들 생성
        CreatePrefabs();
    }

    void InitializePrefabFactory()
    {
        prefabFactory = GetComponent<UIPrefabFactory>();
        if (prefabFactory == null)
        {
            prefabFactory = gameObject.AddComponent<UIPrefabFactory>();
        }
        prefabFactory.Initialize(canvas);
    }

    void InitializeDataProvider()
    {
        dataProvider = GetComponent<MenuDataProvider>();
        if (dataProvider == null)
        {
            dataProvider = gameObject.AddComponent<MenuDataProvider>();
        }
    }

    void InitializePositionCalculator()
    {
        positionCalculator = GetComponent<MenuPositionCalculator>();
        if (positionCalculator == null)
        {
            positionCalculator = gameObject.AddComponent<MenuPositionCalculator>();
        }
        positionCalculator.Initialize(mainCamera, canvas);
    }

    void CreatePrefabs()
    {
        if (menuPrefab == null)
            menuPrefab = prefabFactory.CreateMenuPrefab();

        if (buttonPrefab == null)
            buttonPrefab = prefabFactory.CreateButtonPrefab();

        if (separatorPrefab == null)
            separatorPrefab = prefabFactory.CreateSeparatorPrefab();
    }

    void Update()
    {
        HandleInputs();
    }

    void HandleInputs()
    {
        if (!isMenuVisible) return;

        // ESC 키로 메뉴 닫기
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("ESC 키로 메뉴 닫기");
            HideMenu();
        }

        // 좌클릭으로 메뉴 닫기 (메뉴 영역 외부 클릭 시)
        if (Input.GetMouseButtonDown(0) && !IsMouseOverMenu())
        {
            Debug.Log("메뉴 외부 클릭으로 메뉴 닫기");
            HideMenu();
        }

        // 우클릭 시에도 메뉴 닫기 (새 메뉴가 열리기 전에)
        if (Input.GetMouseButtonDown(1))
        {
            Debug.Log("우클릭으로 이전 메뉴 닫기");
            HideMenu();
        }
    }

    bool IsMouseOverMenu()
    {
        if (currentMenu == null || !isMenuVisible) return false;

        RectTransform menuRect = currentMenu.GetComponent<RectTransform>();
        if (menuRect == null) return false;

        return RectTransformUtility.RectangleContainsScreenPoint(
            menuRect, Input.mousePosition, canvas.worldCamera);
    }

    /// <summary>
    /// 고양이 컨텍스트 메뉴 표시
    /// </summary>
    public void ShowCatMenu(Vector3 objectWorldPosition)
    {
        Debug.Log($"ShowCatMenu 호출됨! 오브젝트 위치: {objectWorldPosition}");

        var menuItems = dataProvider.GetCatMenuItems();
        Vector3 menuPosition = positionCalculator.CalculateMenuPosition(objectWorldPosition);

        ShowMenu(menuPosition, menuItems);

        Debug.Log("고양이 컨텍스트 메뉴 표시 완료");
        DebugLogger.LogToFile("고양이 컨텍스트 메뉴 표시 완료");
    }

    /// <summary>
    /// 타워 컨텍스트 메뉴 표시
    /// </summary>
    public void ShowTowerMenu(Vector3 objectWorldPosition)
    {
        Debug.Log($"ShowTowerMenu 호출됨! 오브젝트 위치: {objectWorldPosition}");

        var menuItems = dataProvider.GetTowerMenuItems();
        Vector3 menuPosition = positionCalculator.CalculateMenuPosition(objectWorldPosition);

        ShowMenu(menuPosition, menuItems);

        Debug.Log("타워 컨텍스트 메뉴 표시 완료");
        DebugLogger.LogToFile("타워 컨텍스트 메뉴 표시 완료");
    }

    void ShowMenu(Vector3 worldPosition, List<ContextMenuItem> menuItems)
    {
        Debug.Log("ShowMenu 시작");

        // 기존 메뉴 정리
        ForceCleanupMenu();

        // click-through 비활성화
        DisableClickThrough();

        // 새 메뉴 생성
        currentMenu = Instantiate(menuPrefab, canvas.transform);
        currentMenu.name = "ContextMenu_Active";

        // 메뉴 위치 설정
        SetMenuPosition(worldPosition);

        // 메뉴 아이템들 생성
        CreateMenuItems(menuItems);

        // 메뉴 활성화
        currentMenu.SetActive(true);
        isMenuVisible = true;

        Debug.Log("ShowMenu 완료!");
    }

    void SetMenuPosition(Vector3 worldPosition)
    {
        Vector2 uiPosition = positionCalculator.ConvertToUIPosition(worldPosition);

        RectTransform menuRect = currentMenu.GetComponent<RectTransform>();
        menuRect.localPosition = uiPosition;

        // 화면 경계 체크
        positionCalculator.ClampMenuToScreen(menuRect);
    }

    void CreateMenuItems(List<ContextMenuItem> menuItems)
    {
        Debug.Log($"CreateMenuItems 시작 - 아이템 개수: {menuItems.Count}");

        foreach (var item in menuItems)
        {
            GameObject menuItem;

            if (item.itemType == ContextMenuItem.ItemType.Button)
            {
                menuItem = Instantiate(buttonPrefab, currentMenu.transform);
                SetupButton(menuItem, item);
            }
            else // Separator
            {
                menuItem = Instantiate(separatorPrefab, currentMenu.transform);
            }
        }

        Debug.Log("CreateMenuItems 완료!");
    }

    void SetupButton(GameObject buttonObject, ContextMenuItem item)
    {
        // 텍스트 설정
        TextMeshProUGUI text = buttonObject.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = item.itemText;
        }

        // 클릭 이벤트 연결
        Button button = buttonObject.GetComponent<Button>();
        if (button != null && item.onClick != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                Debug.Log($"버튼 클릭됨: {item.itemText}");
                item.onClick.Invoke();
                HideMenu();
            });
        }
    }

    void ForceCleanupMenu()
    {
        Debug.Log("ForceCleanupMenu 시작");

        StopAllCoroutines();

        // 기존 메뉴 제거
        if (currentMenu != null)
        {
            Destroy(currentMenu);
            currentMenu = null;
        }

        isMenuVisible = false;
        RestoreClickThroughState();

        Debug.Log("ForceCleanupMenu 완료");
    }

    public void HideMenu()
    {
        Debug.Log("HideMenu 호출됨");

        if (currentMenu != null && isMenuVisible)
        {
            isMenuVisible = false;
            Destroy(currentMenu);
            currentMenu = null;
            RestoreClickThroughState();
            Debug.Log("메뉴 숨기기 완료");
        }
    }

    void DisableClickThrough()
    {
        if (CompatibilityWindowManager.Instance != null)
        {
            CompatibilityWindowManager.Instance.DisableClickThrough();
            Debug.Log("컨텍스트 메뉴 표시로 인한 click-through 비활성화");
        }
    }

    void RestoreClickThroughState()
    {
        if (CompatibilityWindowManager.Instance == null || mainCamera == null) return;

        // 현재 마우스 위치 확인
        Vector2 mousePos = CompatibilityWindowManager.Instance.GetMousePositionInWindow();
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, mainCamera.nearClipPlane));

        // 상호작용 가능한 오브젝트 확인
        Collider2D catCollider = Physics2D.OverlapPoint(mouseWorldPos, 1 << 8); // Layer 8
        Collider2D towerCollider = Physics2D.OverlapPoint(mouseWorldPos, 1 << 9); // Layer 9

        bool isOverInteractableObject = (catCollider != null || towerCollider != null);

        if (isOverInteractableObject)
        {
            CompatibilityWindowManager.Instance.DisableClickThrough();
            Debug.Log("상호작용 가능한 오브젝트 위에 있어서 click-through 비활성화 유지");
        }
        else
        {
            CompatibilityWindowManager.Instance.EnableClickThrough();
            Debug.Log("빈 공간에 있어서 click-through 활성화");
        }
    }

    // 공개 프로퍼티
    public bool IsMenuVisible => isMenuVisible;
}