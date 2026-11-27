using System;
using System.Collections.Generic;

namespace Game.Rules
{
    /// <summary>
    /// 판 밖 진행 파일 한 장의 모양(3-7). 판 저장(<see cref="RunSaveData"/>)과 따로 — 판이 끝나도 지우지 않는다.
    /// 새 칸 = 필드 한 줄 + <see cref="MetaSaveRules.FormatVersion"/> 올림 + 왕복 테스트.
    /// </summary>
    [Serializable]
    public sealed class MetaSaveData
    {
        public int version;
        /// <summary>저장한 때(UTC, 보기용).</summary>
        public string savedAtUtc = "";
        public int currency;
        public int spent;
        public int totalEarned;
        /// <summary>끝난 판 수(재생 횟수).</summary>
        public int runs;
        /// <summary>산 노드 id(트리 표 순서).</summary>
        public string[] owned = Array.Empty<string>();
    }

    /// <summary>판 밖 진행을 담고(<see cref="Capture"/>) 되살리는(<see cref="TryRestore"/>) 규칙 한 곳.</summary>
    public static class MetaSaveRules
    {
        public const int FormatVersion = 1;

        public static MetaSaveData Capture(MetaProgress progress, MetaTree tree)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (tree == null) throw new ArgumentNullException(nameof(tree));
            var ids = new List<string>();
            foreach (MetaNode node in progress.OwnedNodes(tree)) ids.Add(node.Id);
            // 트리에 없는 노드(표에서 지운 줄)는 담지 않는다
            return new MetaSaveData
            {
                version = FormatVersion,
                currency = progress.Currency,
                spent = progress.Spent,
                totalEarned = progress.TotalEarned,
                runs = progress.Runs,
                owned = ids.ToArray(),
            };
        }

        /// <summary>
        /// 되살린다. 형식 번호가 다르거나 수가 음수 · 목록이 빠졌으면 false(깨진 파일 — 백업 뒤 새로).
        /// 트리에 없는 노드 id(표에서 지운 줄)는 빼고 <paramref name="dropped"/>에 적는다 — 재화는 그대로.
        /// </summary>
        public static bool TryRestore(MetaSaveData data, MetaTree tree, out MetaProgress progress, out List<string> dropped, out string error)
        {
            if (tree == null) throw new ArgumentNullException(nameof(tree));
            progress = null;
            dropped = new List<string>();
            error = null;
            if (data == null) { error = "비어 있음"; return false; }
            if (data.version != FormatVersion) { error = $"형식 번호 {data.version} (읽는 번호 {FormatVersion})"; return false; }
            if (data.currency < 0 || data.spent < 0 || data.totalEarned < 0 || data.runs < 0) { error = "수가 음수"; return false; }
            if (data.owned == null) { error = "목록이 빠짐"; return false; }
            var ids = new List<string>();
            foreach (string id in data.owned)
            {
                if (tree.TryGet(id, out _)) { if (!ids.Contains(id)) ids.Add(id); }
                else dropped.Add(id ?? "(null)");
            }
            progress = new MetaProgress();
            progress.Load(data.currency, data.spent, data.totalEarned, data.runs, ids);
            return true;
        }
    }
}
