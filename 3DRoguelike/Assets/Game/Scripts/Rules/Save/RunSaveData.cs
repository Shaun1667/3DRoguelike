using System;

namespace Game.Rules
{
    /// <summary>
    /// 저장 파일 한 장의 모양(3-6). 판 상태(<see cref="RunSession"/>)에서 무엇을 담는지는 <see cref="RunSaveRules"/> 한 곳이 정하고,
    /// 이것은 글자(JSON)로 옮기기 쉬운 평평한 칸만 가진다. 맵 · 적 · 보상은 담지 않는다 — 시드로 다시 만든다.
    /// 새 칸 = 필드 한 줄 + <see cref="RunSaveRules.FormatVersion"/> 올림 + 담기 · 되살리기 + 왕복 테스트.
    /// </summary>
    [Serializable]
    public sealed class RunSaveData
    {
        /// <summary>형식 번호. 다르면 읽지 않는다(<see cref="RunSaveRules.FormatVersion"/>).</summary>
        public int version;
        /// <summary>저장한 때(UTC, 보기용). 규칙은 시계를 모르니 화면 쪽이 넣는다.</summary>
        public string savedAtUtc = "";
        /// <summary>왜 저장했나(보기용: "방 깸", "던전 입장", "끔" …).</summary>
        public string reason = "";

        /// <summary>판 시드 코드("XXXX-XXXX").</summary>
        public string seed = "";
        /// <summary>월드 길의 던전 수 · 마지막 던전이 다음 지역을 여는가 — 월드 데이터가 바뀌었으면 이어 하지 않는다.</summary>
        public int dungeonCount;
        public bool lastDungeonOpensNextRegion;
        /// <summary>판 시간(초).</summary>
        public float elapsedSeconds;

        // --- 월드 ---
        /// <summary>깬 던전 수(던전은 순서대로만 깬다).</summary>
        public int dungeonsCleared;
        /// <summary>잡은 길 위 무리.</summary>
        public int[] encountersCleared = Array.Empty<int>();
        /// <summary>월드로 돌아올 때 설 입구. -1이면 길 시작.</summary>
        public int returnDungeon = -1;
        /// <summary>월드에서 저장한 자리(있으면 그곳에서 이어 한다).</summary>
        public bool hasWorldSpot;
        public float worldX, worldY, worldZ, worldYaw;

        // --- 던전 안 ---
        /// <summary>들어가 있는 던전. -1이면 월드.</summary>
        public int currentDungeon = -1;
        /// <summary>그 층의 깰 방 수를 판 기록에 더했는가.</summary>
        public bool battleRoomsCounted;
        /// <summary>선 방. -1이면 층 처음(시작 방).</summary>
        public int currentRoom = -1;
        public int[] clearedRooms = Array.Empty<int>();
        public int[] visitedRooms = Array.Empty<int>();
        /// <summary>보상물을 골라 받은 방.</summary>
        public int[] rewardRoomsTaken = Array.Empty<int>();
        /// <summary>던전에 들어설 때 가졌던 도구(보상 계획을 같게).</summary>
        public string[] entryUnlocks = Array.Empty<string>();

        // --- 플레이어 ---
        /// <summary>체력. -1이면 가득.</summary>
        public int playerHealth = -1;
        /// <summary>탄창에 남은 탄. -1이면 가득.</summary>
        public int playerAmmo = -1;

        // --- 판 기록 ---
        /// <summary>고른 보상(표의 id, 고른 순서). 성향 · 강화는 이것을 다시 반영해 얻는다.</summary>
        public string[] rewardsTaken = Array.Empty<string>();
        /// <summary>가진 도구(권총 등, enum 이름). 디버그로 주고 뺀 것까지 그대로.</summary>
        public string[] unlocks = Array.Empty<string>();
        /// <summary>이 판에 건 성장 트리 노드 id(3-7, 형식 2부터 — 형식 1 파일은 빈 목록). 판 밖 진행 자체는 따로(meta_progress.json).</summary>
        public string[] metaNodes = Array.Empty<string>();
        /// <summary>성향(검사용 — 보상을 다시 반영한 값과 같아야 한다).</summary>
        public int beast, human;
        public int battleRooms, statsDungeonsCleared, roomsCleared, kills;
    }
}
