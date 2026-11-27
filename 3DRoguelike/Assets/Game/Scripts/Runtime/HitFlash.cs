using UnityEngine;

namespace Game
{
    /// <summary>맞으면 잠깐 희게 빛난다. 재질은 바꾸지 않고 프로퍼티 블록으로 발광만 켠다.</summary>
    [RequireComponent(typeof(Health))]
    public sealed class HitFlash : MonoBehaviour
    {
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private Health health;
        private float duration;
        private float endTime;
        private bool flashing;

        public void Configure(float flashTime)
        {
            duration = flashTime;
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            renderers = GetComponentsInChildren<Renderer>(true);
            block = new MaterialPropertyBlock();
            block.SetColor(EmissionId, Color.white);
        }

        private void OnEnable()
        {
            health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            if (flashing) SetFlash(false);
        }

        private void Update()
        {
            if (flashing && Time.time >= endTime) SetFlash(false);
        }

private void OnDamaged(int amount)
        {
            Flash();
        }

        /// <summary>피해 없이 한 번 번쩍인다(보스 등장 연출).</summary>
        public void Flash()
        {
            endTime = Time.time + duration;
            SetFlash(true);
        }

        private void SetFlash(bool on)
        {
            flashing = on;
            foreach (Renderer target in renderers)
            {
                target.SetPropertyBlock(on ? block : null);
            }
        }
    }
}