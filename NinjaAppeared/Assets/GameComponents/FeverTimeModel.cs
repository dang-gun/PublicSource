using System;
using UnityEngine;

/// <summary>
/// Fever Time 상태와 점수(게이지)를 관리하는 모델.
/// 최대 점수(게이지)가 100에 도달하면 피버 타임이 활성화되고, 지속 시간 종료 후 자동 종료됩니다.
/// </summary>
public class FeverTimeModel
{
    /// <summary>전역 접근을 위한 단순 싱글턴 인스턴스.</summary>
    public static readonly FeverTimeModel Instance = new FeverTimeModel();

    // 주의: ProtractorController 등 다른 코드에서 'new FeverTimeModel()'을 사용할 수 있으므로
    // 생성자를 공개로 유지합니다. 가능하면 전역 동기화를 위해 Instance를 사용하세요.
    public FeverTimeModel() { }

    /// <summary>최대 피버 점수.</summary>
    public const int MaxScore = 100;

    /// <summary>킬 당 기본 증가량.</summary>
    public int ScorePerKill = 1;

    /// <summary>피버 타임 지속 시간(초).</summary>
    public float DurationSeconds = 6f;

    /// <summary>현재 누적 점수 (0~MaxScore).</summary>
    public int CurrentScore { get; private set; }

    /// <summary>피버 타임 진행 중 여부.</summary>
    public bool IsActive { get; private set; }

    private float _remain;

    /// <summary>피버 타임 시작 시 호출되는 이벤트.</summary>
    public event Action OnActivated;
    /// <summary>피버 타임 종료 시 호출되는 이벤트.</summary>
    public event Action OnEnded;
    /// <summary>점수 변경 시 호출 (현재점수, 최대점수).</summary>
    public event Action<int,int> OnScoreChanged;

    /// <summary>점수를 추가합니다. 활성 중이면 무시(게이지 고정).</summary>
    public void AddScore(int value)
    {
        if (value <= 0) return;
        if (IsActive) return; // 활성 중에는 게이지 증가 없음 (종료 후 다시 채움)
        int next;
        try { next = checked(CurrentScore + value); }
        catch { next = int.MaxValue; }
        CurrentScore = Mathf.Clamp(next, 0, MaxScore);
        OnScoreChanged?.Invoke(CurrentScore, MaxScore);
        if (CurrentScore >= MaxScore)
        {
            Activate();
        }

        //Debug.Log("[Fever] Score Added: " + value + " => " + CurrentScore + "/" + MaxScore);
    }

    /// <summary>프레임별 업데이트. FeverTime 진행 시간 관리.</summary>
    public void Update(float deltaTime)
    {
        if (!IsActive) return;
        _remain -= deltaTime;
        if (_remain <= 0f)
        {
            End();
        }
    }

    private void Activate()
    {
        if (IsActive) return;
        IsActive = true;
        _remain = DurationSeconds;
        OnActivated?.Invoke();
        // 활성화 시 가득 찬 상태 반영 (100)
        OnScoreChanged?.Invoke(CurrentScore, MaxScore);
        if (GameConstants.DebugIs)
        {
            Debug.Log(" *** [Fever] Activated (Duration=" + DurationSeconds + "s)");
        }
    }

    private void End()
    {
        if (!IsActive) return;
        IsActive = false;
        CurrentScore = 0; // 종료 후 게이지 리셋
        OnEnded?.Invoke();
        OnScoreChanged?.Invoke(CurrentScore, MaxScore);
        if (GameConstants.DebugIs)
        {
            Debug.Log(" *** [Fever] Ended");
        }
    }

    /// <summary>
    /// 상태 초기화: 피버 타임 종료 및 점수 초기화.
    /// </summary>
    public void Reset()
    {
        bool wasActive = IsActive;
        IsActive = false;
        _remain = 0f;
        CurrentScore = 0;
        // 이전에 활성화되어 있었다면 종료 이벤트 발행
        if (wasActive)
        {
            OnEnded?.Invoke();
        }
        OnScoreChanged?.Invoke(CurrentScore, MaxScore);
    }
}
