using System;
using System.Collections.Generic;

namespace VRShootingGallery.AnimalOrchestra
{
    /// <summary>
    /// The parts of an arrangement, one per performer — the cast table of the Animal Orchestra plan. Each
    /// performer listens to exactly one of these.
    /// </summary>
    /// <remarks>
    /// Plain C# with no Unity types, like the rest of the song side (<see cref="SongData"/>,
    /// <see cref="SongSynth"/>, <see cref="MidiFile"/>), so the songs can be built, rendered and checked
    /// outside the Editor.
    /// </remarks>
    public enum Part
    {
        /// <summary>Elephant, front wall: a stomp per kick, and the floor ripples from the foot.</summary>
        Kick,

        /// <summary>Whale, on the floor: swim speed and glow follow the low band.</summary>
        SubBass,

        /// <summary>Gorilla, left wall: chest beat on snare, alternating fists on toms.</summary>
        Snare,

        /// <summary>Wolf pack, right wall: one wolf per harmony voice, howl height follows pitch.</summary>
        Choir,

        /// <summary>Starling flock, front wall upper band: the flock's centre traces the pitch.</summary>
        Arp,

        /// <summary>Fireflies, every free surface: the high band sets blink rate and density. Also the idle state.</summary>
        Texture,

        /// <summary>Parrot, eye level nearest the front wall: beak and crest from amplitude, wings on held notes.</summary>
        Vocal,

        /// <summary>Bullfrog, waist height left of the viewer: throat sac from note velocity.</summary>
        Bass,

        /// <summary>Woodpecker, waist height right of the viewer: a peck per hat, the open hat lifts the head.</summary>
        HiHat,

        /// <summary>Jellyfish, overhead: bell pulse from the envelope, colour from the chord root.</summary>
        Pads,

        /// <summary>Octopus, low centre on a pedestal: one arm per sounding voice.</summary>
        Keys,
    }

    public static class Parts
    {
        public const int Count = 11;

        public static readonly Part[] All = (Part[])Enum.GetValues(typeof(Part));
    }

    /// <summary>The continuous signals taken from a stem (<see cref="PartAnalysis"/>).</summary>
    public enum Band
    {
        /// <summary>The whole stem's loudness.</summary>
        Level,

        /// <summary>Below about 150 Hz.</summary>
        Low,

        /// <summary>Above about 4 kHz.</summary>
        High,
    }

    /// <summary>General MIDI drum notes, which the drum parts use to say which drum a hit is on.</summary>
    public static class GmDrum
    {
        public const int Kick = 36;
        public const int Snare = 38;
        public const int Clap = 39;
        public const int SnareAlt = 40;
        public const int TomFloorLow = 41;
        public const int ClosedHat = 42;
        public const int TomFloorHigh = 43;
        public const int PedalHat = 44;
        public const int TomLow = 45;
        public const int OpenHat = 46;
        public const int TomLowMid = 47;
        public const int TomHighMid = 48;
        public const int TomHigh = 50;

        public static bool IsTom(int pitch) =>
            pitch == TomFloorLow || pitch == TomFloorHigh || pitch == TomLow ||
            pitch == TomLowMid || pitch == TomHighMid || pitch == TomHigh;

        public static bool IsOpenHat(int pitch) => pitch == OpenHat;
    }

    /// <summary>One note: when, how long, which pitch, how hard. Times are seconds on the song's audio clock.</summary>
    public readonly struct Note : IComparable<Note>
    {
        public readonly double Time;
        public readonly double Duration;
        public readonly int Pitch;

        /// <summary>0 to 1.</summary>
        public readonly float Velocity;

        public Note(double time, double duration, int pitch, float velocity)
        {
            Time = time;
            Duration = duration;
            Pitch = pitch;
            Velocity = velocity;
        }

        public double End => Time + Duration;

        public int CompareTo(Note other)
        {
            int byTime = Time.CompareTo(other.Time);
            return byTime != 0 ? byTime : Pitch.CompareTo(other.Pitch);
        }

        public override string ToString() => $"{Pitch}@{Time:0.000}s+{Duration:0.000} v{Velocity:0.00}";
    }

    /// <summary>
    /// One part's notes, sorted by start, with the queries a performer asks every frame: the last note
    /// to have started, the next one coming, and the ones sounding now. All binary searches, so a
    /// performer can ask from scratch each frame and never has to track its place — which is what keeps
    /// the animation on the audio clock rather than on a count of frames.
    /// </summary>
    public sealed class NoteTrack
    {
        public static readonly NoteTrack Empty = new(Array.Empty<Note>());

        readonly Note[] m_Notes;

        public NoteTrack(IEnumerable<Note> notes)
        {
            var list = new List<Note>(notes);
            list.Sort();
            m_Notes = list.ToArray();

            MinPitch = int.MaxValue;
            MaxPitch = int.MinValue;
            foreach (var note in m_Notes)
            {
                MaxDuration = Math.Max(MaxDuration, note.Duration);
                MinPitch = Math.Min(MinPitch, note.Pitch);
                MaxPitch = Math.Max(MaxPitch, note.Pitch);
            }

            if (m_Notes.Length == 0)
                MinPitch = MaxPitch = 60;
        }

        public int Count => m_Notes.Length;

        public Note this[int index] => m_Notes[index];

        /// <summary>The longest note, which bounds how far back <see cref="ActiveAt"/> has to look.</summary>
        public double MaxDuration { get; }

        public int MinPitch { get; }

        public int MaxPitch { get; }

        /// <summary>Index of the last note starting at or before <paramref name="time"/>; −1 if none has.</summary>
        public int LastAtOrBefore(double time)
        {
            int lo = 0, hi = m_Notes.Length - 1, found = -1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                if (m_Notes[mid].Time <= time)
                {
                    found = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return found;
        }

        /// <summary>The notes sounding at <paramref name="time"/> (started, not yet ended), latest first.</summary>
        public void ActiveAt(double time, List<Note> into)
        {
            into.Clear();
            for (int i = LastAtOrBefore(time); i >= 0; i--)
            {
                var note = m_Notes[i];
                if (note.Time < time - MaxDuration)
                    break;

                if (time < note.End)
                    into.Add(note);
            }
        }
    }

    /// <summary>
    /// A stem's continuous signals — loudness and a low and a high band — measured once over the whole
    /// stem when the song loads, smoothed, and normalised so 1 is the stem's loud passages. Read at the
    /// song time, so they are on the same clock as the notes and never jitter with the frame rate.
    /// </summary>
    /// <remarks>
    /// The plan asks for an envelope follower and a few FFT bands per stem, smoothed before they reach
    /// an animal. Measuring them up front is the same signal without a frame of analysis latency, and
    /// the bands come from simple filters rather than an FFT because three broad bands is all any
    /// animal reads.
    /// </remarks>
    public sealed class PartAnalysis
    {
        readonly float[][] m_Bands;

        public PartAnalysis(float frameRate, float[] level, float[] low, float[] high)
        {
            FrameRate = frameRate;
            m_Bands = new[] { level, low, high };
        }

        /// <summary>Frames per second.</summary>
        public float FrameRate { get; }

        public int Frames => m_Bands[0].Length;

        /// <summary>The band at <paramref name="time"/>, interpolated between frames; 0 outside the stem.</summary>
        public float Sample(Band band, double time)
        {
            var frames = m_Bands[(int)band];
            double position = time * FrameRate;
            if (position < 0.0 || frames.Length == 0)
                return 0f;

            int i = (int)position;
            if (i >= frames.Length - 1)
                return i == frames.Length - 1 ? frames[i] : 0f;

            float f = (float)(position - i);
            return frames[i] + (frames[i + 1] - frames[i]) * f;
        }
    }

    /// <summary>
    /// A song as the orchestra reads it: per part, the notes (the triggers) and the stem analysis (the
    /// continuous signals), plus the tempo for anything that moves to the beat. A part with neither is
    /// missing from the song, and its animal idles through it.
    /// </summary>
    public sealed class SongData
    {
        public string Title = "Untitled";

        /// <summary>Seconds, including the tail after the last note.</summary>
        public double Length;

        public float Bpm = 120f;

        /// <summary>Song time of beat 0.</summary>
        public double FirstBeat;

        public readonly NoteTrack[] Tracks = new NoteTrack[Parts.Count];
        public readonly PartAnalysis[] Analysis = new PartAnalysis[Parts.Count];

        /// <summary>
        /// A synthesised song's mix, interleaved, ready for an AudioClip; null for a song played from its
        /// stems on disk.
        /// </summary>
        public float[] Pcm;

        public int Channels = 2;
        public int SampleRate = 44100;

        public SongData()
        {
            for (int i = 0; i < Tracks.Length; i++)
                Tracks[i] = NoteTrack.Empty;
        }

        public NoteTrack Track(Part part) => Tracks[(int)part];

        public bool Has(Part part) => Tracks[(int)part].Count > 0 || Analysis[(int)part] != null;

        /// <summary>Beats since <see cref="FirstBeat"/>, fractional.</summary>
        public double BeatAt(double time) => (time - FirstBeat) * Bpm / 60.0;

        public float Level(Part part, Band band, double time)
        {
            var analysis = Analysis[(int)part];
            return analysis == null ? 0f : analysis.Sample(band, time);
        }
    }
}
