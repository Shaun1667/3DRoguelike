namespace Game.Rules
{
    /// <summary>
    /// 성향에 따른 모습. 지금은 Beast가 높으면 Mutant 동작(걷기 · 달리기 · 점프)으로 바뀐다(결정 2026-11-11, 사용자).
    /// 한 번 바뀌면 그 판에서는 돌아가지 않는다 — 그 기억은 화면 쪽(<c>PlayerForm</c>)이 갖는다.
    /// </summary>
    public static class TendencyForm
    {
        /// <summary>Beast가 기준 이상이고 Human보다 높으면 true.</summary>
        public static bool ReachesMutant(int beast, int human, int beastThreshold)
        {
            return beast >= beastThreshold && beast > human;
        }
    }
}
