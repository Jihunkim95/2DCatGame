using System;
using System.Runtime.InteropServices;
using UnityEngine;
using System.Collections;

public class WindowManager : MonoBehaviour
{
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    #region Windows API �Լ���
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

    #region ������ �����
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

    #region ����ü
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

    // �̱��� ����
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
        // �ణ�� ���� �� ���� (Unity �ʱ�ȭ �Ϸ� ���)
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
            // ������ �ڵ� ȹ��
            windowHandle = GetActiveWindow();
            
            if (windowHandle == IntPtr.Zero)
            {
                Debug.LogError("������ �ڵ��� ã�� �� �����ϴ�!");
                return;
            }

            Debug.Log($"������ �ڵ� ȹ��: {windowHandle}");

            // 1. �⺻ ������ ��Ÿ�� ����
            int currentStyle = GetWindowLong(windowHandle, GWL_EXSTYLE);
            Debug.Log($"���� ������ ��Ÿ��: {currentStyle:X}");

            // 2. ���̾�� ������ ����
            int newStyle = currentStyle | WS_EX_LAYERED;
            SetWindowLong(windowHandle, GWL_EXSTYLE, newStyle);
            Debug.Log("���̾�� ������ ���� �Ϸ�");

            // 3. ���� ���� - ���� Ű ��İ� ���� ��� ��� �õ�
            // �������� ���� �������� ����
            SetLayeredWindowAttributes(windowHandle, 0x00000000, 0, LWA_COLORKEY);
            Debug.Log("���� Ű ���� ���� �Ϸ�");
            
            // ���� ������ ����
            SetLayeredWindowAttributes(windowHandle, 0, 255, LWA_ALPHA);
            Debug.Log("���� ���� ���� �Ϸ�");

            // 4. DWM Ȯ�� (Windows Vista �̻�)
            MARGINS margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            int result = DwmExtendFrameIntoClientArea(windowHandle, ref margins);
            Debug.Log($"DWM Ȯ�� ���: {result}");

            // 5. �ִϸ��̼� ��Ȱ��ȭ
            int value = 1;
            DwmSetWindowAttribute(windowHandle, DWMWA_TRANSITIONS_FORCEDISABLED, ref value, sizeof(int));

            // 6. �׻� ���� ǥ��
            SetWindowPos(windowHandle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED);
            
            Debug.Log("������ ���� �Ϸ�!");
        }
        catch (Exception e)
        {
            Debug.LogError($"������ ���� ����: {e.Message}");
            Debug.LogError($"���� Ʈ���̽�: {e.StackTrace}");
        }
#else
        Debug.Log("�����Ϳ����� ������ ������ ��Ȱ��ȭ�˴ϴ�. ���� �� �׽�Ʈ�ϼ���.");
#endif
    }

    // ��ü ȭ�� Ŭ�� ��� Ȱ��ȭ
    public void EnableClickThrough()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (windowHandle == IntPtr.Zero) return;
        
        try
        {
            int currentStyle = GetWindowLong(windowHandle, GWL_EXSTYLE);
            SetWindowLong(windowHandle, GWL_EXSTYLE, currentStyle | WS_EX_TRANSPARENT);
            isClickThrough = true;
            
            Debug.Log("Ŭ�� ��� Ȱ��ȭ");
        }
        catch (Exception e)
        {
            Debug.LogError($"Ŭ�� ��� Ȱ��ȭ ����: {e.Message}");
        }
#else
        Debug.Log("�����Ϳ����� Ŭ�� ��� ����� ��Ȱ��ȭ�˴ϴ�.");
#endif
    }

    // Ŭ�� ��� ��Ȱ��ȭ (����� ��ȣ�ۿ� ����)
    public void DisableClickThrough()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (windowHandle == IntPtr.Zero) return;
        
        try
        {
            int currentStyle = GetWindowLong(windowHandle, GWL_EXSTYLE);
            SetWindowLong(windowHandle, GWL_EXSTYLE, currentStyle & ~WS_EX_TRANSPARENT);
            isClickThrough = false;
            
            Debug.Log("Ŭ�� ��� ��Ȱ��ȭ");
        }
        catch (Exception e)
        {
            Debug.LogError($"Ŭ�� ��� ��Ȱ��ȭ ����: {e.Message}");
        }
#else
        Debug.Log("�����Ϳ����� Ŭ�� ��� ����� ��Ȱ��ȭ�˴ϴ�.");
#endif
    }

    // ���콺 ��ġ �������� (��ũ�� ��ǥ)
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
            Debug.LogError($"���콺 ��ġ ȹ�� ����: {e.Message}");
            return Input.mousePosition;
        }
#else
        return Input.mousePosition;
#endif
    }

    // ���콺 ��ġ �������� (������ �� ��ǥ)
    public Vector2 GetMousePositionInWindow()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (windowHandle == IntPtr.Zero) return Input.mousePosition;
        
        try
        {
            POINT point;
            GetCursorPos(out point);
            ScreenToClient(windowHandle, ref point);
            
            // Unity ��ǥ��� ��ȯ (Y�� ����)
            return new Vector2(point.x, Screen.height - point.y);
        }
        catch (Exception e)
        {
            Debug.LogError($"������ �� ���콺 ��ġ ȹ�� ����: {e.Message}");
            return Input.mousePosition;
        }
#else
        return Input.mousePosition;
#endif
    }

    public bool IsClickThrough => isClickThrough;
}