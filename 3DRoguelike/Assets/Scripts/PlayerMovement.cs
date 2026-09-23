using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 카메라가 바라보는 방향을 기준으로 이동하는 TPS 스타일 무브먼트.
///
/// - 캐릭터는 항상 "카메라가 보는 방향"을 그대로 바라본다 (faceCameraDirection = true).
/// - W/S는 카메라 정면/후면, A/D는 카메라 기준 좌/우로 이동한다.
/// - 즉 A만 누르면 캐릭터는 정면을 바라본 채로 옆으로 스트레이프(strafe) 이동한다.
///
/// CameraController.cs의 GetFlatForward() / GetFlatRight()를 그대로 사용한다.
/// Player 오브젝트에는 Character Controller 컴포넌트가 필요하다.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("씬에 있는 CameraTarget에 붙인 CameraController를 드래그해서 연결")]
    [SerializeField] private CameraController cameraController;

    [Header("이동 설정")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -9.81f;

    [Header("점프 설정")]
    [Tooltip("점프 시 도달하는 최대 높이 (미터)")]
    [SerializeField] private float jumpHeight = 1.2f;

    [Header("회전 설정")]
    [Tooltip("true면 캐릭터가 항상 카메라 방향을 바라본다 (A/D는 옆으로 스트레이프).")]
    [SerializeField] private bool faceCameraDirection = true;

    private CharacterController controller;
    private float verticalVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        Vector2 input = ReadMoveInput();

        // 1) 카메라 기준 수평 forward/right 벡터를 가져온다 (y성분 제거된 상태)
        Vector3 camForward = cameraController.GetFlatForward();
        Vector3 camRight = cameraController.GetFlatRight();

        // 2) 입력을 카메라 기준 방향으로 변환
        Vector3 moveDir = camForward * input.y + camRight * input.x;
        if (moveDir.sqrMagnitude > 1f)
        {
            moveDir.Normalize();
        }

        // 3) 캐릭터를 카메라가 보는 방향으로 정렬 (이동 입력과 무관하게 항상 적용 -> 스트레이프 이동)
        if (faceCameraDirection)
        {
            transform.rotation = Quaternion.Euler(0f, cameraController.transform.eulerAngles.y, 0f);
        }

        // 4) 중력 & 점프 처리
        bool isGrounded = controller.isGrounded;

        if (isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f; // 바닥에 붙어있게 하는 최소값
        }

        // 바닥에 닿아있을 때만 스페이스바로 점프 (공중 점프 방지)
        if (isGrounded && ReadJumpPressed())
        {
            // v = sqrt(h * -2 * g) : 원하는 최대 높이(jumpHeight)에 도달하는 데 필요한 초기 속도
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        verticalVelocity += gravity * Time.deltaTime;

        // 5) 최종 이동 적용
        Vector3 velocity = moveDir * moveSpeed;
        velocity.y = verticalVelocity;

        controller.Move(velocity * Time.deltaTime);
    }

    /// <summary>
    /// 새 Input System의 Keyboard를 직접 폴링해서 WASD를 -1~1 범위의 Vector2로 반환한다.
    /// x: A(-1) / D(+1), y: S(-1) / W(+1)
    /// </summary>
    private Vector2 ReadMoveInput()
    {
        Vector2 input = Vector2.zero;
        Keyboard kb = Keyboard.current;
        if (kb == null) return input;

        if (kb.wKey.isPressed) input.y += 1f;
        if (kb.sKey.isPressed) input.y -= 1f;
        if (kb.dKey.isPressed) input.x += 1f;
        if (kb.aKey.isPressed) input.x -= 1f;

        return input;
    }

    /// <summary>
    /// 스페이스바가 "이번 프레임에 새로 눌렸는지"를 반환한다.
    /// isPressed 대신 wasPressedThisFrame을 써야 키를 계속 누르고 있어도
    /// 매 프레임 점프가 발동하지 않고 한 번만 발동한다.
    /// </summary>
    private bool ReadJumpPressed()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return false;

        return kb.spaceKey.wasPressedThisFrame;
    }
}