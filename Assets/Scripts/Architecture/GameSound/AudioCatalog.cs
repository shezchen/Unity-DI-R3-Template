using System;
using System.Collections.Generic;
using Architecture.Audio;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Architecture
{
    [Serializable]
    public sealed class AudioClipReference : AssetReferenceT<AudioClip>
    {
        public AudioClipReference(string guid) : base(guid)
        {
        }
    }

    [Serializable]
    public sealed class AudioCueDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private AudioClipReference _clipReference;
        [SerializeField, Min(0f)] private float _defaultGain = 1f;

        public string Id => _id;
        public AudioClipReference ClipReference => _clipReference;
        public float DefaultGain => _defaultGain;

        internal AudioCueDefinition(string id, string assetGuid, float defaultGain = 1f)
        {
            _id = id;
            _clipReference = new AudioClipReference(assetGuid);
            _defaultGain = Mathf.Max(0f, defaultGain);
        }
    }

    /// <summary>
    /// Runtime-only mapping from typed cue IDs to Addressable AudioClip references.
    /// Authoring and code generation live in Assets/Editor/Audio.
    /// </summary>
    [CreateAssetMenu(menuName = "16Party/Audio/Audio Catalog", fileName = "AudioCatalog")]
    public sealed class AudioCatalog : ScriptableObject
    {
        [SerializeField] private List<AudioCueDefinition> _music = new();
        [SerializeField] private List<AudioCueDefinition> _sfx = new();

        private readonly Dictionary<MusicCueId, AudioCueDefinition> _musicIndex = new();
        private readonly Dictionary<SfxCueId, AudioCueDefinition> _sfxIndex = new();
        private bool _isIndexBuilt;

        public IReadOnlyList<AudioCueDefinition> Music => _music;
        public IReadOnlyList<AudioCueDefinition> Sfx => _sfx;

        public bool TryGet(MusicCueId cueId, out AudioCueDefinition definition)
        {
            EnsureIndex();
            return _musicIndex.TryGetValue(cueId, out definition);
        }

        public bool TryGet(SfxCueId cueId, out AudioCueDefinition definition)
        {
            EnsureIndex();
            return _sfxIndex.TryGetValue(cueId, out definition);
        }

        internal void ReplaceEntries(
            IEnumerable<AudioCueDefinition> music,
            IEnumerable<AudioCueDefinition> sfx)
        {
            _music.Clear();
            _sfx.Clear();
            _music.AddRange(music);
            _sfx.AddRange(sfx);
            RebuildIndex();
        }

        private void OnEnable() => RebuildIndex();
        private void OnValidate() => RebuildIndex();

        private void EnsureIndex()
        {
            if (!_isIndexBuilt)
            {
                RebuildIndex();
            }
        }

        private void RebuildIndex()
        {
            _musicIndex.Clear();
            _sfxIndex.Clear();
            AddEntries(_music, _musicIndex, "Music");
            AddEntries(_sfx, _sfxIndex, "SFX");
            _isIndexBuilt = true;
        }

        private static void AddEntries<TCueId>(
            IEnumerable<AudioCueDefinition> definitions,
            IDictionary<TCueId, AudioCueDefinition> index,
            string category)
        {
            foreach (var definition in definitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    continue;
                }

                object boxedId = typeof(TCueId) == typeof(MusicCueId)
                    ? new MusicCueId(definition.Id)
                    : new SfxCueId(definition.Id);
                var cueId = (TCueId)boxedId;

                if (index.ContainsKey(cueId))
                {
                    Debug.LogError($"[AudioCatalog] Duplicate {category} cue ID '{definition.Id}'.");
                    continue;
                }

                index.Add(cueId, definition);
            }
        }
    }
}
