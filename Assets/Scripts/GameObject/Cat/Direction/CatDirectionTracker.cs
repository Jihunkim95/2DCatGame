using UnityEngine;

/// <summary>
/// 고양이의 바라보는 방향을 추적하는 클래스
/// </summary>
public class CatDirectionTracker : MonoBehaviour
{
    // 방향 추적
    private CatPlayerAnimator.CatDirection currentFacingDirection = CatPlayerAnimator.CatDirection.Left;
    private Vector3 lastMovementPosition;

    // 이벤트
    public System.Action<CatPlayerAnimator.CatDirection> OnDirectionChanged;

    void Start()
    {
        lastMovementPosition = transform.position;
    }

    void Update()
    {
        UpdateDirection();
    }

    void UpdateDirection()
    {
        Vector3 currentPosition = transform.position;
        Vector3 movement = currentPosition - lastMovementPosition;

        if (Mathf.Abs(movement.x) > 0.001f) // 임계값 설정으로 미세한 움직임 무시
        {
            CatPlayerAnimator.CatDirection newDirection;

            // X값이 감소하면 Left, 증가하면 Right
            if (movement.x < 0)
            {
                newDirection = CatPlayerAnimator.CatDirection.Left;
            }
            else
            {
                newDirection = CatPlayerAnimator.CatDirection.Right;
            }

            // 방향이 실제로 바뀌었을 때만 업데이트
            if (newDirection != currentFacingDirection)
            {
                currentFacingDirection = newDirection;
                Debug.Log($"🐱 고양이 방향 변경: X={movement.x:F3} → {currentFacingDirection}");
                DebugLogger.LogToFile($"고양이 방향 변경: X={movement.x:F3} → {currentFacingDirection}");

                OnDirectionChanged?.Invoke(currentFacingDirection);
            }
        }

        // 방향 추적을 위한 위치 업데이트
        lastMovementPosition = currentPosition;
    }

    // 외부에서 방향 강제 설정
    public void SetFacingDirection(CatPlayerAnimator.CatDirection direction)
    {
        if (currentFacingDirection != direction)
        {
            currentFacingDirection = direction;
            Debug.Log($"고양이 방향 강제 설정: {currentFacingDirection}");
            OnDirectionChanged?.Invoke(currentFacingDirection);
        }
    }

    // Gizmos로 방향 표시
    void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            Gizmos.color = currentFacingDirection == CatPlayerAnimator.CatDirection.Left ? Color.red : Color.cyan;
            Vector3 arrowStart = transform.position + Vector3.up * 0.5f;
            Vector3 arrowEnd = arrowStart + (currentFacingDirection == CatPlayerAnimator.CatDirection.Left ? Vector3.left : Vector3.right) * 0.5f;
            Gizmos.DrawLine(arrowStart, arrowEnd);
            Gizmos.DrawWireSphere(arrowEnd, 0.1f);
        }
    }

    // 프로퍼티
    public CatPlayerAnimator.CatDirection CurrentFacingDirection => currentFacingDirection;
}