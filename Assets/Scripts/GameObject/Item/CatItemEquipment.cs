using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 고양이에게 아이템을 실제로 착용시키는 클래스 (ItemType 기반으로 수정)
/// </summary>
public class CatItemEquipment : MonoBehaviour
{
    [Header("장착 포인트들")]
    public Transform headPoint;     // 머리 포인트 (Hat, Accessory용)
    public Transform facePoint;     // 얼굴 포인트 (Glasses용)
    public Transform bodyPoint;     // 몸체 포인트 (Costume용)
    public Transform backPoint;     // 등 뒤 포인트 (Wings용)

    // 현재 착용 중인 아이템 오브젝트들 (ItemType별로 관리)
    private Dictionary<ItemData.ItemType, GameObject> equippedObjects = new Dictionary<ItemData.ItemType, GameObject>();

    // 싱글톤
    public static CatItemEquipment Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
        }
    }

    void Start()
    {
        SetupEquipmentPoints();

        // 게임 시작 시 저장된 아이템들 착용
        if (ItemManager.Instance != null)
        {
            // ItemManager가 단일 아이템만 지원하므로 해당 아이템 착용
            if (ItemManager.Instance.EquippedItem != null)
            {
                EquipItem(ItemManager.Instance.EquippedItem);
            }
        }

        Debug.Log("고양이 아이템 장비 시스템 초기화 완료");
        DebugLogger.LogToFile("고양이 아이템 장비 시스템 초기화 완료");
    }

    void SetupEquipmentPoints()
    {
        // 장착 포인트들이 없으면 자동 생성
        if (headPoint == null)
        {
            GameObject headObj = new GameObject("HeadPoint");
            headObj.transform.SetParent(transform);
            headObj.transform.localPosition = new Vector3(0, 0.3f, 0);
            headPoint = headObj.transform;
        }

        if (facePoint == null)
        {
            GameObject faceObj = new GameObject("FacePoint");
            faceObj.transform.SetParent(transform);
            faceObj.transform.localPosition = new Vector3(0, 0.1f, 0);
            facePoint = faceObj.transform;
        }

        if (bodyPoint == null)
        {
            GameObject bodyObj = new GameObject("BodyPoint");
            bodyObj.transform.SetParent(transform);
            bodyObj.transform.localPosition = new Vector3(0, -0.1f, 0);
            bodyPoint = bodyObj.transform;
        }

        if (backPoint == null)
        {
            GameObject backObj = new GameObject("BackPoint");
            backObj.transform.SetParent(transform);
            backObj.transform.localPosition = new Vector3(0, 0, -0.1f);
            backPoint = backObj.transform;
        }
    }

    public void EquipItem(ItemData item)
    {
        if (item == null) return;

        // 기존 아이템 제거 (같은 타입의 아이템)
        UnequipItem(item.itemType);

        // 새 아이템 생성
        GameObject itemObj = new GameObject($"Equipped_{item.itemName}");
        SpriteRenderer spriteRenderer = itemObj.AddComponent<SpriteRenderer>();

        // 스프라이트 설정
        spriteRenderer.sprite = item.itemSprite;
        spriteRenderer.sortingOrder = item.sortingOrder;

        // 위치 설정
        Transform parentPoint = GetEquipmentPoint(item.itemType);
        itemObj.transform.SetParent(parentPoint);
        itemObj.transform.localPosition = item.positionOffset;
        itemObj.transform.localRotation = Quaternion.Euler(item.rotationOffset);
        itemObj.transform.localScale = item.scaleMultiplier;

        // 딕셔너리에 저장
        equippedObjects[item.itemType] = itemObj;

        Debug.Log($"아이템 착용 완료: {item.itemName} ({item.itemType})");
    }

    public void UnequipItem(ItemData.ItemType itemType)
    {
        if (equippedObjects.ContainsKey(itemType))
        {
            GameObject itemObj = equippedObjects[itemType];
            if (itemObj != null)
            {
                Destroy(itemObj);
            }
            equippedObjects.Remove(itemType);

            Debug.Log($"아이템 해제 완료: {itemType}");
        }
    }

    // 모든 아이템 해제
    public void UnequipAllItems()
    {
        foreach (var kvp in equippedObjects)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value);
            }
        }
        equippedObjects.Clear();
        Debug.Log("모든 아이템 해제 완료");
    }

    Transform GetEquipmentPoint(ItemData.ItemType itemType)
    {
        switch (itemType)
        {
            case ItemData.ItemType.Hat:
                return headPoint;
            case ItemData.ItemType.Glasses:
                return facePoint;
            case ItemData.ItemType.Accessory:
                return headPoint; // 액세서리도 머리 부근에 착용
            case ItemData.ItemType.Wings:
                return backPoint;
            case ItemData.ItemType.Costume:
                return bodyPoint;
            default:
                return headPoint;
        }
    }

    // 고양이 방향 변경 시 아이템들도 함께 뒤집기
    public void UpdateItemDirection(bool facingRight)
    {
        foreach (var kvp in equippedObjects)
        {
            if (kvp.Value != null)
            {
                SpriteRenderer sr = kvp.Value.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.flipX = !facingRight;
                }
            }
        }
    }

    // 특정 타입의 아이템이 착용되어 있는지 확인
    public bool IsItemTypeEquipped(ItemData.ItemType itemType)
    {
        return equippedObjects.ContainsKey(itemType) && equippedObjects[itemType] != null;
    }

    // 착용 중인 아이템 오브젝트 가져오기
    public GameObject GetEquippedItemObject(ItemData.ItemType itemType)
    {
        if (equippedObjects.ContainsKey(itemType))
        {
            return equippedObjects[itemType];
        }
        return null;
    }

    // 착용 중인 모든 아이템 타입 목록
    public ItemData.ItemType[] GetEquippedItemTypes()
    {
        var types = new ItemData.ItemType[equippedObjects.Count];
        int index = 0;
        foreach (var kvp in equippedObjects)
        {
            if (kvp.Value != null)
            {
                types[index] = kvp.Key;
                index++;
            }
        }
        return types;
    }

    // 프로퍼티들
    public int EquippedItemCount => equippedObjects.Count;
    public bool HasAnyItemEquipped => equippedObjects.Count > 0;
}