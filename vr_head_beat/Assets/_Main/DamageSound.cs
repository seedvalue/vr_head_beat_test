using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Проигрывает звук урона в зависимости от силы удара.
/// Использует три AudioSource для избежания прерывания звуков.
/// </summary>
public class DamageSound : MonoBehaviour
{
    [Tooltip("Список звуков от самого слабого к самому сильному.")]
    public List<AudioClip> damageClips = new List<AudioClip>();

    [Tooltip("Минимальная сила удара (0-1) с которой начинает проигрываться звук.")]
    [Range(0f, 1f)]
    public float threshold = 0.3f;

    [SerializeField] private AudioSource[] audioSources = new AudioSource[3];
    private int currentSourceIndex = 0;

    private void Awake()
    {
        // Если в инспекторе не назначены AudioSource — создаём их
        for (int i = 0; i < audioSources.Length; i++)
        {
            if (audioSources[i] == null)
            {
                GameObject go = new GameObject($"DamageAudioSource_{i}");
                go.transform.SetParent(transform);
                audioSources[i] = go.AddComponent<AudioSource>();
            }
        }
    }

    /// <summary>
    /// Применить урон и воспроизвести соответствующий звук.
    /// </summary>
    /// <param name="force">Сила удара от 0 до 1.</param>
    public void ApplyDamage(float force)
    {
        if (force < threshold || damageClips == null || damageClips.Count == 0)
            return;

        float t = Mathf.InverseLerp(threshold, 1f, force);
        int index = Mathf.FloorToInt(t * (damageClips.Count - 1));
        index = Mathf.Clamp(index, 0, damageClips.Count - 1);

        AudioClip clip = damageClips[index];
        if (clip == null) return;

        // Выбираем следующий AudioSource
        AudioSource source = audioSources[currentSourceIndex];
        source.clip = clip;
        source.Play();

        // Переходим к следующему для следующего удара
        currentSourceIndex = (currentSourceIndex + 1) % audioSources.Length;
    }
}