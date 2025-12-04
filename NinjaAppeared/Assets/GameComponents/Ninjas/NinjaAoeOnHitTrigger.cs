using UnityEngine;

/// <summary>
/// 닌자에 부착되어 적과 충돌(또는 트리거 접촉) 시 해당 지점을 기준으로 AoE 처치를 수행합니다.
/// 기본 타겟 처치는 다른 시스템(예: ProtractorController)이 담당하며, 본 컴포넌트는 콤보를 중복 증가시키지 않기 위해 기본 타겟에 대한 처치/콤보를 수행하지 않습니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Transform))]
public class NinjaAoeOnHitTrigger : MonoBehaviour
{
    [Tooltip("(더 이상 사용 안 함) 기본 적 처치는 다른 시스템에서 처리합니다.")]
    public bool killPrimaryIfNotHandled = false; // 안전을 위해 기본값 false로 유지

    private NinjaAoeOnHit _aoe;
    private int _lastFrameHandled = -1;

    void Awake()
    {
        _aoe = GetComponent<NinjaAoeOnHit>();
        if (_aoe == null) _aoe = gameObject.AddComponent<NinjaAoeOnHit>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryHandleHit(collision.collider, TryGetContactPoint(collision));
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Trigger에는 접촉점 정보가 없으므로 근사값 사용
        Vector2 cp = other.ClosestPoint(transform.position);
        TryHandleHit(other, cp);
    }

    private void TryHandleHit(Collider2D other, Vector2 contactPoint)
    {
        if (other == null) return;
        // 동일 프레임 과도한 중복 실행 방지(충돌/트리거가 동시 발생하는 경우)
        if (_lastFrameHandled == Time.frameCount) return;

        // EnemyControler 탐색 (자식 포함)
        var enemy = other.GetComponentInParent<EnemyControler>();
        if (enemy == null) return;

        // 기본 타겟 처치 및 콤보 증가는 본 스크립트에서 수행하지 않음 (중복 방지)
        _lastFrameHandled = Time.frameCount;
        if (_aoe != null)
        {
            // 요구사항: 부딪친 적의 처리가 끝난 다음 범위 죽음 체크 실행
            // => 주 처리(예: ProtractorController.HandleHitEnemy)가 완료되어 IsDead가 true가 된 후 AoE 처리
            StartCoroutine(WaitAndProcessAoeAfterPrimary(contactPoint, enemy));
        }
    }

    private System.Collections.IEnumerator WaitAndProcessAoeAfterPrimary(Vector2 contactPoint, EnemyControler primary)
    {
        // 물리 스텝 종료까지 한 번 대기하여 다른 충돌 핸들러들이 먼저 실행되도록 함
        yield return new WaitForFixedUpdate();

        // 최대 대기 시간 동안 주 대상의 사망 처리가 완료되기를 기다림
        const float timeout = 0.5f; // 안전 타임아웃
        float elapsed = 0f;
        while (primary != null && !primary.IsDead && elapsed < timeout)
        {
            yield return null; // 다음 프레임까지 대기
            elapsed += Time.deltaTime;
        }

        // 주 대상 생존/사망 여부와 무관하게 마지막에 AoE 실행(사양에 따라 조정 가능)
        if (_aoe != null)
        {
            _aoe.ProcessAoe(contactPoint, primary);
        }
    }

    private static Vector2 TryGetContactPoint(Collision2D collision)
    {
        if (collision == null) return collision.transform.position;
        if (collision.contactCount > 0)
        {
            return collision.GetContact(0).point;
        }
        // Fallback: 두 콜라이더 간 최단점 사용
        var a = collision.otherCollider; // this
        var b = collision.collider; // other
        if (a != null && b != null)
        {
            ColliderDistance2D d = a.Distance(b);
            if (d.isOverlapped)
            {
                return d.pointA; // this collider 쪽 접촉점 근사
            }
        }
        return collision.transform.position;
    }
}
