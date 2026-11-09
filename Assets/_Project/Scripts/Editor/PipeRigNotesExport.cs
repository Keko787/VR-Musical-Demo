using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;
using VRShootingGallery.Pipes;
using VRShootingGallery.Visualizer;

namespace VRShootingGallery.EditorTools
{
    /// <summary>
    /// Writes out what the pipe rig hears in each track, to be read outside Unity: every note
    /// <see cref="RigScore"/> finds — when, in which part, which instrument plays it, its pitch, how
    /// strong and how sure — and every one it finds and then drops, with why. Optionally the track
    /// too, as the mono WAV the score is read from, so the notes can be checked against the music.
    /// </summary>
    /// <remarks>
    /// The files go to <c>Logs/PipeRigNotes</c>, which git ignores: the tracks are commercial, and
    /// the notes are read from them. It exports the clips selected in the Project window, or the
    /// whole music folder if none are. Each track is read the way the rig reads it in the room, by
    /// the same code, so the notes are exactly the ones the balls are thrown for.
    /// </remarks>
    public static class PipeRigNotesExport
    {
        const string k_Tag = "PipeRig";
        const string k_NotesMenu = "VR-Music-Demo/Pipe Rig/Export Track Notes";
        const string k_AudioMenu = "VR-Music-Demo/Pipe Rig/Export Track Notes and Audio";
        const string k_Folder = "Logs/PipeRigNotes";

        /// <summary>Seconds a track may take to read before it is given up on.</summary>
        const double k_Timeout = 180.0;

        static readonly string[] k_PitchNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        [MenuItem(k_NotesMenu, false, 740)]
        static void ExportNotes() => Export(false);

        [MenuItem(k_AudioMenu, false, 741)]
        static void ExportNotesAndAudio() => Export(true);

        static void Export(bool audio)
        {
            var clips = Selection.GetFiltered<AudioClip>(SelectionMode.Assets);
            if (clips.Length == 0)
                clips = MusicDeck.ClipsInMusicFolder();
            if (clips.Length == 0)
            {
                Debug.LogWarning($"[{k_Tag}] No tracks to export: select some clips, or put them in {MusicDeck.MusicFolder}.");
                return;
            }

            Directory.CreateDirectory(k_Folder);
            var summary = new StringBuilder("track,seconds,lane,instrument,kept,dropped,kept_per_second\n");
            int exported = 0;
            try
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    if (!ExportClip(clips[i], audio, summary, i, clips.Length))
                        break;
                    exported++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            File.WriteAllText(Path.Combine(k_Folder, "summary.csv"), summary.ToString());
            string where = Path.GetFullPath(k_Folder);
            Debug.Log($"[{k_Tag}] Exported the notes of {exported} of {clips.Length} track(s){(audio ? " and their audio" : "")} to {where}.");
            EditorUtility.RevealInFinder(Path.Combine(where, "summary.csv"));
        }

        /// <summary>Reads one track, writes its notes and, if asked, its audio. False if cancelled.</summary>
        static bool ExportClip(AudioClip clip, bool audio, StringBuilder summary, int index, int count)
        {
            string title = TrackIndicator.TitleOf(clip);
            string file = SafeName(title);
            var score = new RigScore(clip, keepDropped: true);
            double started = EditorApplication.timeSinceStartup;
            while (!score.Done && !score.Failed)
            {
                score.Step(30f);
                Thread.Sleep(5);

                float progress = (index + score.Progress) / count;
                if (EditorUtility.DisplayCancelableProgressBar("Export Track Notes", $"{title} ({index + 1} of {count})", progress))
                {
                    score.Cancel();
                    return false;
                }

                if (EditorApplication.timeSinceStartup - started > k_Timeout)
                {
                    score.Cancel();
                    Debug.LogError($"[{k_Tag}] Gave up reading '{title}' after {k_Timeout:0} s.");
                    return true;
                }
            }

            if (score.Failed)
            {
                Debug.LogError($"[{k_Tag}] Cannot read '{title}': {score.Error}.");
                return true;
            }

            var notes = new List<RigNote>();
            score.CopyNotes(notes, 0);
            var dropped = new List<RigDrop>();
            score.CopyDropped(dropped);
            WriteNotes(Path.Combine(k_Folder, file + ".csv"), notes, dropped);
            Summarise(summary, title, clip.length, notes, dropped);

            if (audio)
                WriteWav(Path.Combine(k_Folder, file + ".wav"), clip);
            return true;
        }

        /// <summary>
        /// One row a note, kept or dropped, in time order. Time is the moment in the track a ball
        /// strikes for it; rise is the onset's rise in its band in dB (for a crash, how much louder
        /// the music got); clarity is how far its pitch class stands over the average one.
        /// </summary>
        static void WriteNotes(string path, List<RigNote> notes, List<RigDrop> dropped)
        {
            var rows = new List<(float Time, string Row)>(notes.Count + dropped.Count);
            foreach (var note in notes)
                rows.Add((note.Time, Row(note, true, "")));
            foreach (var drop in dropped)
                rows.Add((drop.Note.Time, Row(drop.Note, false, drop.Why)));
            rows.Sort((a, b) => a.Time.CompareTo(b.Time));

            var csv = new StringBuilder("time,lane,instrument,kept,why,pitch,clarity,rise_db,strength\n");
            foreach (var (_, row) in rows)
                csv.Append(row).Append('\n');
            File.WriteAllText(path, csv.ToString());
        }

        static string Row(RigNote note, bool kept, string why)
        {
            var c = CultureInfo.InvariantCulture;
            string pitch = note.PitchClass >= 0 ? k_PitchNames[note.PitchClass % 12] : "";
            return string.Join(",",
                note.Time.ToString("0.000", c), note.Lane.ToString(), Instrument(note.Lane), kept ? "1" : "0", why, pitch,
                note.Pitched ? note.Clarity.ToString("0.00", c) : "", note.Rise.ToString("0.0", c), note.Strength.ToString("0.00", c));
        }

        /// <summary>What the rig plays a part on, its partner in brackets.</summary>
        static string Instrument(RigLane lane) => lane switch
        {
            RigLane.Kick => "kick",
            RigLane.Snare => "snare",
            RigLane.Tom => "toms",
            RigLane.HiHat => "hi-hat",
            RigLane.Crash => "crash or gong",
            RigLane.Bass => "bass strings (guitar)",
            RigLane.Mid => "mallets (glockenspiel)",
            _ => "strings (chimes)",
        };

        static void Summarise(StringBuilder summary, string title, float seconds, List<RigNote> notes, List<RigDrop> dropped)
        {
            var c = CultureInfo.InvariantCulture;
            var kept = new int[System.Enum.GetValues(typeof(RigLane)).Length];
            var lost = new int[kept.Length];
            foreach (var note in notes)
                kept[(int)note.Lane]++;
            foreach (var drop in dropped)
                lost[(int)drop.Note.Lane]++;

            string name = "\"" + title.Replace("\"", "'") + "\"";
            for (int lane = 0; lane < kept.Length; lane++)
            {
                summary.Append(name).Append(',').Append(seconds.ToString("0.0", c)).Append(',').Append((RigLane)lane).Append(',')
                    .Append(Instrument((RigLane)lane)).Append(',').Append(kept[lane]).Append(',').Append(lost[lane]).Append(',')
                    .Append((kept[lane] / Mathf.Max(1f, seconds)).ToString("0.00", c)).Append('\n');
            }
        }

        /// <summary>
        /// The track as RigScore reads it: one channel, each pair of frames averaged at 32 kHz or
        /// more, 16-bit. Read a few seconds at a time, so a long track needs no big buffer.
        /// </summary>
        static void WriteWav(string path, AudioClip clip)
        {
            int channels = Mathf.Max(1, clip.channels);
            int factor = clip.frequency >= 32000 ? 2 : 1;
            int rate = clip.frequency / factor;
            int frames = clip.samples / factor;

            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(stream);
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + frames * 2);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);                     // PCM
            writer.Write((short)1);                     // mono
            writer.Write(rate);
            writer.Write(rate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(frames * 2);

            int chunk = (clip.frequency * 5 / factor) * factor;
            var buffer = new float[chunk * channels];
            float scale = 1f / (channels * factor);
            int written = 0;
            for (int start = 0; start < frames * factor && written < frames; start += chunk)
            {
                int take = Mathf.Min(chunk, frames * factor - start);
                if (buffer.Length != take * channels)
                    buffer = new float[take * channels];
                if (!clip.GetData(buffer, start))
                    break;

                for (int f = 0; f + factor <= take && written < frames; f += factor)
                {
                    float sum = 0f;
                    for (int i = f * channels; i < (f + factor) * channels; i++)
                        sum += buffer[i];
                    writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sum * scale, -1f, 1f) * 32767f));
                    written++;
                }
            }

            // A clip that would not hand back all of its samples: pad, so the header stays true.
            for (; written < frames; written++)
                writer.Write((short)0);
        }

        static string SafeName(string title)
        {
            var name = new StringBuilder(title.Length);
            foreach (char ch in title)
                name.Append(char.IsLetterOrDigit(ch) || ch == ' ' || ch == '-' ? ch : '_');
            return name.ToString().Trim();
        }
    }
}
