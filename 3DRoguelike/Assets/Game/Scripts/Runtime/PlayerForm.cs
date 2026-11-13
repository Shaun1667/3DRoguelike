using System;
using Game.Rules;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 고라니의 모습. Beast가 높아지면 애니메이터를 Mutant 컨트롤러로 바꿔 걷기 · 달리기 · 점프가 Mutant 동작이 된다.
    /// 판단은 규칙 <see cref="TendencyForm"/>, 기준은 <c>PlayerTuning.mutantFormBeast</c>(BALANCE 1번). 한 번 바뀌면 그 판에서는 그대로다.
    /// 두 컨트롤러는 같은 상태 · 매개변수로 만들어져서(TestRoomSetup) 바꿔도 조작 코드는 그대로 동작한다.
    /// </summary>
    public sealed class PlayerForm : MonoBehaviour
    {
        [SerializeField] private PlayerTuning tuning;
        [SerializeField] private Animator animator;
        [SerializeField] private RuntimeAnimatorController mutantController;

        public bool IsMutant { get; private set; }

        /// <summary>Mutant 모습이 된 순간.</summary>
        public event Action BecameMutant;

        /// <summary>보상을 고른 뒤 부른다. 조건을 넘었으면 모습을 바꾸고 true.</summary>
        public bool UpdateForm(int beast, int human)
        {
            if (IsMutant || !TendencyForm.ReachesMutant(beast, human, tuning.mutantFormBeast)) return false;
            BecomeMutant();
            return true;
        }

        /// <summary>바로 Mutant 모습으로(디버그 · 확인용도 이 길로).</summary>
        public void BecomeMutant()
        {
            if (IsMutant) return;
            if (mutantController == null)
            {
                Debug.LogWarning("[PlayerForm] Mutant 컨트롤러가 없습니다. Tools/Game/Setup Test Room을 다시 실행하세요.");
                return;
            }
            IsMutant = true;
            animator.runtimeAnimatorController = mutantController;
            Debug.Log("[PlayerForm] Beast 성향이 높아져 Mutant 동작으로 바뀜");
            BecameMutant?.Invoke();
        }
    }
}
