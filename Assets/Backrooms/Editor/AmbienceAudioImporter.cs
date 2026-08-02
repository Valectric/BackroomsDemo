using UnityEditor;
using UnityEngine;

namespace Backrooms.Editor
{
    /// <summary>
    /// Forces the CC0 ambience to import small. The files ship inside a mobile WebGL download, so
    /// what Unity re-encodes them to matters more than what they are on disk.
    /// </summary>
    /// <remarks>
    /// Left on Unity's defaults, 204 KB of source Ogg became just over a megabyte in the build — the
    /// importer re-encodes from the decoded signal, so an already-small file buys nothing by itself.
    /// These are occasional background one-shots heard under a synthesised hum on a phone speaker,
    /// which is about the least demanding thing audio can be asked to do, so they are forced to mono
    /// at 22 kHz and a low Vorbis quality.
    /// </remarks>
    public sealed class AmbienceAudioImporter : AssetPostprocessor
    {
        /// <summary>Folder whose audio this applies to.</summary>
        private const string AmbienceFolder = "FreesoundCC0";

        /// <summary>
        /// Applies the compression settings before an ambience clip is imported.
        /// </summary>
        private void OnPreprocessAudio()
        {
            if (!assetPath.Contains(AmbienceFolder)) return;

            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = true;
            importer.loadInBackground = true;

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.preloadAudioData = false;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.3f;
            settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            settings.sampleRateOverride = 22050;
            importer.defaultSampleSettings = settings;
        }
    }
}
