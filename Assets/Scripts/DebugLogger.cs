using UnityEngine;
using System.IO;
using System;

public class DebugLogger : MonoBehaviour
{
    private static string logFilePath;

    void Awake()
    {
        // 로그 파일 경로 설정 (실행 파일과 같은 폴더)
        logFilePath = Path.Combine(Application.dataPath, "../", "DebugLog.txt");

        // 기존 로그 파일 삭제 (새 세션 시작)
        if (File.Exists(logFilePath))
            File.Delete(logFilePath);

        // Unity 로그를 파일로 리다이렉트
        Application.logMessageReceived += OnLogMessageReceived;

        LogToFile("=== Debug Logger 시작 ===");
        LogToFile($"Unity 버전: {Application.unityVersion}");
        LogToFile($"플랫폼: {Application.platform}");
        LogToFile($"해상도: {Screen.width}x{Screen.height}");
    }

    void OnLogMessageReceived(string logString, string stackTrace, LogType type)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        string logEntry = $"[{timestamp}] [{type}] {logString}";

        if (type == LogType.Exception || type == LogType.Error)
        {
            logEntry += $"\nStackTrace: {stackTrace}";
        }

        LogToFile(logEntry);
    }

    public static void LogToFile(string message)
    {
        try
        {
            using (StreamWriter writer = new StreamWriter(logFilePath, true))
            {
                writer.WriteLine(message);
                writer.Flush();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"로그 파일 쓰기 실패: {e.Message}");
        }
    }

    void OnDestroy()
    {
        Application.logMessageReceived -= OnLogMessageReceived;
        LogToFile("=== Debug Logger 종료 ===");
    }
}