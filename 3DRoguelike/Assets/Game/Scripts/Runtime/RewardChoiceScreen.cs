using System;
using System.Collections.Generic;
using Game.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// 보상물을 주우면 뜨는 선택 화면. 선택지 카드(최대 3장)와 지금 Beast/Human 성향을 보여 주고, 클릭 또는 1 · 2 · 3으로 고른다.
    /// 떠 있는 동안 게임 시간은 멈추고 커서가 풀린다.
    /// </summary>
    public sealed class RewardChoiceScreen : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text header;
        [SerializeField] private Button[] cards;
        [SerializeField] private TMP_Text[] cardTexts;

        private IReadOnlyList<RewardDefinition> offers;
        private Action<RewardDefinition> onChosen;

        public bool IsShown => panel.activeSelf;
        public string HeaderText => header.text;

        private void Awake()
        {
            panel.SetActive(false);
            for (int i = 0; i < cards.Length; i++)
            {
                int index = i;
                cards[i].onClick.AddListener(() => Choose(index));
            }
        }

        public void Show(IReadOnlyList<RewardDefinition> rewardOffers, RunStats stats, bool rare, Action<RewardDefinition> chosen)
        {
            offers = rewardOffers;
            onChosen = chosen;
            header.text = (rare ? "희귀 보상" : "보상") + $" — 하나를 고르세요\n지금 성향  Beast {stats.Beast} · Human {stats.Human}";
            for (int i = 0; i < cards.Length; i++)
            {
                bool used = i < offers.Count;
                cards[i].gameObject.SetActive(used);
                if (!used) continue;
                RewardDefinition reward = offers[i];
                cardTexts[i].text = $"<size=130%><b>{reward.Name}</b></size>\n\n{reward.Description}\n\n{reward.Tendency} +{reward.Amount}\n<size=70%>[{i + 1}]</size>";
            }
            panel.SetActive(true);
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>고르기(키 · 클릭 · 확인용 코드). 범위를 벗어나면 무시한다.</summary>
        public void Choose(int index)
        {
            if (!IsShown || offers == null || index < 0 || index >= offers.Count) return;
            panel.SetActive(false);
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Action<RewardDefinition> callback = onChosen;
            onChosen = null;
            callback?.Invoke(offers[index]);
        }

        private void Update()
        {
            if (!IsShown || Keyboard.current == null) return;
            if (Keyboard.current.digit1Key.wasPressedThisFrame) Choose(0);
            else if (Keyboard.current.digit2Key.wasPressedThisFrame) Choose(1);
            else if (Keyboard.current.digit3Key.wasPressedThisFrame) Choose(2);
        }
    }
}
