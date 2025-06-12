// ==================== ItemData.cs ====================
using UnityEngine;

/// <summary>
/// 아이템 데이터 정의
/// </summary>
[CreateAssetMenu(fileName = "New Item", menuName = "Cat Game/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("기본 정보")]
    public string itemName;
    public string description;
    public Sprite itemIcon;
    public Sprite itemSprite; // 실제 착용할 스프라이트

    [Header("아이템 타입")]
    public ItemType itemType;

    [Header("위치 설정")]
    public Vector3 positionOffset = Vector3.zero;
    public Vector3 rotationOffset = Vector3.zero;
    public Vector3 scaleMultiplier = Vector3.one;
    public int sortingOrder = 1;

    [Header("비용")]
    public int cost = 10;
    public bool isUnlocked = true;

    public enum ItemType
    {
        Hat,        // 모자
        Glasses,    // 안경
        Accessory,  // 액세서리
        Wings,      // 날개
        Costume     // 의상
    }
}
