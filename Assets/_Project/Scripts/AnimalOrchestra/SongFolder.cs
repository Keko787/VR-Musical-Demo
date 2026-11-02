using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace VRShootingGallery.AnimalOrchestra
{
    /// <summary>
    /// Loads a song from a folder on disk — the plan's song format: one folder per song holding the MIDI
    /// file, one audio stem per part, and a small mapping file naming which MIDI track and which stem
    /// feed which animal. Adding a song is adding a folder; no animation work.
    /// </summary>
    /// <remarks>
    /// <para>Folders live under <c>StreamingAssets/AnimalOrchestra/</c>, so a build carries them as plain files.
    /// The mapping file is <c>song.json</c>:</para>
    /// <code>
    /// {
    ///   "title": "My Song",
    ///   "midi": "song.mid",
    ///   "parts": [
    ///     { "part": "Kick",  "stem": "kick.wav",  "track": "Drums", "pitches": "36" },
    ///     { "part": "Snare", "stem": "snare.wav", "track": "Drums", "pitches": "38,40,41,43,45,47,48,50" },
    ///     { "part": "Vocal", "stem": "vocal.wav", "track": "Lead Vox" },
    ///     { "part": "Pads",  "stem": "pads.wav" }
    ///   ]
    /// }
    /// </code>
    /// <para>
    /// <c>part</c> is one of the <see cref="Part"/> names. <c>track</c> is a MIDI track's name (or its index);
    /// <c>channel</c> (1–16) and <c>pitches</c> (a comma list, or a range like <c>"60-84"</c>) narrow it, which
    /// is how one drum track feeds the elephant, the gorilla and the woodpecker. A part with a stem but no
    /// track moves on its stem alone; a part with neither is idle for the song.
    /// </para>
    /// <para>
    /// Stems are WAV or OGG, all starting at the same instant as the MIDI file's time zero — the way a DAW
    /// exports them. They play together, scheduled to the same audio-clock sample.
    /// </para>
    /// </remarks>
    public static class SongFolder
    {
        public const string MappingFile = "song.json";

        /// <summary>Where song folders are looked for: <c>StreamingAssets/AnimalOrchestra</c>.</summary>
        public static string Root => Path.Combine(Application.streamingAssetsPath, "AnimalOrchestra");

        [Serializable]
        class Mapping
        {
            public string title;
            public string midi;
            public PartMapping[] parts;
        }

        [Serializable]
        class PartMapping
        {
            public string part;
            public string stem;
            public string track;
            public int channel;
            public string pitches;
        }

        /// <summary>Every folder under <see cref="Root"/> with a mapping file, sorted by name.</summary>
        public static List<string> FindAll()
        {
            var folders = new List<string>();
            if (!Directory.Exists(Root))
                return folders;

            foreach (var folder in Directory.GetDirectories(Root))
                if (File.Exists(Path.Combine(folder, MappingFile)))
                    folders.Add(folder);

            folders.Sort(StringComparer.OrdinalIgnoreCase);
            return folders;
        }

        /// <summary>
        /// Loads the song in <paramref name="folder"/>: parses its mapping and MIDI, loads its stems, and
        /// measures them on a worker thread. Calls <paramref name="done"/> with the song, or
        /// <paramref name="failed"/> with the reason. Run as a coroutine.
        /// </summary>
        public static IEnumerator Load(string folder, Action<PreparedSong> done, Action<string> failed)
        {
            Mapping mapping;
            MidiFile midi = null;
            try
            {
                mapping = JsonUtility.FromJson<Mapping>(File.ReadAllText(Path.Combine(folder, MappingFile)));
                if (mapping?.parts == null)
                    throw new FormatException("no \"parts\" list");

                if (!string.IsNullOrEmpty(mapping.midi))
                    midi = MidiFile.Parse(File.ReadAllBytes(Path.Combine(folder, mapping.midi)));
            }
            catch (Exception e)
            {
                failed?.Invoke($"'{folder}': {e.Message}");
                yield break;
            }

            var song = new SongData
            {
                Title = string.IsNullOrEmpty(mapping.title) ? Path.GetFileName(folder) : mapping.title,
                Bpm = midi?.Bpm ?? 120f,
                FirstBeat = 0.0,
            };

            var clips = new List<AudioClip>();
            double length = 0.0;

            foreach (var entry in mapping.parts)
            {
                if (!Enum.TryParse(entry.part, true, out Part part))
                {
                    Debug.LogWarning($"[AnimalOrchestra] '{folder}': unknown part \"{entry.part}\" — expected one of " +
                                     string.Join(", ", Parts.All));
                    continue;
                }

                if (midi != null && !string.IsNullOrEmpty(entry.track))
                {
                    var track = midi.Find(entry.track);
                    if (track == null)
                        Debug.LogWarning($"[AnimalOrchestra] '{folder}': no MIDI track \"{entry.track}\" for {part}.");
                    else
                        song.Tracks[(int)part] = new NoteTrack(Select(track, entry.channel, ParsePitches(entry.pitches)));
                }

                if (string.IsNullOrEmpty(entry.stem))
                    continue;

                AudioClip clip = null;
                string error = null;
                yield return LoadClip(Path.Combine(folder, entry.stem), c => clip = c, e => error = e);
                if (clip == null)
                {
                    Debug.LogWarning($"[AnimalOrchestra] '{folder}': stem '{entry.stem}' for {part}: {error}");
                    continue;
                }

                clip.name = $"{song.Title} — {part}";
                clips.Add(clip);
                length = Math.Max(length, clip.length);

                // Measured off the main thread: a four-minute stem is ten million samples.
                var samples = new float[clip.samples * clip.channels];
                clip.GetData(samples, 0);
                int channels = clip.channels, rate = clip.frequency;
                var measure = Task.Run(() => StemAnalysis.Measure(samples, channels, rate));
                while (!measure.IsCompleted)
                    yield return null;

                if (measure.IsFaulted)
                    Debug.LogWarning($"[AnimalOrchestra] '{folder}': could not measure '{entry.stem}': {measure.Exception?.InnerException?.Message}");
                else
                    song.Analysis[(int)part] = measure.Result;
            }

            foreach (var track in song.Tracks)
                if (track.Count > 0)
                    length = Math.Max(length, track[track.Count - 1].End + 1.0);

            if (clips.Count == 0)
            {
                failed?.Invoke($"'{folder}': no stem could be loaded, so there is nothing to hear.");
                yield break;
            }

            song.Length = length;
            done?.Invoke(new PreparedSong(song, clips.ToArray()));
        }

        static IEnumerator LoadClip(string path, Action<AudioClip> done, Action<string> failed)
        {
            if (!File.Exists(path))
            {
                failed("file not found");
                yield break;
            }

            var type = Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".wav" => AudioType.WAV,
                ".ogg" => AudioType.OGGVORBIS,
                ".mp3" => AudioType.MPEG,
                _ => AudioType.UNKNOWN,
            };

            using var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, type);
            if (request.downloadHandler is DownloadHandlerAudioClip handler)
            {
                // Whole and decoded, so it can be measured with GetData and started sample-accurately.
                handler.streamAudio = false;
                handler.compressed = false;
            }

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                failed(request.error);
                yield break;
            }

            var clip = DownloadHandlerAudioClip.GetContent(request);
            if (clip == null || clip.samples == 0)
                failed("not a readable audio file");
            else
                done(clip);
        }

        static IEnumerable<Note> Select(MidiFile.Track track, int channel, HashSet<int> pitches)
        {
            foreach (var (note, noteChannel) in track.Notes)
            {
                if (channel >= 1 && channel <= 16 && noteChannel != channel - 1)
                    continue;
                if (pitches != null && !pitches.Contains(note.Pitch))
                    continue;
                yield return note;
            }
        }

        /// <summary>"36", "38,40,41", "60-84" or a mix; null (every pitch) for an empty string.</summary>
        static HashSet<int> ParsePitches(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var set = new HashSet<int>();
            foreach (var item in text.Split(','))
            {
                var range = item.Split('-');
                if (range.Length == 2 && int.TryParse(range[0].Trim(), out int low) && int.TryParse(range[1].Trim(), out int high))
                {
                    for (int p = Math.Max(0, low); p <= Math.Min(127, high); p++)
                        set.Add(p);
                }
                else if (int.TryParse(item.Trim(), out int pitch))
                {
                    set.Add(pitch);
                }
            }

            return set.Count > 0 ? set : null;
        }
    }
}
