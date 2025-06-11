using UnityEngine;

/// <summary>
/// 컨텍스트 메뉴 아이템 정의
/// </summary>
[System.Serializable]
public class ContextMenuItem
{
    public enum ItemType { Button, Separator }

    public ItemType itemType;
    public string itemText;
    public Sprite itemIcon;
    public System.Action onClick;

    // 생성자
    public ContextMenuItem(ItemType type, string text = "", System.Action clickAction = null)
    {
        itemType = type;
        itemText = text;
        onClick = clickAction;
    }

    // 버튼 생성용 헬퍼 메서드
    public static ContextMenuItem CreateButton(string text, System.Action onClick)
    {
        return new ContextMenuItem(ItemType.Button, text, onClick);
    }

    // 구분선 생성용 헬퍼 메서드
    public static ContextMenuItem CreateSeparator()
    {
        return new ContextMenuItem(ItemType.Separator);
    }
}