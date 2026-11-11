using System;
using Game.Rules;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 플레이어가 읽는 판 안 강화. 값은 규칙 <see cref="UpgradeSheet"/> 한 곳에서 오고, 층에서는 <see cref="FloorRun"/>이
    /// 판 기록의 강화 합계를 넘긴다. 시험 방처럼 판이 없으면 빈 합계(배율 1)를 쓴다.
    /// </summary>
    public sealed class PlayerUpgrades : MonoBehaviour
    {
        private UpgradeSheet sheet = new UpgradeSheet();

        /// <summary>강화가 바뀌었거나 다른 합계로 바뀐 순간.</summary>
        public event Action Changed;

        public UpgradeSheet Sheet => sheet;

        public float Scale(UpgradeStat stat) => sheet.Scale(stat);
        public float Bonus(UpgradeStat stat) => sheet.Bonus(stat);
        public int ScaleInt(UpgradeStat stat, int baseValue) => sheet.ScaleInt(stat, baseValue);

        /// <summary>이 판의 강화 합계를 쓴다.</summary>
        public void Use(UpgradeSheet runSheet)
        {
            if (runSheet == null) throw new ArgumentNullException(nameof(runSheet));
            sheet.Changed -= OnSheetChanged;
            sheet = runSheet;
            sheet.Changed += OnSheetChanged;
            Changed?.Invoke();
        }

        private void Awake()
        {
            // FloorRun.Awake가 먼저 Use를 불렀을 수 있어 한 번만 구독되게 뺐다가 넣는다
            sheet.Changed -= OnSheetChanged;
            sheet.Changed += OnSheetChanged;
        }

        private void OnDestroy()
        {
            sheet.Changed -= OnSheetChanged;
        }

        private void OnSheetChanged() => Changed?.Invoke();
    }
}
