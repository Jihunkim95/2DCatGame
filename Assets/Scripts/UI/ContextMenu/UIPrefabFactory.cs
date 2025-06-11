using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI 프리팹 생성을 담당하는 팩토리 클래스
/// </summary>
public class UIPrefabFactory : MonoBehaviour
{
    [Header("메뉴 설정")]
    public Vector2 menuSize = new Vector2(120f, 180f);
    public float buttonHeight = 24f;
    public float separatorHeight = 6f;

    [Header("폰트 설정")]
    public TMP_FontAsset dungGeunMoFont;

    private Canvas targetCanvas;

    public void Initialize(Canvas canvas)
    {
        targetCanvas = canvas;
        LoadDungGeunMoFont();
    }

    public GameObject CreateMenuPrefab()
    {
        // 메뉴 패널 생성
        GameObject menu = new GameObject("ContextMenu");
        menu.transform.SetParent(targetCanvas.transform, false);

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

    public GameObject CreateButtonPrefab()
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
        textRect.offsetMin = new Vector2(8, 0);
        textRect.offsetMax = new Vector2(-8, 0);

        text.text = "메뉴 아이템";
        text.fontSize = 10f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;

        // DungGeunMo 폰트 적용 시도
        ApplyFont(text);

        return button;
    }

    public GameObject CreateSeparatorPrefab()
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

    private void LoadDungGeunMoFont()
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

    private void ApplyFont(TextMeshProUGUI textComponent)
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
}