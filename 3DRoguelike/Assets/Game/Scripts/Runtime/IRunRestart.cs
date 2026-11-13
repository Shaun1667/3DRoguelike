namespace Game
{
    /// <summary>결과 화면의 "새 판 · 같은 시드" 버튼이 부르는 쪽(월드 · 층).</summary>
    public interface IRunRestart
    {
        void Restart(bool sameSeed);
    }
}
