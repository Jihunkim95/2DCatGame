using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// 아이템 창 UI 관리자
/// </summary>
public class ItemUI : MonoBehaviour
{
    [Header("UI 요소들")]
    public Button itemButton;           // Item 버튼
    public GameObject itemPanel;        // 아이템 창 패널
    public Transform itemListParent;    // 아이템 목록 부모
    public GameObject itemSlotPrefab;   // 아이템 슬롯 프리팹

    [Header("아이템 창 설정")]
    public Vector2 panelSize = new Vector2(200f, 400f);
    public float slotHeight = 60f;

    [Header("폰트 설정")]
    public TMP_FontAsset dungGeunMoFont;

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
        LoadDungGeunMoFont();
        SetupUI();

        // Item 버튼 클릭 이벤트 연결
        if (itemButton != null)
        {
            itemButton.onClick.AddListener(ToggleItemPanel);
        }

        Debug.Log("아이템 UI 초기화 완료");
        DebugLogger.LogToFile("아이템 UI 초기화 완료");
    }

    void LoadDungGeunMoFont()
    {
        if (dungGeunMoFont != null) return;

        string[] resourcePaths = {
            "Font/DungGeunMo SDF",
            "Font/DungGeunMo",
            "DungGeunMo SDF",
            "DungGeunMo"
        };

        foreach (string path in resourcePaths)
        {
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>(path);
            if (font != null)
            {
                dungGeunMoFont = font;
                Debug.Log($"DungGeunMo 폰트 로드 성공: {path}");
                return;
            }
        }

        // 기본 폰트 사용
        dungGeunMoFont = TMP_Settings.defaultFontAsset;
        Debug.LogWarning("DungGeunMo 폰트를 찾을 수 없어 기본 폰트 사용");
    }

    void ApplyFont(TextMeshProUGUI textComponent)
    {
        if (textComponent == null) return;

        if (dungGeunMoFont != null)
        {
            try
            {
                textComponent.font = dungGeunMoFont;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"폰트 적용 실패: {e.Message}");
                textComponent.font = TMP_Settings.defaultFontAsset;
            }
        }
        else if (textComponent.font == null)
        {
            textComponent.font = TMP_Settings.defaultFontAsset;
        }
    }

    void SetupUI()
    {
        // UI 요소들이 없으면 자동 생성
        if (itemButton == null)
        {
            CreateItemButton();
        }

        if (itemPanel == null)
        {
            CreateItemPanel();
        }

        if (itemSlotPrefab == null)
        {
            CreateItemSlotPrefab();
        }

        // 초기에는 패널 숨김
        if (itemPanel != null)
        {
            itemPanel.SetActive(false);
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

        // 텍스트 추가 (TextMeshPro 사용)
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "Item";
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 14;

        // DungGeunMo 폰트 적용
        ApplyFont(text);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        itemButton = button;
    }

    void CreateItemPanel()
    {
        // Canvas 찾기
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // 아이템 패널 생성
        GameObject panelObj = new GameObject("ItemPanel");
        panelObj.transform.SetParent(canvas.transform, false);

        RectTransform rect = panelObj.AddComponent<RectTransform>();
        Image image = panelObj.AddComponent<Image>();
        ScrollRect scrollRect = panelObj.AddComponent<ScrollRect>();

        // 패널 설정 (화면 좌측에 배치)
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(panelSize.x * 0.5f + 10f, 0f);
        rect.sizeDelta = panelSize;

        image.color = new Color(0.1f, 0.1f, 0.2f, 0.9f);

        // 스크롤 영역 생성
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(panelObj.transform, false);

        RectTransform contentRect = contentObj.AddComponent<RectTransform>();
        VerticalLayoutGroup layoutGroup = contentObj.AddComponent<VerticalLayoutGroup>();
        ContentSizeFitter sizeFitter = contentObj.AddComponent<ContentSizeFitter>();

        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;

        layoutGroup.spacing = 5f;
        layoutGroup.padding = new RectOffset(10, 10, 10, 10);
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = true;

        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 스크롤 설정
        scrollRect.content = contentRect;
        scrollRect.vertical = true;
        scrollRect.horizontal = false;

        itemPanel = panelObj;
        itemListParent = contentObj.transform;
    }

    void CreateItemSlotPrefab()
    {
        // 아이템 슬롯 프리팹 생성
        GameObject slotObj = new GameObject("ItemSlot");

        RectTransform rect = slotObj.AddComponent<RectTransform>();
        Image image = slotObj.AddComponent<Image>();
        Button button = slotObj.AddComponent<Button>();
        LayoutElement layoutElement = slotObj.AddComponent<LayoutElement>();

        rect.sizeDelta = new Vector2(0f, slotHeight);
        image.color = new Color(0.15f, 0.15f, 0.25f, 1f);
        layoutElement.minHeight = slotHeight;
        layoutElement.preferredHeight = slotHeight;

        // 아이콘 이미지
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(slotObj.transform, false);

        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        Image iconImage = iconObj.AddComponent<Image>();

        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(30f, 0f);
        iconRect.sizeDelta = new Vector2(40f, 40f);

        // 텍스트 (TextMeshPro 사용)
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(slotObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();

        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(70f, 0f);
        textRect.offsetMax = new Vector2(-10f, 0f);

        text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.fontSize = 12;

        // DungGeunMo 폰트 적용
        ApplyFont(text);

        itemSlotPrefab = slotObj;
        slotObj.SetActive(false);
    }

    public void ToggleItemPanel()
    {
        isPanelVisible = !isPanelVisible;

        if (itemPanel != null)
        {
            itemPanel.SetActive(isPanelVisible);

            if (isPanelVisible)
            {
                RefreshItemList();

                // 패널이 열릴 때 click-through 비활성화
                if (CompatibilityWindowManager.Instance != null)
                {
                    CompatibilityWindowManager.Instance.DisableClickThrough();
                }
            }
            else
            {
                // 패널이 닫힐 때 click-through 상태 복원
                RestoreClickThroughState();
            }
        }

        Debug.Log($"아이템 패널 {(isPanelVisible ? "열기" : "닫기")}");
    }

    public void RefreshItemList()
    {
        Debug.Log("=== RefreshItemList 시작 ===");

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

        // 기존 아이템들 제거
        foreach (Transform child in itemListParent)
        {
            Destroy(child.gameObject);
        }

        // 새 아이템들 생성
        foreach (ItemData item in ItemManager.Instance.AllItems)
        {
            Debug.Log($"아이템 생성 중: {item.itemName}");
            CreateItemSlot(item);
        }

        Debug.Log($"아이템 목록 갱신 완료 - {ItemManager.Instance.AllItems.Count}개");
        Debug.Log("=== RefreshItemList 완료 ===");
    }

    void CreateItemSlot(ItemData item)
    {
        if (itemSlotPrefab == null || itemListParent == null) return;

        GameObject slotObj = Instantiate(itemSlotPrefab, itemListParent);
        slotObj.SetActive(true);

        // 아이콘 설정
        Image iconImage = slotObj.transform.Find("Icon").GetComponent<Image>();
        if (iconImage != null && item.itemIcon != null)
        {
            iconImage.sprite = item.itemIcon;
        }

        // 텍스트 설정
        TextMeshProUGUI text = slotObj.transform.Find("Text").GetComponent<TextMeshProUGUI>();
        if (text != null)
        {
            bool isOwned = ItemManager.Instance.IsItemOwned(item.itemName);
            bool isEquipped = ItemManager.Instance.IsItemEquipped(item);

            if (isEquipped)
            {
                text.text = $"[착용중] {item.itemName}";
                text.color = Color.green;
            }
            else if (isOwned)
            {
                text.text = item.itemName;
                text.color = Color.white;
            }
            else
            {
                text.text = $"{item.itemName} ({item.cost} 츄르)";
                text.color = Color.gray;
            }
        }

        // 버튼 이벤트 설정
        Button button = slotObj.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnItemSlotClicked(item));
        }

        // 배경색 설정
        Image bgImage = slotObj.GetComponent<Image>();
        if (bgImage != null)
        {
            if (ItemManager.Instance.IsItemEquipped(item))
            {
                bgImage.color = new Color(0.2f, 0.4f, 0.2f, 1f); // 녹색 (착용중)
            }
            else if (ItemManager.Instance.IsItemOwned(item.itemName))
            {
                bgImage.color = new Color(0.15f, 0.15f, 0.25f, 1f); // 기본색 (소유)
            }
            else
            {
                bgImage.color = new Color(0.3f, 0.15f, 0.15f, 1f); // 빨간색 (미소유)
            }
        }
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
        }
        else if (isOwned)
        {
            // 착용
            ItemManager.Instance.EquipItem(item);
            RefreshItemList();
            Debug.Log($"아이템 착용: {item.itemName}");
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

        // 패널 외부 클릭으로 닫기
        if (Input.GetMouseButtonDown(0) && isPanelVisible)
        {
            Vector2 mousePos = Input.mousePosition;
            RectTransform panelRect = itemPanel.GetComponent<RectTransform>();

            if (!RectTransformUtility.RectangleContainsScreenPoint(panelRect, mousePos))
            {
                ToggleItemPanel();
            }
        }

        // 디버그: T 키로 강제 아이템 생성 테스트
        if (Input.GetKeyDown(KeyCode.T))
        {
            Debug.Log("=== 강제 아이템 테스트 ===");
            if (ItemManager.Instance != null)
            {
                Debug.Log($"ItemManager 존재, 아이템 개수: {ItemManager.Instance.AllItems.Count}");
                foreach (var item in ItemManager.Instance.AllItems)
                {
                    Debug.Log($"- {item.itemName} ({item.cost} 츄르)");
                }
            }
            else
            {
                Debug.LogError("ItemManager.Instance가 null!");
            }

            if (isPanelVisible)
            {
                RefreshItemList();
            }
        }
    }

    // 프로퍼티
    public bool IsPanelVisible => isPanelVisible;
}