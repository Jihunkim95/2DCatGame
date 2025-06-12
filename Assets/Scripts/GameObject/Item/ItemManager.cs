using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 아이템 시스템 관리자
/// </summary>
public class ItemManager : MonoBehaviour
{
    [Header("아이템 데이터")]
    public List<ItemData> allItems = new List<ItemData>();

    [Header("기본 아이템들 (코드로 생성)")]
    public bool generateDefaultItems = true;

    [Header("모자 스프라이트들 (Inspector에서 할당)")]
    public Sprite catBeauty1;
    public Sprite catBeauty2;
    public Sprite catBeauty3;

    // 현재 착용 중인 아이템 (한 번에 하나만)
    private ItemData equippedItem = null;

    // 소유한 아이템들
    private HashSet<string> ownedItems = new HashSet<string>();

    // 싱글톤
    public static ItemManager Instance { get; private set; }

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


        if (generateDefaultItems)
        {
            GenerateDefaultItems();
        }
        LoadPlayerData();

        Debug.Log($"아이템 매니저 초기화 완료 - 총 {allItems.Count}개 아이템");
        DebugLogger.LogToFile($"아이템 매니저 초기화 완료 - 총 {allItems.Count}개 아이템");
    }

    void GenerateDefaultItems()
    {
        // Inspector에서 할당된 스프라이트들 사용
        allItems.Add(CreateHatItemWithSprite("고양이 모자 1", catBeauty1, new Vector3(0, 0.3f, 0), 5));
        allItems.Add(CreateHatItemWithSprite("고양이 모자 2", catBeauty2, new Vector3(0, 0.3f, 0), 10));
        allItems.Add(CreateHatItemWithSprite("고양이 모자 3", catBeauty3, new Vector3(0, 0.4f, 0), 15));

        Debug.Log($"모자 아이템 {allItems.Count}개 생성 완료");
    }

    ItemData CreateHatItemWithSprite(string name, Sprite sprite, Vector3 offset, int cost)
    {
        ItemData item = ScriptableObject.CreateInstance<ItemData>();
        item.itemName = name;
        item.description = $"귀여운 {name}";
        item.itemType = ItemData.ItemType.Hat;
        item.positionOffset = offset;
        item.cost = cost;
        item.isUnlocked = cost <= 10; // 10 츄르 이하는 기본 해금
        item.sortingOrder = 5; // 고양이보다 위에 표시

        if (sprite != null)
        {
            item.itemIcon = sprite;
            item.itemSprite = sprite;
            Debug.Log($"모자 스프라이트 할당 성공: {name}");
        }
        else
        {
            // 스프라이트가 할당되지 않았으면 기본 스프라이트 생성
            Debug.LogWarning($"모자 스프라이트가 할당되지 않았습니다: {name}. 기본 스프라이트 사용");
            Color hatColor = GetRandomHatColor();
            item.itemIcon = CreateSimpleSprite(hatColor, 32);
            item.itemSprite = CreateSimpleSprite(hatColor, 64);
        }

        return item;
    }

    Color GetRandomHatColor()
    {
        Color[] colors = { Color.red, Color.blue, Color.yellow, Color.green, Color.magenta };
        return colors[Random.Range(0, colors.Length)];
    }

    Sprite CreateSimpleSprite(Color color, int size)
    {
        Texture2D texture = new Texture2D(size, size);
        Color[] colors = new Color[size * size];

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size * 0.4f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                if (distance <= radius)
                {
                    colors[y * size + x] = color;
                }
                else
                {
                    colors[y * size + x] = Color.clear;
                }
            }
        }

        texture.SetPixels(colors);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // 아이템 구매
    public bool PurchaseItem(ItemData item)
    {
        if (IsItemOwned(item.itemName))
        {
            Debug.Log($"이미 소유한 아이템: {item.itemName}");
            return false;
        }

        if (CatTower.Instance != null && CatTower.Instance.SpendChur(item.cost))
        {
            ownedItems.Add(item.itemName);
            SavePlayerData();

            Debug.Log($"아이템 구매 성공: {item.itemName} (-{item.cost} 츄르)");
            DebugLogger.LogToFile($"아이템 구매 성공: {item.itemName} (-{item.cost} 츄르)");
            return true;
        }

        Debug.Log($"츄르가 부족하여 구매 실패: {item.itemName} (필요: {item.cost})");
        return false;
    }

    // 아이템 착용
    public void EquipItem(ItemData item)
    {
        if (!IsItemOwned(item.itemName) && !item.isUnlocked)
        {
            Debug.Log($"소유하지 않은 아이템: {item.itemName}");
            return;
        }

        equippedItem = item;

        // TestCat에게 적용
        if (TestCat.Instance != null)
        {
            TestCat.Instance.EquipHat(item);
        }

        SavePlayerData();

        Debug.Log($"아이템 착용: {item.itemName}");
        DebugLogger.LogToFile($"아이템 착용: {item.itemName}");
    }

    // 아이템 해제
    public void UnequipItem()
    {
        if (equippedItem != null)
        {
            ItemData item = equippedItem;
            equippedItem = null;

            // TestCat에서 제거
            if (TestCat.Instance != null)
            {
                TestCat.Instance.UnequipHat();
            }

            SavePlayerData();

            Debug.Log($"아이템 해제: {item.itemName}");
            DebugLogger.LogToFile($"아이템 해제: {item.itemName}");
        }
    }

    // 데이터 저장/로드
    void SavePlayerData()
    {
        // 소유 아이템 저장
        string ownedItemsJson = string.Join(",", ownedItems);
        ES3.Save("ownedItems", ownedItemsJson);

        // 착용 아이템 저장
        if (equippedItem != null)
        {
            ES3.Save("equippedItem", equippedItem.itemName);
        }
        else
        {
            ES3.DeleteKey("equippedItem");
        }

        Debug.Log("아이템 데이터 저장 완료");
    }

    void LoadPlayerData()
    {
        // 소유 아이템 로드
        if (ES3.KeyExists("ownedItems"))
        {
            string ownedItemsJson = ES3.Load<string>("ownedItems");
            if (!string.IsNullOrEmpty(ownedItemsJson))
            {
                string[] itemNames = ownedItemsJson.Split(',');
                ownedItems = new HashSet<string>(itemNames);
            }
        }

        // 착용 아이템 로드
        if (ES3.KeyExists("equippedItem"))
        {
            string itemName = ES3.Load<string>("equippedItem");
            ItemData item = GetItemByName(itemName);
            if (item != null)
            {
                equippedItem = item;
            }
        }

        Debug.Log($"아이템 데이터 로드 완료 - 소유: {ownedItems.Count}개, 착용: {(equippedItem != null ? equippedItem.itemName : "없음")}");
    }

    // 유틸리티 메서드들
    public ItemData GetItemByName(string itemName)
    {
        return allItems.FirstOrDefault(item => item.itemName == itemName);
    }

    public bool IsItemOwned(string itemName)
    {
        ItemData item = GetItemByName(itemName);
        return item != null && (item.isUnlocked || ownedItems.Contains(itemName));
    }

    public bool IsItemEquipped(ItemData item)
    {
        return equippedItem != null && equippedItem == item;
    }

    // 프로퍼티들
    public List<ItemData> AllItems => allItems;
    public ItemData EquippedItem => equippedItem;
    public HashSet<string> OwnedItems => ownedItems;
}