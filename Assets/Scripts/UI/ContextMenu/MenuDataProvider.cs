using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 컨텍스트 메뉴 데이터를 제공하는 클래스
/// </summary>
public class MenuDataProvider : MonoBehaviour
{
    public List<ContextMenuItem> GetCatMenuItems()
    {
        return new List<ContextMenuItem>
        {
            ContextMenuItem.CreateButton(GetCatStatusText(), OnCatStatusClicked),
            ContextMenuItem.CreateSeparator(),
            ContextMenuItem.CreateButton(GetFeedCatText(), OnFeedCatClicked),
            ContextMenuItem.CreateButton("쓰다듬기", OnPetCatClicked)
        };
    }

    public List<ContextMenuItem> GetTowerMenuItems()
    {
        return new List<ContextMenuItem>
        {
            ContextMenuItem.CreateButton(GetTowerInfoText(), OnTowerInfoClicked),
            ContextMenuItem.CreateSeparator(),
            ContextMenuItem.CreateButton(GetUpgradeText(), OnUpgradeClicked),
            ContextMenuItem.CreateSeparator(),
            ContextMenuItem.CreateButton("츄르 수집", OnCollectClicked),
            ContextMenuItem.CreateButton("생산 정보", OnTowerInfoClicked)
        };
    }

    private string GetCatStatusText()
    {
        if (GameDataManager.Instance != null)
        {
            return $"상태: {GameDataManager.Instance.HappinessStatus} ({GameDataManager.Instance.Happiness:F1}%)";
        }
        return "고양이 상태보기";
    }

    private string GetFeedCatText()
    {
        if (CatTower.Instance != null)
        {
            bool canFeed = CatTower.Instance.ChurCount >= 1;
            return canFeed ?
                $"먹이주기 (츄르: {CatTower.Instance.ChurCount}개)" :
                "먹이주기 (츄르 부족)";
        }
        return "먹이주기 (1 츄르)";
    }

    private string GetTowerInfoText()
    {
        if (CatTower.Instance != null)
        {
            return $"레벨 {CatTower.Instance.Level} 타워 (츄르: {CatTower.Instance.ChurCount}개)";
        }
        return "타워 정보";
    }

    private string GetUpgradeText()
    {
        if (CatTower.Instance != null)
        {
            if (CatTower.Instance.CanUpgrade())
            {
                return $"업그레이드 ({CatTower.Instance.GetUpgradeCost()} 츄르)";
            }
            else if (CatTower.Instance.Level >= 3)
            {
                return "업그레이드 (최대 레벨)";
            }
            else
            {
                return "업그레이드 (츄르 부족)";
            }
        }
        return "업그레이드";
    }

    // 메뉴 아이템 클릭 이벤트들
    private void OnCatStatusClicked()
    {
        Debug.Log("고양이 상태 확인!");
        if (GameDataManager.Instance != null)
        {
            Debug.Log($"현재 행복도: {GameDataManager.Instance.Happiness:F1}% - {GameDataManager.Instance.HappinessStatus}");
        }
    }

    private void OnFeedCatClicked()
    {
        Debug.Log("먹이주기 클릭!");
        if (CatTower.Instance != null && GameDataManager.Instance != null)
        {
            if (CatTower.Instance.SpendChur(1))
            {
                GameDataManager.Instance.FeedCat(1);
                Debug.Log("고양이에게 츄르 1개를 주었습니다!");
                DebugLogger.LogToFile("고양이에게 츄르 1개를 주었습니다!");

                // TestCat에도 알려주기
                if (TestCat.Instance != null)
                {
                    TestCat.Instance.FeedCat();
                }
            }
            else
            {
                Debug.Log("츄르가 부족합니다!");
            }
        }
    }

    private void OnPetCatClicked()
    {
        Debug.Log("쓰다듬기 클릭!");
        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.happiness += 2f;
            GameDataManager.Instance.happiness = Mathf.Clamp(GameDataManager.Instance.happiness, 0f, 100f);
            Debug.Log("고양이를 쓰다듬었습니다! +2 행복도");

            // TestCat에도 알려주기
            if (TestCat.Instance != null)
            {
                TestCat.Instance.PetCat();
            }
        }
    }

    private void OnTowerInfoClicked()
    {
        Debug.Log("타워 정보 클릭!");
        if (CatTower.Instance != null)
        {
            Debug.Log($"타워 레벨: {CatTower.Instance.Level}");
            Debug.Log($"보유 츄르: {CatTower.Instance.ChurCount}개");
            Debug.Log($"생산 정보: {CatTower.Instance.ProductionInfo}");
        }
    }

    private void OnUpgradeClicked()
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

    private void OnCollectClicked()
    {
        Debug.Log("츄르 수집 클릭!");
        if (CatTower.Instance != null)
        {
            Debug.Log($"현재 {CatTower.Instance.ChurCount}개의 츄르를 보유하고 있습니다!");
        }
    }
}