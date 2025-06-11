using UnityEngine;

/// <summary>
/// 고양이 스프라이트 관리를 담당하는 클래스
/// </summary>
public class CatSpriteManager : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Vector3 originalScale;
    private Sprite originalSprite;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        // 원본 스케일과 스프라이트 저장
        originalScale = transform.localScale;

        // 고양이 스프라이트가 없으면 기본 스프라이트 생성
        if (spriteRenderer.sprite == null)
        {
            CreateDefaultCatSprite();
        }

        // 원본 스프라이트 저장 (생성 후에)
        originalSprite = spriteRenderer.sprite;
    }

    void CreateDefaultCatSprite()
    {
        // 기본 원형 스프라이트 생성 (PPU 200으로 설정)
        Texture2D texture = new Texture2D(64, 64);
        Color[] colors = new Color[64 * 64];

        // 원형 모양으로 색칠
        Vector2 center = new Vector2(32, 32);
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                if (distance <= 30)
                {
                    colors[y * 64 + x] = Color.white; // 고양이 색상
                }
                else
                {
                    colors[y * 64 + x] = Color.clear; // 투명
                }
            }
        }

        texture.SetPixels(colors);
        texture.Apply();

        // PPU 200으로 설정하여 기존 Cat 이미지들과 일치시킴
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 200f);
        spriteRenderer.sprite = sprite;

        Debug.Log("기본 고양이 스프라이트 생성 완료 (PPU: 200)");
        DebugLogger.LogToFile("기본 고양이 스프라이트 생성 완료 (PPU: 200)");
    }

    // 스프라이트가 변경될 때 원본 정보 업데이트
    public void UpdateOriginalSpriteInfo()
    {
        originalScale = transform.localScale;
        originalSprite = spriteRenderer.sprite;

        Debug.Log($"원본 스프라이트 정보 업데이트 - 스케일: {originalScale}, PPU: {(originalSprite != null ? originalSprite.pixelsPerUnit : 0)}");
        DebugLogger.LogToFile($"원본 스프라이트 정보 업데이트 - 스케일: {originalScale}, PPU: {(originalSprite != null ? originalSprite.pixelsPerUnit : 0)}");
    }

    // 색상 변경
    public void SetColor(Color color)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
        }
    }

    // 스케일 변경
    public void SetScale(Vector3 scale)
    {
        transform.localScale = scale;
    }

    // 원본 스케일로 복원
    public void ResetScale()
    {
        transform.localScale = originalScale;
    }

    // 프로퍼티들
    public Vector3 OriginalScale => originalScale;
    public Sprite OriginalSprite => originalSprite;
    public SpriteRenderer SpriteRenderer => spriteRenderer;
}