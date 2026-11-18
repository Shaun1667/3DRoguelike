using UnityEngine;

namespace Game
{
    /// <summary>
    /// 고라니 털(셸 방식) 조절 값. 메뉴 Tools/Game/Setup Gorani Fur가 이 값으로 털 메시를 굽고 털 재질에 넣는다.
    /// 겹 수 · 털 나는 범위(목 비중 · 털 끝 범위)는 메시를 다시 구워야 바뀌므로 메뉴를 다시 실행한다.
    /// 나머지 값은 에디터에서 고치면 바로 털 재질에 반영된다(OnValidate).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Fur Settings", fileName = "GoraniFur")]
    public sealed class FurSettings : ScriptableObject
    {
        [Header("메시(바꾸면 메뉴 다시 실행)")]
        [Tooltip("셸 겹 수. 털 메시 정점 = 털 범위 정점 × 겹 수. 많을수록 촘촘하지만 무겁다.")]
        [Range(4, 32)] public int shellCount = 12;
        [Tooltip("목 뼈 가중치를 얼마만큼 털로 칠지(0 = 목 털 없음, 1 = 머리와 같게). 머리와 몸통 사이를 부드럽게 잇는다.")]
        [Range(0f, 1f)] public float neckWeight = 0.5f;
        [Tooltip("손 · 손가락에도 털을 낼지. 끄면 머리(목 위)에만 난다. handLength는 켤 때만 쓴다.")]
        public bool includeHands = false;
        [Tooltip("손 · 손가락 털 길이(머리 털 길이에 대한 비율). 손은 작아서 머리보다 짧게 둔다.")]
        [Range(0f, 1f)] public float handLength = 0.45f;

        [Header("모양")]
        [Tooltip("털 길이(미터, 월드 기준).")]
        [Range(0.001f, 0.05f)] public float length = 0.007f;
        [Tooltip("털 밀도: UV 1칸에 놓이는 털 가닥 수(한 줄). 고라니 UV 1칸 ≈ 2 m라 1000이면 가닥 사이 약 2 mm.")]
        [Range(100f, 4000f)] public float density = 1400f;
        [Tooltip("가닥 굵기(칸 크기에 대한 비율).")]
        [Range(0.2f, 2f)] public float thickness = 0.95f;
        [Tooltip("끝 가늘어짐(0 = 원기둥, 1 = 끝이 뾰족).")]
        [Range(0f, 1f)] public float tipThinning = 0.75f;
        [Tooltip("가닥 길이 들쭉날쭉한 정도(0 = 모두 같은 길이).")]
        [Range(0f, 1f)] public float lengthJitter = 0.5f;
        [Tooltip("이 밝기보다 어두운 알베도(눈 · 코 · 입)에는 털을 내지 않는다.")]
        [Range(0f, 0.3f)] public float darkCutoff = 0.11f;

        [Header("움직임")]
        [Tooltip("중력 처짐(털 길이에 대한 비율).")]
        [Range(0f, 1f)] public float gravity = 0.25f;
        [Tooltip("바람 흔들림 세기(털 길이에 대한 비율).")]
        [Range(0f, 1f)] public float windStrength = 0.15f;
        [Tooltip("바람 흔들림 빠르기.")]
        [Range(0f, 10f)] public float windSpeed = 2f;

        [Header("빛")]
        [Tooltip("털 뿌리 쪽을 어둡게 하는 정도(셸 AO).")]
        [Range(0f, 1f)] public float occlusion = 0.45f;
        [Tooltip("가장자리 림 라이트 세기.")]
        [Range(0f, 1f)] public float rim = 0.2f;
        [Tooltip("털 색에 곱하는 색(알베도 텍스처를 따른다).")]
        public Color tint = Color.white;

        [Header("연결")]
        [Tooltip("값을 넣을 털 재질. 메뉴가 채운다.")]
        public Material material;

        public void ApplyTo(Material target)
        {
            if (target == null) return;
            target.SetFloat("_ShellCount", shellCount);
            target.SetFloat("_FurLength", length);
            target.SetFloat("_FurDensity", density);
            target.SetFloat("_FurThickness", thickness);
            target.SetFloat("_FurThinning", tipThinning);
            target.SetFloat("_FurJitter", lengthJitter);
            target.SetFloat("_DarkCutoff", darkCutoff);
            target.SetFloat("_Gravity", gravity);
            target.SetFloat("_WindStrength", windStrength);
            target.SetFloat("_WindSpeed", windSpeed);
            target.SetFloat("_Occlusion", occlusion);
            target.SetFloat("_Rim", rim);
            target.SetColor("_BaseColor", tint);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (material == null) return;
            ApplyTo(material);
            UnityEditor.EditorUtility.SetDirty(material);
        }
#endif
    }
}
