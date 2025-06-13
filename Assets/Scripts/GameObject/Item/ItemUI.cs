using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 아이템 창 UI 관리자 - 우측하단 세로 배치, 이미지만 표시
/// </summary>
public class ItemUI : MonoBehaviour
{
    [Header("UI 요소들")]
    public Button itemButton;           // Item 버튼 (Inspector에서 할당)
    public GameObject itemPanel;        // 아이템 패널 (자동 생성됨)
    public Transform itemListParent;    // 아이템 목록 부모 (자동 생성됨)
    public GameObject itemSlotPrefab;   // 아이템 슬롯 프리팹 (자동 생성됨)

    [Header("아이템 창 설정")]
    public float slotSize = 50f;        // 슬롯 크기 (정사각형)
    public float slotSpacing = 5f;      // 슬롯 간격
    public Vector2 panelOffset = new Vector2(0f, 150f); // 화면 우측하단에서의 오프셋

    private List<GameObject> itemSlots = new List<GameObject>();
    private bool isPanelVisible = false;

    // 싱글톤
    public static ItemUI Instance { get; private set; }

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
        SetupUI();

        // 상태 초기화 (중요!)
        isPanelVisible = false;

        // Item 버튼 클릭 이벤트 연결
        if (itemButton != null)
        {
            itemButton.onClick.AddListener(ToggleItemPanel);
        }
        else
        {
            Debug.LogWarning("ItemButton이 Inspector에서 할당되지 않았습니다! 자동으로 생성합니다.");
            CreateItemButton();
        }

        // 초기에는 패널 숨김 (강제)
        if (itemPanel != null)
        {
            itemPanel.SetActive(false);
        }

        Debug.Log($"아이템 UI 초기화 완료 - 패널 상태: {isPanelVisible}");
        DebugLogger.LogToFile("아이템 UI 초기화 완료 (우측하단 세로배치)");
    }

    void SetupUI()
    {
        // UI 요소들이 없으면 자동 생성
        if (itemPanel == null)
        {
            CreateItemPanel();
        }

        if (itemSlotPrefab == null)
        {
            CreateItemSlotPrefab();
        }
    }

    void CreateItemButton()
    {
        // Canvas 찾기
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // Item 버튼 생성
        GameObject buttonObj = new GameObject("ItemButton");
        buttonObj.transform.SetParent(canvas.transform, false);

        RectTransform rect = buttonObj.AddComponent<RectTransform>();
        Image image = buttonObj.AddComponent<Image>();
        Button button = buttonObj.AddComponent<Button>();

        // 버튼 설정 (화면 우측 상단에 배치)
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-60f, -60f);
        rect.sizeDelta = new Vector2(80f, 40f);

        image.color = new Color(0.2f, 0.4f, 0.8f, 0.8f);

        // 텍스트 추가
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);

        Text text = textObj.AddComponent<Text>();
        text.text = "Item";
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 14;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        // 클릭 이벤트 연결
        button.onClick.AddListener(ToggleItemPanel);
        itemButton = button;
    }

    void CreateItemPanel()
    {
        // Canvas 찾기
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // 아이템 패널 생성 (우측 하단)
        GameObject panelObj = new GameObject("ItemPanel");
        panelObj.transform.SetParent(canvas.transform, false);

        RectTransform rect = panelObj.AddComponent<RectTransform>();
        VerticalLayoutGroup layoutGroup = panelObj.AddComponent<VerticalLayoutGroup>();
        ContentSizeFitter sizeFitter = panelObj.AddComponent<ContentSizeFitter>();

        // 우측 하단에 앵커 설정
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = panelOffset;

        // 레이아웃 설정 (세로 배치, 아래에서 위로)
        layoutGroup.childAlignment = TextAnchor.LowerCenter;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = true;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.spacing = slotSpacing;
        layoutGroup.padding = new RectOffset(0, 0, 0, 0);

        // 크기 자동 조정
        sizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        itemPanel = panelObj;
        itemListParent = panelObj.transform;
    }

    void CreateItemSlotPrefab()
    {
        // 아이템 슬롯 프리팹 생성 (정사각형, 이미지만)
        GameObject slotObj = new GameObject("ItemSlot");

        RectTransform rect = slotObj.AddComponent<RectTransform>();
        Image image = slotObj.AddComponent<Image>();
        Button button = slotObj.AddComponent<Button>();
        LayoutElement layoutElement = slotObj.AddComponent<LayoutElement>();

        // 정사각형 슬롯 설정
        rect.sizeDelta = new Vector2(slotSize, slotSize);

        // 기본 배경색 (투명)
        image.color = new Color(0f, 0f, 0f, 0.3f);

        // 레이아웃 설정
        layoutElement.minWidth = slotSize;
        layoutElement.minHeight = slotSize;
        layoutElement.preferredWidth = slotSize;
        layoutElement.preferredHeight = slotSize;

        // 버튼 색상 설정
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.3f);
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.6f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 0.8f);
        colors.selectedColor = new Color(0.9f, 0.9f, 0.9f, 0.5f);
        button.colors = colors;

        // 아이템 이미지 (슬롯 전체를 덮도록)
        GameObject itemImageObj = new GameObject("ItemImage");
        itemImageObj.transform.SetParent(slotObj.transform, false);

        RectTransform itemImageRect = itemImageObj.AddComponent<RectTransform>();
        Image itemImage = itemImageObj.AddComponent<Image>();

        // 이미지가 슬롯 전체를 덮도록 설정
        itemImageRect.anchorMin = Vector2.zero;
        itemImageRect.anchorMax = Vector2.one;
        itemImageRect.offsetMin = Vector2.zero;
        itemImageRect.offsetMax = Vector2.zero;

        // 기본적으로 이미지는 숨김
        itemImage.color = new Color(1f, 1f, 1f, 0f);

        // 착용 상태 표시용 테두리
        GameObject borderObj = new GameObject("EquipBorder");
        borderObj.transform.SetParent(slotObj.transform, false);

        RectTransform borderRect = borderObj.AddComponent<RectTransform>();
        Image borderImage = borderObj.AddComponent<Image>();

        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = Vector2.zero;
        borderRect.offsetMax = Vector2.zero;

        // 기본적으로 테두리는 숨김
        borderImage.color = new Color(0f, 1f, 0f, 1f); // 녹색 테두리

        itemSlotPrefab = slotObj;
        slotObj.SetActive(false);
    }

    public void ToggleItemPanel()
    {
        Debug.Log($"ToggleItemPanel 호출 - 현재 상태: {isPanelVisible}");

        isPanelVisible = !isPanelVisible;

        Debug.Log($"ToggleItemPanel - 변경된 상태: {isPanelVisible}");

        if (itemPanel != null)
        {
            itemPanel.SetActive(isPanelVisible);
            Debug.Log($"itemPanel.SetActive({isPanelVisible}) 완료");

            if (isPanelVisible)
            {
                Debug.Log("패널 열기 - RefreshItemList 호출");
                RefreshItemList();

                // 패널이 열릴 때 click-through 비활성화
                if (CompatibilityWindowManager.Instance != null)
                {
                    CompatibilityWindowManager.Instance.DisableClickThrough();
                }
            }
            else
            {
                Debug.Log("패널 닫기 - click-through 상태 복원");
                // 패널이 닫힐 때 click-through 상태 복원
                RestoreClickThroughState();
            }
        }
        else
        {
            Debug.LogError("itemPanel이 null입니다!");
        }

        Debug.Log($"아이템 패널 {(isPanelVisible ? "열기" : "닫기")} 완료");
    }

    public void RefreshItemList()
    {
        Debug.Log("=== RefreshItemList 시작 (세로배치) ===");

        if (itemListParent == null)
        {
            Debug.LogError("itemListParent가 null입니다!");
            return;
        }

        if (ItemManager.Instance == null)
        {
            Debug.LogError("ItemManager.Instance가 null입니다!");
            return;
        }

        Debug.Log($"ItemManager 아이템 개수: {ItemManager.Instance.AllItems.Count}");

        // 기존 슬롯들 제거
        ClearItemSlots();

        // 새 아이템들 생성
        foreach (ItemData item in ItemManager.Instance.AllItems)
        {
            Debug.Log($"아이템 슬롯 생성 중: {item.itemName}");
            CreateItemSlot(item);
        }

        Debug.Log($"아이템 목록 갱신 완료 - {ItemManager.Instance.AllItems.Count}개");
        Debug.Log("=== RefreshItemList 완료 ===");
    }

    void ClearItemSlots()
    {
        // 기존 슬롯들 제거
        foreach (GameObject slot in itemSlots)
        {
            if (slot != null)
            {
                Destroy(slot);
            }
        }
        itemSlots.Clear();

        // 혹시 남은 자식들도 제거
        foreach (Transform child in itemListParent)
        {
            Destroy(child.gameObject);
        }
    }

    void CreateItemSlot(ItemData item)
    {
        if (itemSlotPrefab == null || itemListParent == null) return;

        GameObject slotObj = Instantiate(itemSlotPrefab, itemListParent);
        slotObj.SetActive(true);
        itemSlots.Add(slotObj);

        // 아이템 이미지 설정
        Image itemImage = slotObj.transform.Find("ItemImage").GetComponent<Image>();
        Image borderImage = slotObj.transform.Find("EquipBorder").GetComponent<Image>();
        Image bgImage = slotObj.GetComponent<Image>();

        bool isOwned = ItemManager.Instance.IsItemOwned(item.itemName);
        bool isEquipped = ItemManager.Instance.IsItemEquipped(item);

        if (isOwned && item.itemIcon != null)
        {
            // 소유한 아이템 - 이미지 표시
            itemImage.sprite = item.itemIcon;
            itemImage.color = Color.white;

            if (isEquipped)
            {
                // 착용중 - 녹색 테두리 표시
                borderImage.color = new Color(96f/255f, 134f / 255f, 247f / 255f, 0.8f);
                bgImage.color = new Color(0.2f, 0.6f, 0.2f, 0.5f);
            }
            else
            {
                // 소유하지만 미착용
                borderImage.color = new Color(0f, 1f, 0f, 0f);
                bgImage.color = new Color(0f, 0f, 0f, 0.3f);
            }
        }
        else
        {
            // 미소유 아이템
            if (item.itemIcon != null)
            {
                itemImage.sprite = item.itemIcon;
                itemImage.color = new Color(0.3f, 0.3f, 0.3f, 0.7f); // 어둡게 표시
            }
            else
            {
                itemImage.color = new Color(1f, 1f, 1f, 0f); // 숨김
            }

            borderImage.color = new Color(1f, 0f, 0f, 0.3f); // 빨간색 테두리
            bgImage.color = new Color(0.5f, 0.1f, 0.1f, 0.4f);
        }

        // 버튼 이벤트 설정
        Button button = slotObj.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnItemSlotClicked(item));
        }

        // 슬롯 이름 설정 (디버그용)
        slotObj.name = $"ItemSlot_{item.itemName}";
    }

    void OnItemSlotClicked(ItemData item)
    {
        if (ItemManager.Instance == null) return;

        bool isOwned = ItemManager.Instance.IsItemOwned(item.itemName);
        bool isEquipped = ItemManager.Instance.IsItemEquipped(item);

        if (isEquipped)
        {
            // 착용 해제
            ItemManager.Instance.UnequipItem();
            RefreshItemList();
            Debug.Log($"아이템 해제: {item.itemName}");

            // click-through 상태 복원
            RestoreClickThroughState();
        }
        else if (isOwned)
        {
            // 착용
            ItemManager.Instance.EquipItem(item);
            RefreshItemList();
            Debug.Log($"아이템 착용: {item.itemName}");

            // click-through 상태 복원
            RestoreClickThroughState();
        }
        else
        {
            // 구매 시도
            if (ItemManager.Instance.PurchaseItem(item))
            {
                // 구매 성공 시 바로 착용
                ItemManager.Instance.EquipItem(item);
                RefreshItemList();
                Debug.Log($"아이템 구매 및 착용: {item.itemName}");

                // click-through 상태 복원
                RestoreClickThroughState();
            }
            else
            {
                Debug.Log($"아이템 구매 실패: {item.itemName} (츄르 부족)");
            }
        }
    }

    void RestoreClickThroughState()
    {
        if (CompatibilityWindowManager.Instance == null) return;

        // 현재 마우스 위치 확인하여 적절한 click-through 상태 설정
        Vector2 mousePos = CompatibilityWindowManager.Instance.GetMousePositionInWindow();
        Camera mainCamera = Camera.main;

        if (mainCamera != null)
        {
            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, mainCamera.nearClipPlane));

            // 상호작용 가능한 오브젝트 확인
            Collider2D catCollider = Physics2D.OverlapPoint(mouseWorldPos, 1 << 8);
            Collider2D towerCollider = Physics2D.OverlapPoint(mouseWorldPos, 1 << 9);

            bool isOverInteractableObject = (catCollider != null || towerCollider != null);

            if (isOverInteractableObject)
            {
                CompatibilityWindowManager.Instance.DisableClickThrough();
            }
            else
            {
                CompatibilityWindowManager.Instance.EnableClickThrough();
            }
        }
    }

    void Update()
    {
        // ESC 키로 패널 닫기
        if (Input.GetKeyDown(KeyCode.Escape) && isPanelVisible)
        {
            ToggleItemPanel();
        }

        // 패널 외부 클릭으로 닫기 (패널이 보일 때만)
        if (Input.GetMouseButtonDown(0) && isPanelVisible && itemPanel != null)
        {
            Vector2 mousePos = Input.mousePosition;
            RectTransform panelRect = itemPanel.GetComponent<RectTransform>();

            if (!RectTransformUtility.RectangleContainsScreenPoint(panelRect, mousePos))
            {
                ToggleItemPanel();
            }
        }

        // 디버그: T 키로 강제 아이템 갱신 테스트
        if (Input.GetKeyDown(KeyCode.T))
        {
            Debug.Log("=== 강제 아이템 테스트 (세로배치) ===");
            if (ItemManager.Instance != null)
            {
                Debug.Log($"ItemManager 존재, 아이템 개수: {ItemManager.Instance.AllItems.Count}");
                foreach (var item in ItemManager.Instance.AllItems)
                {
                    Debug.Log($"- {item.itemName} ({item.cost} 츄르)");
                }
                if (isPanelVisible)
                {
                    RefreshItemList();
                }
            }
            else
            {
                Debug.LogError("ItemManager.Instance가 null!");
            }
        }
    }

    // 슬롯 위치 조정 메서드 (런타임에서 호출 가능)
    public void SetSlotSize(float newSize)
    {
        slotSize = newSize;
        if (itemSlotPrefab != null)
        {
            RectTransform rect = itemSlotPrefab.GetComponent<RectTransform>();
            LayoutElement layout = itemSlotPrefab.GetComponent<LayoutElement>();

            rect.sizeDelta = new Vector2(slotSize, slotSize);
            layout.minWidth = slotSize;
            layout.minHeight = slotSize;
            layout.preferredWidth = slotSize;
            layout.preferredHeight = slotSize;
        }
        RefreshItemList();
    }

    public void SetPanelOffset(Vector2 newOffset)
    {
        panelOffset = newOffset;
        if (itemListParent != null)
        {
            RectTransform rect = itemListParent.GetComponent<RectTransform>();
            rect.anchoredPosition = panelOffset;
        }
    }

    // 프로퍼티
    public int ItemCount => itemSlots.Count;
    public Vector2 PanelOffset => panelOffset;
    public float SlotSize => slotSize;
    public bool IsPanelVisible => isPanelVisible;
}