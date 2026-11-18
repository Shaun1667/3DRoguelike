using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 고라니 손 모양 표. 값은 Humanoid 손가락 머슬(-1 완전히 굽힘 ~ +1 젖힘, 고라니는 약 0.7 ~ 0.8이 곧게 편 손가락)이고 양손이 같은 값을 쓴다.
    /// Setup Test Room이 이 값으로 손가락 포즈 클립을 다시 만든다(HandPoseSetup) — 값을 바꾸면 메뉴를 다시 실행한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Hand Pose Table")]
    public sealed class HandPoseTable : ScriptableObject
    {
        /// <summary>손가락 하나. 첫 마디 · 벌림 · 둘째 마디 · 끝 마디.</summary>
        [Serializable]
        public struct Finger
        {
            public float stretch1;
            public float spread;
            public float stretch2;
            public float stretch3;

            public Finger(float stretch1, float spread, float stretch2, float stretch3)
            {
                this.stretch1 = stretch1;
                this.spread = spread;
                this.stretch2 = stretch2;
                this.stretch3 = stretch3;
            }
        }

        [Serializable]
        public struct Pose
        {
            public Finger thumb;
            public Finger index;
            public Finger middle;
            public Finger ring;
            public Finger little;

            public Finger this[int finger]
            {
                get
                {
                    switch (finger)
                    {
                        case 0: return thumb;
                        case 1: return index;
                        case 2: return middle;
                        case 3: return ring;
                        default: return little;
                    }
                }
            }
        }

        [Tooltip("평소 손(양손): 힘을 빼고 살짝 쥔 모양. 걷기 · 달리기 · 대기 등 모든 동작 위에 덮는다")]
        public Pose relaxed = new Pose
        {
            thumb = new Finger(-1.3f, -0.5f, 0f, 0.2f),
            index = new Finger(0.07f, -0.3f, -0.08f, 0.37f),
            middle = new Finger(-0.03f, -1f, -0.19f, 0.32f),
            ring = new Finger(-0.13f, -0.7f, -0.3f, 0.25f),
            little = new Finger(-0.23f, -0.3f, -0.35f, 0.21f),
        };

        [Tooltip("권총을 쥔 손(오른손, 조준 중). 왼손은 평소 손 그대로")]
        public Pose grip = new Pose
        {
            thumb = new Finger(-1.3f, -0.6f, -0.2f, -0.2f),
            index = new Finger(0.27f, -0.5f, -0.19f, 0.37f),
            middle = new Finger(-0.73f, -1f, -0.97f, -0.08f),
            ring = new Finger(-0.73f, -0.8f, -0.97f, -0.08f),
            little = new Finger(-0.73f, -0.6f, -0.97f, -0.08f),
        };

        [Tooltip("할퀴는 손(양손, 근접 공격 중): 손가락을 벌리고 갈퀴처럼 굽힘")]
        public Pose claw = new Pose
        {
            thumb = new Finger(-0.6f, 0.2f, -0.3f, -0.3f),
            index = new Finger(0.5f, 0.3f, -0.4f, -0.3f),
            middle = new Finger(0.5f, -0.6f, -0.4f, -0.3f),
            ring = new Finger(0.5f, -0.2f, -0.4f, -0.3f),
            little = new Finger(0.5f, 0.3f, -0.4f, -0.3f),
        };

        [Tooltip("손 모양이 바뀌는 빠르기(초당, 10 = 0.1초에 걸쳐 바뀜)")]
        public float blendSpeed = 10f;

        [Tooltip("조준 중 오른손을 조준 방향 축으로 돌리는 각(도). 0이면 손바닥이 아래, -90이면 손바닥이 안쪽(엄지가 위)")]
        public float gripRoll = -90f;
    }
}
