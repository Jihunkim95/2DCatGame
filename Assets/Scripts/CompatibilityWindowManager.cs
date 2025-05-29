using System;
using System.Runtime.InteropServices;
using UnityEngine;
using System.Collections;

public class CompatibilityWindowManager : MonoBehaviour
{
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    #region Windows API 함수들
    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateSolidBrush(uint color);

    [DllImport("user32.dll")]
    private static extern IntPtr SetClassLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);
    #endregion
#endif

    #region 윈도우 상수들
    private const int GWL_EXSTYLE = -20;
    private const int GWL_STYLE = -16;  // 추가: 일반 스타일
    private const int GCL_HBRBACKGROUND = -10;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TRANSPARENT = 0x20;

    // 윈도우 스타일 상수들 추가
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_VISIBLE = 0x10000000;
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_SYSMENU = 0x00080000;
    private const int WS_THICKFRAME = 0x00040000;
    private const int WS_MINIMIZEBOX = 0x00020000;
    private const int WS_MAXIMIZEBOX = 0x00010000;

    private const uint LWA_COLORKEY = 0x1;
    private const uint LWA_ALPHA = 0x2;

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_FRAMECHANGED = 0x0020;
    #endregion

    #region 구조체
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }
    #endregion

    private IntPtr windowHandle;
    private bool isClickThrough = false;

    // 싱글톤 패턴
    public static CompatibilityWindowManager Instance { get; private set; }

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
        StartCoroutine(SetupWindowDelayed());
    }

    IEnumerator SetupWindowDelayed()
    {
        yield return new WaitForSeconds(1f); // 더 긴 지연
        SetupWindow();
    }

    void SetupWindow()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try
        {
            windowHandle = GetActiveWindow();
            
            if (windowHandle == IntPtr.Zero)
            {
                Debug.LogError("윈도우 핸들을 찾을 수 없습니다!");
                DebugLogger.LogToFile("윈도우 핸들을 찾을 수 없습니다!");
                return;
            }

            Debug.Log($"=== Full Screen Borderless 윈도우 설정 시작 ===");
            Debug.Log($"윈도우 핸들 획득: {windowHandle}");
            Debug.Log($"현재 화면 해상도: {Screen.width}x{Screen.height}");
            DebugLogger.LogToFile($"=== Full Screen Borderless 윈도우 설정 시작 ===");
            DebugLogger.LogToFile($"윈도우 핸들 획득: {windowHandle}");
            DebugLogger.LogToFile($"현재 화면 해상도: {Screen.width}x{Screen.height}");

            // 1. 현재 윈도우 스타일 확인
            int currentStyle = GetWindowLong(windowHandle, GWL_STYLE);
            int currentExStyle = GetWindowLong(windowHandle, GWL_EXSTYLE);
            
            Debug.Log($"현재 일반 스타일: 0x{currentStyle:X8}");
            Debug.Log($"현재 확장 스타일: 0x{currentExStyle:X8}");
            DebugLogger.LogToFile($"현재 일반 스타일: 0x{currentStyle:X8}");
            DebugLogger.LogToFile($"현재 확장 스타일: 0x{currentExStyle:X8}");

            // 2. 완전한 Borderless 스타일 설정 (핵심!)
            int borderlessStyle = WS_POPUP | WS_VISIBLE;
            int styleResult = SetWindowLong(windowHandle, GWL_STYLE, borderlessStyle);
            
            Debug.Log($"새로운 일반 스타일 설정: 0x{borderlessStyle:X8}");
            Debug.Log($"스타일 변경 결과: {styleResult}");
            DebugLogger.LogToFile($"새로운 일반 스타일 설정: 0x{borderlessStyle:X8} (완전 테두리 제거!)");
            DebugLogger.LogToFile($"스타일 변경 결과: {styleResult}");

            // 3. 윈도우 배경을 NULL로 설정 (GDI 방식)
            SetClassLongPtr(windowHandle, GCL_HBRBACKGROUND, IntPtr.Zero);
            Debug.Log("윈도우 배경 브러시 제거");
            DebugLogger.LogToFile("윈도우 배경 브러시 제거");

            // 4. 레이어드 윈도우 설정
            int newExStyle = currentExStyle | WS_EX_LAYERED;
            int exStyleResult = SetWindowLong(windowHandle, GWL_EXSTYLE, newExStyle);
            Debug.Log($"레이어드 윈도우 설정 완료, 결과: {exStyleResult}");
            DebugLogger.LogToFile($"레이어드 윈도우 설정 완료, 결과: {exStyleResult}");

            // 5. 투명 색상 키 설정 (검은색을 투명으로)
            bool transparentResult = SetLayeredWindowAttributes(windowHandle, 0x00000000, 0, LWA_COLORKEY);
            Debug.Log($"검은색 투명화 설정 결과: {transparentResult}");
            DebugLogger.LogToFile($"검은색 투명화 설정 결과: {transparentResult} (검은색 → 투명)");

            // 6. 항상 위에 표시 + 프레임 강제 업데이트
            bool posResult = SetWindowPos(windowHandle, HWND_TOPMOST, 0, 0, 0, 0, 
                SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED);
            Debug.Log($"윈도우 위치 설정 결과: {posResult}");
            DebugLogger.LogToFile($"윈도우 위치 설정 결과: {posResult}");

            // 7. 윈도우 다시 그리기 강제
            bool invalidateResult = InvalidateRect(windowHandle, IntPtr.Zero, true);
            Debug.Log($"윈도우 다시 그리기 결과: {invalidateResult}");
            DebugLogger.LogToFile($"윈도우 다시 그리기 결과: {invalidateResult}");

            // 8. 최종 스타일 확인
            int finalStyle = GetWindowLong(windowHandle, GWL_STYLE);
            int finalExStyle = GetWindowLong(windowHandle, GWL_EXSTYLE);
            
            Debug.Log($"최종 일반 스타일: 0x{finalStyle:X8}");
            Debug.Log($"최종 확장 스타일: 0x{finalExStyle:X8}");
            DebugLogger.LogToFile($"최종 일반 스타일: 0x{finalStyle:X8}");
            DebugLogger.LogToFile($"최종 확장 스타일: 0x{finalExStyle:X8}");
            
            Debug.Log("=== 완전한 Borderless 윈도우 설정 완료! ===");
            DebugLogger.LogToFile("=== 완전한 Borderless 윈도우 설정 완료! (_, X 버튼 완전 제거) ===");
        }
        catch (Exception e)
        {
            Debug.LogError($"Borderless 윈도우 설정 실패: {e.Message}");
            Debug.LogError($"스택 트레이스: {e.StackTrace}");
            DebugLogger.LogToFile($"Borderless 윈도우 설정 실패: {e.Message}");
            DebugLogger.LogToFile($"스택 트레이스: {e.StackTrace}");
        }
#else
        Debug.Log("에디터에서는 윈도우 설정이 비활성화됩니다.");
        DebugLogger.LogToFile("에디터에서는 윈도우 설정이 비활성화됩니다.");
#endif
    }

    public void EnableClickThrough()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (windowHandle == IntPtr.Zero) return;
        
        try
        {
            int currentStyle = GetWindowLong(windowHandle, GWL_EXSTYLE);
            SetWindowLong(windowHandle, GWL_EXSTYLE, currentStyle | WS_EX_TRANSPARENT);
            isClickThrough = true;
            Debug.Log("클릭 통과 활성화");
        }
        catch (Exception e)
        {
            Debug.LogError($"클릭 통과 활성화 실패: {e.Message}");
        }
#endif
    }

    public void DisableClickThrough()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (windowHandle == IntPtr.Zero) return;
        
        try
        {
            int currentStyle = GetWindowLong(windowHandle, GWL_EXSTYLE);
            SetWindowLong(windowHandle, GWL_EXSTYLE, currentStyle & ~WS_EX_TRANSPARENT);
            isClickThrough = false;
            Debug.Log("클릭 통과 비활성화");
        }
        catch (Exception e)
        {
            Debug.LogError($"클릭 통과 비활성화 실패: {e.Message}");
        }
#endif
    }

    public Vector2 GetMousePosition()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try
        {
            POINT point;
            GetCursorPos(out point);
            return new Vector2(point.x, point.y);
        }
        catch (Exception e)
        {
            Debug.LogError($"마우스 위치 획득 실패: {e.Message}");
            return Input.mousePosition;
        }
#else
        return Input.mousePosition;
#endif
    }

    public Vector2 GetMousePositionInWindow()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (windowHandle == IntPtr.Zero) return Input.mousePosition;
        
        try
        {
            POINT point;
            GetCursorPos(out point);
            ScreenToClient(windowHandle, ref point);
            return new Vector2(point.x, Screen.height - point.y);
        }
        catch (Exception e)
        {
            Debug.LogError($"윈도우 내 마우스 위치 획득 실패: {e.Message}");
            return Input.mousePosition;
        }
#else
        return Input.mousePosition;
#endif
    }

    public bool IsClickThrough => isClickThrough;
}