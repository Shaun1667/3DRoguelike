namespace Game
{
    /// <summary>피해를 받을 수 있는 대상.</summary>
    public interface IDamageable
    {
        void TakeDamage(int amount);
    }
}