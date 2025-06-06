using UnityEngine;
using UnityEngine.UI;

public class BGMManager : MonoBehaviour
{
    [Header("BGM 설정")]
    public AudioSource bgmAudioSource;  // BGM 오디오 소스
    public Button bgmToggleButton;      // BGM 토글 버튼

    [Header("버튼 이미지")]
    public Image buttonImage;           // 버튼의 Image 컴포넌트
    public Sprite bgmOnSprite;          // BGM ON 상태 이미지
    public Sprite bgmOffSprite;         // BGM OFF 상태 이미지

    [Header("버튼 텍스트 (선택사항)")]
    public Text buttonText;             // 버튼의 텍스트 컴포넌트

    [Header("BGM 상태")]
    public bool isBGMOn = true;         // BGM 상태 (기본값: 켜짐)

    void Start()
    {
        // BGM AudioSource가 할당되지 않았다면 자동으로 찾기
        if (bgmAudioSource == null)
        {
            bgmAudioSource = FindObjectOfType<AudioSource>();
        }

        // 버튼 이미지가 할당되지 않았다면 자동으로 찾기
        if (buttonImage == null && bgmToggleButton != null)
        {
            buttonImage = bgmToggleButton.GetComponent<Image>();
        }

        // 버튼 클릭 이벤트 연결
        if (bgmToggleButton != null)
        {
            bgmToggleButton.onClick.AddListener(ToggleBGM);
        }

        // 초기 상태 설정
        UpdateBGMState();
        UpdateButtonImage();
        UpdateButtonText();
    }

    // BGM on/off 토글 함수
    public void ToggleBGM()
    {
        isBGMOn = !isBGMOn;
        UpdateBGMState();
        UpdateButtonImage();
        UpdateButtonText();

        // PlayerPrefs에 상태 저장 (게임 재시작 시에도 설정 유지)
        PlayerPrefs.SetInt("BGM_Enabled", isBGMOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    // BGM 상태 업데이트
    private void UpdateBGMState()
    {
        if (bgmAudioSource != null)
        {
            if (isBGMOn)
            {
                bgmAudioSource.UnPause();
                bgmAudioSource.mute = false;
            }
            else
            {
                bgmAudioSource.Pause();
                // 또는 bgmAudioSource.mute = true; 를 사용해도 됩니다
            }
        }
    }

    // 버튼 이미지 업데이트
    private void UpdateButtonImage()
    {
        if (buttonImage != null)
        {
            if (isBGMOn && bgmOnSprite != null)
            {
                buttonImage.sprite = bgmOnSprite;
            }
            else if (!isBGMOn && bgmOffSprite != null)
            {
                buttonImage.sprite = bgmOffSprite;
            }
        }
    }

    // 버튼 텍스트 업데이트
    private void UpdateButtonText()
    {
        if (buttonText != null)
        {
            buttonText.text = isBGMOn ? "BGM OFF" : "BGM ON";
        }
    }

    // 게임 시작 시 저장된 설정 불러오기
    void Awake()
    {
        // 저장된 BGM 설정 불러오기 (기본값: 1=켜짐)
        isBGMOn = PlayerPrefs.GetInt("BGM_Enabled", 1) == 1;
    }

    // BGM 볼륨 설정 함수 (추가 기능)
    public void SetBGMVolume(float volume)
    {
        if (bgmAudioSource != null)
        {
            bgmAudioSource.volume = Mathf.Clamp01(volume);
        }
    }

    // BGM 상태 확인 함수
    public bool IsBGMEnabled()
    {
        return isBGMOn;
    }
}