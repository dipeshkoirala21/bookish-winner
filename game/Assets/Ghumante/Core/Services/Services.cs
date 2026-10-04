using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Ghumante.Core.Services
{
    // Service interfaces that gameplay assemblies depend on (ARCHITECTURE.md 7.1, 7.11, 7.12). App wires the
    // implementations; the Null* versions below are the M0/M1 defaults and collect or do nothing.

    /// <summary>Opt-in analytics (ADR-009). Implementations must drop events until consent is given.</summary>
    public interface IAnalytics
    {
        bool Enabled { get; }
        void SetConsent(bool granted);
        void TrackEvent(string name, IReadOnlyDictionary<string, object> parameters = null);
    }

    public interface ICrashReporter
    {
        void SetConsent(bool granted);
        void RecordException(Exception exception, string context = null);
        void Breadcrumb(string message);
    }

    /// <summary>Platform cloud save (Game Center, Play Games Services). Payloads are save JSON documents.</summary>
    public interface ICloudSave
    {
        Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>The stored document of a slot, or null when there is none.</summary>
        Task<string> LoadAsync(string slot, CancellationToken cancellationToken = default(CancellationToken));

        Task<bool> SaveAsync(string slot, string json, CancellationToken cancellationToken = default(CancellationToken));
    }

    /// <summary>Story Mode hook (7.11): quests react to events on the bus and persist in the save's "quests".</summary>
    public interface IQuestService
    {
        bool IsActive(string questId);
        bool IsCompleted(string questId);
        IReadOnlyList<string> ActiveQuests { get; }
    }

    public interface IDialogueService
    {
        bool HasDialogue(string npcId);

        /// <summary>Start a conversation; returns false when there is nothing to say.</summary>
        bool TryStart(string npcId);
    }

    /// <summary>Named boolean/integer world-state flags for Story Mode (persisted in the save's "story").</summary>
    public interface IWorldStateFlags
    {
        bool GetBool(string flag);
        void SetBool(string flag, bool value);
        int GetInt(string flag);
        void SetInt(string flag, int value);
    }

    /// <summary>Named NPCs that quests and dialogue can refer to.</summary>
    public interface INpcRegistry
    {
        bool TryGetPosition(string npcId, out Geo.WorldPos position);
        IReadOnlyList<string> NpcIds { get; }
    }

    /// <summary>Files that make up an installed region (DATA_FORMATS.md section 2).</summary>
    public enum RegionFileKind
    {
        Manifest = 0,
        Pack = 1,
        SearchIndex = 2,
        RouteGraph = 3,
    }

    /// <summary>
    /// Where region packs come from: Play Asset Delivery, a CDN download cache or the built-in data
    /// (implemented in Ghumante.Platform). Streams returned by <see cref="OpenAsync"/> are owned by the caller;
    /// pack streams must be seekable (PackReader reads them with random access).
    /// </summary>
    public interface IRegionPackSource
    {
        Task<IReadOnlyList<string>> ListRegionsAsync(CancellationToken cancellationToken = default(CancellationToken));
        Task<bool> IsAvailableAsync(string regionId, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>Download or unpack a region; progress is reported in [0, 1].</summary>
        Task<bool> RequestAsync(string regionId, IProgress<float> progress = null,
                                CancellationToken cancellationToken = default(CancellationToken));

        Task<Stream> OpenAsync(string regionId, RegionFileKind kind,
                               CancellationToken cancellationToken = default(CancellationToken));
    }

    public static class RegionPackSourceExtensions
    {
        public static Task<Stream> OpenPackAsync(this IRegionPackSource source, string regionId,
                                                 CancellationToken cancellationToken = default(CancellationToken))
        {
            return source.OpenAsync(regionId, RegionFileKind.Pack, cancellationToken);
        }
    }

    // ---------------------------------------------------------------------------------------------------------
    // Null implementations
    // ---------------------------------------------------------------------------------------------------------

    public sealed class NullAnalytics : IAnalytics
    {
        public bool Enabled
        {
            get { return false; }
        }

        public void SetConsent(bool granted)
        {
        }

        public void TrackEvent(string name, IReadOnlyDictionary<string, object> parameters = null)
        {
        }
    }

    public sealed class NullCrashReporter : ICrashReporter
    {
        public void SetConsent(bool granted)
        {
        }

        public void RecordException(Exception exception, string context = null)
        {
        }

        public void Breadcrumb(string message)
        {
        }
    }

    public sealed class NullCloudSave : ICloudSave
    {
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(false);
        }

        public Task<string> LoadAsync(string slot, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult<string>(null);
        }

        public Task<bool> SaveAsync(string slot, string json, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(false);
        }
    }

    public sealed class NullQuestService : IQuestService
    {
        public bool IsActive(string questId)
        {
            return false;
        }

        public bool IsCompleted(string questId)
        {
            return false;
        }

        public IReadOnlyList<string> ActiveQuests
        {
            get { return Array.Empty<string>(); }
        }
    }

    public sealed class NullDialogueService : IDialogueService
    {
        public bool HasDialogue(string npcId)
        {
            return false;
        }

        public bool TryStart(string npcId)
        {
            return false;
        }
    }

    /// <summary>In-memory flags (not persisted): the M1 default so Story Mode code paths can run.</summary>
    public sealed class NullWorldStateFlags : IWorldStateFlags
    {
        private readonly Dictionary<string, int> _flags = new Dictionary<string, int>(StringComparer.Ordinal);

        public bool GetBool(string flag)
        {
            return GetInt(flag) != 0;
        }

        public void SetBool(string flag, bool value)
        {
            SetInt(flag, value ? 1 : 0);
        }

        public int GetInt(string flag)
        {
            int v;
            return flag != null && _flags.TryGetValue(flag, out v) ? v : 0;
        }

        public void SetInt(string flag, int value)
        {
            if (flag == null) throw new ArgumentNullException(nameof(flag));
            _flags[flag] = value;
        }
    }

    public sealed class NullNpcRegistry : INpcRegistry
    {
        public bool TryGetPosition(string npcId, out Geo.WorldPos position)
        {
            position = default(Geo.WorldPos);
            return false;
        }

        public IReadOnlyList<string> NpcIds
        {
            get { return Array.Empty<string>(); }
        }
    }

    /// <summary>No regions available (editor without data, tests).</summary>
    public sealed class NullRegionPackSource : IRegionPackSource
    {
        public Task<IReadOnlyList<string>> ListRegionsAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        public Task<bool> IsAvailableAsync(string regionId, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(false);
        }

        public Task<bool> RequestAsync(string regionId, IProgress<float> progress = null,
                                       CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(false);
        }

        public Task<Stream> OpenAsync(string regionId, RegionFileKind kind,
                                      CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromException<Stream>(new FileNotFoundException("region " + regionId + " is not available"));
        }
    }
}
