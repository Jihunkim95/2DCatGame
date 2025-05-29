using System;
using System.Runtime.InteropServices;
using UnityEngine;
using System.Collections;

public class WindowManager : MonoBehaviour
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
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    #endregion
#endif

    #region 윈도우 상수들
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_TOPMOST = 0x8;

    private const uint LWA_COLORKEY = 0x1;
    private const uint LWA_ALPHA = 0x2;

    private const int DWMWA_TRANSITIONS_FORCEDISABLED = 3;

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

    [StructLayout(LayoutKind.Sequential)]
    public struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }
    #endregion

    private IntPtr windowHandle;
    private bool isClickThrough = false;

    // 싱글톤 패턴
    public static WindowManager Instance { get; private set; }

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
        // 약간의 지연 후 설정 (Unity 초기화 완료 대기)
        StartCoroutine(SetupWindowDelayed());
    }

    IEnumerator SetupWindowDelayed()
    {
        yield return new WaitForSeconds(0.5f);
        SetupWindow();
    }

    void SetupWindow()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try
        {
            // 윈도우 핸들 획득
            windowHandle = GetActiveWindow();
            
            if (windowHandle == IntPtr.Zero)
            {
                Debug.LogError("윈도우 핸들을 찾을 수 없습니다!");
                return;
            }

            Debug.Log($"윈도우 핸들 획득: {windowHandle}");

            // 1. 기본 윈도우 스타일 설정
            int currentStyle = GetWindowLong(windowHandle, GWL_EXSTYLE);
            Debug.Log($"현재 윈도우 스타일: {currentStyle:X}");

            // 2. 레이어드 윈도우 설정
            int newStyle = currentStyle | WS_EX_LAYERED;
            SetWindowLong(windowHandle, GWL_EXSTYLE, newStyle);
            Debug.Log("레이어드 윈도우 설정 완료");

            // 3. 투명도 설정 - 색상 키 방식과 알파 방식 모두 시도
            // 검은색을 투명 색상으로 설정
            SetLayeredWindowAttributes(windowHandle, 0x00000000, 0, LWA_COLORKEY);
            Debug.Log("색상 키 투명도 설정 완료");
            
            // 알파 투명도도 설정
            SetLayeredWindowAttributes(windowHandle, 0, 255, LWA_ALPHA);
            Debug.Log("알파 투명도 설정 완료");

            // 4. DWM 확장 (Windows Vista 이상)
            MARGINS margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            int result = DwmExtendFrameIntoClientArea(windowHandle, ref margins);
            Debug.Log($"DWM 확장 결과: {result}");

            // 5. 애니메이션 비활성화
            int value = 1;
            DwmSetWindowAttribute(windowHandle, DWMWA_TRANSITIONS_FORCEDISABLED, ref value, sizeof(int));

            // 6. 항상 위에 표시
            SetWindowPos(windowHandle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED);
            
            Debug.Log("윈도우 설정 완료!");
        }
        catch (Exception e)
        {
            Debug.LogError($"윈도우 설정 실패: {e.Message}");
            Debug.LogError($"스택 트레이스: {e.StackTrace}");
        }
#else
        Debug.Log("에디터에서는 윈도우 설정이 비활성화됩니다. 빌드 후 테스트하세요.");
#endif
    }

    // 전체 화면 클릭 통과 활성화
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
#else
        Debug.Log("에디터에서는 클릭 통과 기능이 비활성화됩니다.");
#endif
    }

    // 클릭 통과 비활성화 (고양이 상호작용 가능)
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
#else
        Debug.Log("에디터에서는 클릭 통과 기능이 비활성화됩니다.");
#endif
    }

    // 마우스 위치 가져오기 (스크린 좌표)
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

    // 마우스 위치 가져오기 (윈도우 내 좌표)
    public Vector2 GetMousePositionInWindow()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (windowHandle == IntPtr.Zero) return Input.mousePosition;
        
        try
        {
            POINT point;
            GetCursorPos(out point);
            ScreenToClient(windowHandle, ref point);
            
            // Unity 좌표계로 변환 (Y축 반전)
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