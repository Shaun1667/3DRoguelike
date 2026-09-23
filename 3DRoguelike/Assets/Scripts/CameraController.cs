using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// TPS 스타일 마우스 카메라 회전 스크립트.
///
/// [계층 구조 예시]
/// CameraTarget (이 스크립트를 붙이는 오브젝트, 좌우 회전(Yaw) 담당)
///   └─ CameraPivot (상하 회전(Pitch) 담당, 자식 오브젝트)
///        └─ Main Camera (Pivot의 자식, 뒤로 살짝 빠진 위치)
///
/// CameraTarget은 매 프레임 플레이어의 "위치"만 따라가고, 회전은 마우스 입력으로만 결정한다.
/// 이렇게 분리해두면 나중에 "이동은 카메라가 보는 방향 기준"으로 만들 때
/// 플레이어의 실제 forward 벡터는 그대로 두고, 카메라 오브젝트의 forward만 참고하면 되므로 편하다.
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("따라다닐 대상 (플레이어). 위치만 참고하고 회전은 참고하지 않는다.")]
    [SerializeField] private Transform target;

    [Tooltip("상하 회전(피치)을 담당할 자식 오브젝트. 반드시 이 오브젝트의 자식이어야 한다.")]
    [SerializeField] private Transform pivot;

    [Header("감도")]
    [SerializeField] private float mouseSensitivityX = 0.15f;
    [SerializeField] private float mouseSensitivityY = 0.15f;

    [Header("상하 회전 제한 (도)")]
    [SerializeField] private float minPitch = -40f;
    [SerializeField] private float maxPitch = 75f;

    [Header("옵션")]
    [SerializeField] private bool invertY = false;
    [SerializeField] private bool lockCursor = true;

    private float yaw;
    private float pitch;

    private void Start()
    {
        // 시작 시점의 회전값을 그대로 이어받는다 (씬에서 미리 각도를 잡아둔 경우 대비)
        yaw = transform.eulerAngles.y;

        pitch = pivot.localEulerAngles.x;
        if (pitch > 180f) pitch -= 360f; // 0~360 범위를 -180~180으로 정규화

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void LateUpdate()
    {
        // 1) 위치는 플레이어를 그대로 따라간다 (회전은 따라가지 않음)
        if (target != null)
        {
            transform.position = target.position;
        }

        // 2) 마우스 입력 읽기 (새 Input System)
        //    Mouse.current.delta는 "지난 프레임 이후 움직인 픽셀량"이라 이미 프레임 독립적이다.
        //    따라서 Time.deltaTime을 추가로 곱하지 않는다.
        Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;

        float mouseX = mouseDelta.x * mouseSensitivityX;
        float mouseY = mouseDelta.y * mouseSensitivityY;

        yaw += mouseX;
        pitch += invertY ? mouseY : -mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // 3) 좌우 회전은 이 오브젝트(부모)에, 상하 회전은 pivot(자식)에 적용
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        pivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void OnDisable()
    {
        // 다른 스크립트/에디터 작업을 위해 커서를 원상복구
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    /// <summary>
    /// 나중에 이동 스크립트에서 "카메라가 바라보는 방향"을 참고할 때 사용할 헬퍼.
    /// 예: Vector3 moveDir = cam.GetFlatForward() * v + cam.GetFlatRight() * h;
    /// </summary>
    public Vector3 GetFlatForward()
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        return forward.normalized;
    }

    public Vector3 GetFlatRight()
    {
        Vector3 right = transform.right;
        right.y = 0f;
        return right.normalized;
    }
}