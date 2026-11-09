using System;
using System.Collections.Generic;

namespace VRShootingGallery.AnimalOrchestra
{
    /// <summary>
    /// The orchestra's own songs: three short arrangements written out note by note, so the demo plays
    /// with nothing to license and nothing to load. Composed here and synthesised by
    /// <see cref="SongSynth"/>; the notes that make the sound are the same notes the animals read, so
    /// every sound has a visible cause by construction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each song is a key, a tempo, a four-chord loop and a list of sections, each section naming the
    /// parts that play in it. Parts come and go between sections, so animals take turns resting — and
    /// the third song leaves the vocal and keys out altogether, the case the plan describes as "songs
    /// without a part simply leave that animal idle".
    /// </para>
    /// <para>
    /// Deterministic: the melodies come from a seeded generator, so a song is the same every time it
    /// plays and a repeated chorus sings the same tune.
    /// </para>
    /// </remarks>
    public static class DemoSongs
    {
        static readonly int[] k_Minor = { 0, 2, 3, 5, 7, 8, 10 };
        static readonly int[] k_Major = { 0, 2, 4, 5, 7, 9, 11 };

        /// <summary>Song time of the first downbeat: a short lead-in, so the first hits have time for their windups.</summary>
        const double k_LeadIn = 0.6;

        /// <summary>Silence kept after the last note, for releases and the reverb to ring out.</summary>
        const double k_Tail = 3.0;

        /// <summary>General MIDI's crash cymbal, the note a drop's roar is written on.</summary>
        const int k_Crash = 49;

        public static int Count => 3;

        public static SongData Compose(int index)
        {
            var song = (index % Count) switch
            {
                0 => LagoonLights(),
                1 => NightParade(),
                _ => DeepCurrent(),
            };

            return song.Write();
        }

        // ================================================================== the songs

        /// <summary>A minor, 110 BPM, i–VI–III–VII: the whole orchestra, building to two choruses.</summary>
        static Arrangement LagoonLights() => new()
        {
            Title = "Lagoon Lights",
            Bpm = 110f,
            Tonic = 57,
            Scale = k_Minor,
            Progression = new[] { 0, 5, 2, 6 },
            Seed = 11,
            Sections = new[]
            {
                new Section("Intro", 4, 0.2f, Part.Pads, Part.Texture, Part.Choir),
                new Section("Verse", 8, 0.5f, Part.Kick, Part.SubBass, Part.Snare, Part.HiHat, Part.Bass, Part.Vocal,
                    Part.Pads, Part.Texture, Part.Keys) { Melody = 0, Fill = true },
                new Section("Build", 4, 0.75f, Part.Kick, Part.SubBass, Part.Snare, Part.HiHat, Part.Bass, Part.Arp,
                    Part.Pads, Part.Texture) { FourOnFloor = true, Roll = true },
                new Section("Chorus", 8, 1f, Part.Kick, Part.SubBass, Part.Snare, Part.Choir, Part.Arp, Part.Texture,
                    Part.Vocal, Part.Bass, Part.HiHat, Part.Pads) { FourOnFloor = true, Melody = 1, Fill = true },
                new Section("Break", 4, 0.3f, Part.Pads, Part.Keys, Part.Vocal, Part.Texture, Part.SubBass)
                    { Melody = 2, BrokenKeys = true },
                new Section("Chorus", 8, 1f, Part.Kick, Part.SubBass, Part.Snare, Part.Choir, Part.Arp, Part.Texture,
                    Part.Vocal, Part.Bass, Part.HiHat, Part.Pads, Part.Keys) { FourOnFloor = true, Melody = 1 },
                new Section("Outro", 4, 0.2f, Part.Pads, Part.Choir, Part.Texture, Part.Keys) { BrokenKeys = true },
            },
        };

        /// <summary>D major, 96 BPM, I–V–vi–IV, a half-time groove led by the keys and the voice.</summary>
        static Arrangement NightParade() => new()
        {
            Title = "Night Parade",
            Bpm = 96f,
            Tonic = 50,
            Scale = k_Major,
            Progression = new[] { 0, 4, 5, 3 },
            Seed = 23,
            KickSteps = new[] { 0, 7, 10 },
            SnareSteps = new[] { 8 },
            Sections = new[]
            {
                new Section("Intro", 4, 0.2f, Part.Keys, Part.Pads, Part.Texture) { BrokenKeys = true },
                new Section("Verse", 8, 0.5f, Part.Kick, Part.SubBass, Part.Snare, Part.HiHat, Part.Bass, Part.Keys,
                    Part.Vocal, Part.Texture) { Melody = 0, Fill = true },
                new Section("Chorus", 8, 0.9f, Part.Kick, Part.SubBass, Part.Snare, Part.HiHat, Part.Bass, Part.Keys,
                    Part.Vocal, Part.Choir, Part.Pads, Part.Arp, Part.Texture) { Melody = 1, Fill = true },
                new Section("Verse", 4, 0.5f, Part.Kick, Part.SubBass, Part.Snare, Part.HiHat, Part.Bass, Part.Keys,
                    Part.Vocal, Part.Texture) { Melody = 0, Roll = true },
                new Section("Chorus", 8, 1f, Part.Kick, Part.SubBass, Part.Snare, Part.HiHat, Part.Bass, Part.Keys,
                    Part.Vocal, Part.Choir, Part.Pads, Part.Arp, Part.Texture) { Melody = 1 },
                new Section("Outro", 4, 0.2f, Part.Keys, Part.Pads, Part.Choir) { BrokenKeys = true },
            },
        };

        /// <summary>E minor, 124 BPM, i–iv–VI–v: electronic, arpeggio-led, no vocal and no keys.</summary>
        static Arrangement DeepCurrent() => new()
        {
            Title = "Deep Current",
            Bpm = 124f,
            Tonic = 52,
            Scale = k_Minor,
            Progression = new[] { 0, 3, 5, 4 },
            Seed = 37,
            Sections = new[]
            {
                new Section("Intro", 8, 0.3f, Part.Arp, Part.Pads, Part.Texture, Part.SubBass),
                new Section("Groove", 8, 0.6f, Part.Kick, Part.SubBass, Part.Snare, Part.HiHat, Part.Bass, Part.Arp,
                    Part.Texture) { FourOnFloor = true, Fill = true },
                new Section("Build", 4, 0.8f, Part.Kick, Part.SubBass, Part.Snare, Part.HiHat, Part.Arp, Part.Pads,
                    Part.Texture) { FourOnFloor = true, Roll = true },
                new Section("Drop", 8, 1f, Part.Kick, Part.SubBass, Part.Snare, Part.HiHat, Part.Bass, Part.Arp,
                    Part.Choir, Part.Pads, Part.Texture) { FourOnFloor = true, Fill = true },
                new Section("Break", 4, 0.3f, Part.Pads, Part.Choir, Part.Arp, Part.Texture),
                new Section("Drop", 8, 1f, Part.Kick, Part.SubBass, Part.Snare, Part.HiHat, Part.Bass, Part.Arp,
                    Part.Choir, Part.Pads, Part.Texture) { FourOnFloor = true },
                new Section("Outro", 4, 0.2f, Part.Pads, Part.Arp, Part.Texture),
            },
        };

        // ================================================================== arrangement

        sealed class Section
        {
            public readonly string Name;
            public readonly int Bars;
            public readonly float Energy;
            readonly HashSet<Part> m_Parts;

            public bool FourOnFloor;
            public bool Fill;
            public bool Roll;
            public bool BrokenKeys;
            public int Melody = -1;

            public Section(string name, int bars, float energy, params Part[] parts)
            {
                Name = name;
                Bars = bars;
                Energy = energy;
                m_Parts = new HashSet<Part>(parts);
            }

            public bool Has(Part part) => m_Parts.Contains(part);
        }

        sealed class Arrangement
        {
            public string Title;
            public float Bpm;

            /// <summary>The key's tonic as a MIDI note, around the third octave.</summary>
            public int Tonic;

            public int[] Scale;

            /// <summary>Scale degree of each bar's chord, looping.</summary>
            public int[] Progression;

            public Section[] Sections;
            public int Seed;
            public int[] KickSteps = { 0, 6, 10 };
            public int[] SnareSteps = { 4, 12 };

            readonly List<Note>[] m_Notes = new List<Note>[Parts.Count];

            double Step => 60.0 / Bpm / 4.0;
            double BarLength => Step * 16.0;

            void Add(Part part, double time, double duration, int pitch, float velocity) =>
                (m_Notes[(int)part] ??= new List<Note>()).Add(new Note(time, duration, pitch,
                    Math.Max(0.05f, Math.Min(1f, velocity))));

            /// <summary>The pitch of scale degree <paramref name="degree"/> (any integer) above <paramref name="tonic"/>.</summary>
            int Degree(int tonic, int degree)
            {
                int octave = (int)Math.Floor(degree / 7.0);
                return tonic + Scale[degree - octave * 7] + 12 * octave;
            }

            /// <summary>The pitch classes of the bar's chord: root, third, fifth, seventh.</summary>
            int[] Chord(int bar)
            {
                int root = Progression[bar % Progression.Length];
                var classes = new int[4];
                for (int i = 0; i < 4; i++)
                    classes[i] = ((Degree(Tonic, root + 2 * i) % 12) + 12) % 12;
                return classes;
            }

            /// <summary>The lowest pitch at or above <paramref name="floor"/> with pitch class <paramref name="pitchClass"/>.</summary>
            static int Above(int floor, int pitchClass) => floor + (((pitchClass - floor) % 12) + 12) % 12;

            /// <summary>The pitch nearest <paramref name="previous"/> with pitch class <paramref name="pitchClass"/>.</summary>
            static int Nearest(int previous, int pitchClass)
            {
                int up = Above(previous, pitchClass);
                return up - previous > 6 ? up - 12 : up;
            }

            public SongData Write()
            {
                var random = new Random(Seed);
                var melodies = new Dictionary<int, int[][]>();
                var padVoices = new[] { Tonic + 7, Tonic + 10, Tonic + 14 };
                var keyVoices = new[] { Tonic + 12, Tonic + 16, Tonic + 19 };
                var choirVoices = new[] { Tonic + 3, Tonic + 7, Tonic + 12 };

                int bar = 0;
                float energyBefore = 0f;
                foreach (var section in Sections)
                {
                    for (int local = 0; local < section.Bars; local++, bar++)
                    {
                        double t0 = k_LeadIn + bar * BarLength;
                        bool last = local == section.Bars - 1;
                        var chord = Chord(bar);

                        // The drop: a crash on the downbeat where the song lifts into a chorus, which the tiger
                        // roars on. Twice a song, about — only where the energy jumps, not on every loud bar.
                        if (local == 0 && section.Energy >= 0.9f && section.Energy - energyBefore >= 0.2f)
                            Add(Part.Roar, t0, 1.5, k_Crash, 1f);

                        if (section.Has(Part.Kick)) Kick(section, t0, bar, last);
                        if (section.Has(Part.Snare)) Snare(section, t0, last);
                        if (section.Has(Part.HiHat)) HiHat(section, t0);
                        if (section.Has(Part.Texture)) Texture(section, t0, chord, random);
                        if (section.Has(Part.SubBass)) Sub(t0, chord);
                        if (section.Has(Part.Bass)) Bass(section, t0, chord, last);
                        if (section.Has(Part.Pads)) Pads(t0, chord, padVoices);
                        if (section.Has(Part.Keys)) Keys(section, t0, chord, keyVoices);
                        if (section.Has(Part.Arp)) Arp(section, t0, chord);
                        if (section.Has(Part.Choir)) Choir(section, t0, chord, choirVoices);

                        // Melodies are written two bars at a time, keyed by the section's melody and the
                        // phrase's place in it, so a repeated chorus sings the same tune over the same chords.
                        if (section.Has(Part.Vocal) && local % 2 == 0)
                        {
                            int phrase = local / 2;
                            int key = section.Melody * 16 + phrase;
                            if (!melodies.TryGetValue(key, out var tune))
                                melodies[key] = tune = Tune(section, phrase, bar, new Random(Seed * 1000 + key));
                            Sing(t0, tune);
                        }
                    }

                    energyBefore = section.Energy;
                }

                var song = new SongData
                {
                    Title = Title,
                    Bpm = Bpm,
                    FirstBeat = k_LeadIn,
                };

                double end = 0.0;
                foreach (var part in Parts.All)
                {
                    var notes = m_Notes[(int)part];
                    if (notes == null)
                        continue;

                    song.Tracks[(int)part] = new NoteTrack(notes);
                    foreach (var note in notes)
                        end = Math.Max(end, note.End);
                }

                song.Length = end + k_Tail;
                return song;
            }

            // ---------------------------------------------------------------- drums

            void Kick(Section section, double t0, int bar, bool last)
            {
                var steps = section.FourOnFloor ? new[] { 0, 4, 8, 12 } : KickSteps;
                foreach (int s in steps)
                {
                    // The first kick of each four-bar phrase is the accent the elephant raises its trunk to.
                    if (last && section.Roll && s >= 8)
                        continue;

                    float velocity = s == 0 ? (bar % 4 == 0 ? 1f : 0.9f) : 0.82f;
                    Add(Part.Kick, t0 + s * Step, Step, GmDrum.Kick, velocity);
                }
            }

            void Snare(Section section, double t0, bool last)
            {
                if (last && section.Roll)
                {
                    // A rising roll into the next section: sixteenths from beat three.
                    foreach (int s in SnareSteps)
                        if (s < 8)
                            Add(Part.Snare, t0 + s * Step, Step, GmDrum.Snare, 0.85f);
                    for (int s = 8; s < 16; s++)
                        Add(Part.Snare, t0 + s * Step, Step, GmDrum.Snare, 0.4f + 0.6f * (s - 8) / 7f);
                    return;
                }

                if (last && section.Fill)
                {
                    // Down the toms in the last beats of the section.
                    foreach (int s in SnareSteps)
                        if (s < 10)
                            Add(Part.Snare, t0 + s * Step, Step, GmDrum.Snare, 0.9f);
                    int[] toms = { GmDrum.TomHigh, GmDrum.TomHighMid, GmDrum.TomLow, GmDrum.TomLow, GmDrum.TomFloorHigh, GmDrum.TomFloorHigh };
                    for (int i = 0; i < toms.Length; i++)
                        Add(Part.Snare, t0 + (10 + i) * Step, Step, toms[i], 0.7f + 0.05f * i);
                    return;
                }

                foreach (int s in SnareSteps)
                    Add(Part.Snare, t0 + s * Step, Step, GmDrum.Snare, 0.9f);
            }

            void HiHat(Section section, double t0)
            {
                bool busy = section.Energy >= 0.7f;
                for (int s = 0; s < 16; s++)
                {
                    bool open = (s == 14 && section.Energy >= 0.5f) || (s == 6 && busy);
                    if (open)
                    {
                        Add(Part.HiHat, t0 + s * Step, Step * 2.5, GmDrum.OpenHat, 0.75f);
                        continue;
                    }

                    if (s % 2 == 0)
                        Add(Part.HiHat, t0 + s * Step, Step * 0.4, GmDrum.ClosedHat, s % 4 == 0 ? 0.8f : 0.6f);
                    else if (busy && s != 15 && s != 7)
                        Add(Part.HiHat, t0 + s * Step, Step * 0.4, GmDrum.ClosedHat, 0.35f);
                }
            }

            /// <summary>A shaker on the sixteenths, plus a few high glints on chord tones — the fireflies' part.</summary>
            void Texture(Section section, double t0, int[] chord, Random random)
            {
                for (int s = 0; s < 16; s++)
                {
                    float accent = s % 4 == 2 ? 0.7f : 0.35f;
                    Add(Part.Texture, t0 + s * Step, Step * 0.5, 70, accent + (float)random.NextDouble() * 0.2f);
                }

                int glints = section.Energy < 0.4f ? 2 : 1;
                for (int g = 0; g < glints; g++)
                {
                    int s = random.Next(16);
                    int pitch = Above(88, chord[random.Next(3)]);
                    Add(Part.Texture, t0 + s * Step + Step * 0.03, Step * 6, pitch, 0.5f + (float)random.NextDouble() * 0.4f);
                }
            }

            // ---------------------------------------------------------------- bass

            void Sub(double t0, int[] chord)
            {
                int pitch = Above(28, chord[0]);
                Add(Part.SubBass, t0, BarLength * 0.5 - 0.02, pitch, 0.95f);
                Add(Part.SubBass, t0 + BarLength * 0.5, BarLength * 0.5 - 0.02, pitch, 0.8f);
            }

            void Bass(Section section, double t0, int[] chord, bool last)
            {
                int root = Above(33, chord[0]);
                int fifth = Nearest(root + 7, chord[2]);

                (int step, int pitch, float velocity)[] pattern = section.Energy >= 0.6f
                    ? new[] { (0, root, 1f), (3, root, 0.55f), (6, root + 12, 0.8f), (8, root, 0.9f), (10, fifth, 0.6f),
                        (11, root, 0.5f), (14, root + 12, 0.75f) }
                    : new[] { (0, root, 0.95f), (6, root, 0.6f), (8, root, 0.8f), (14, fifth, 0.6f) };

                foreach (var (step, pitch, velocity) in pattern)
                {
                    if (last && section.Roll && step > 8)
                        continue;
                    Add(Part.Bass, t0 + step * Step, Step * 1.6, pitch, velocity);
                }
            }

            // ---------------------------------------------------------------- harmony

            /// <summary>
            /// A four-voice chord per bar: the root in the bass, so the lowest pad note names the chord
            /// (the jellyfish take their colour from it), and the other three voice-led from the last chord.
            /// </summary>
            void Pads(double t0, int[] chord, int[] upper)
            {
                Add(Part.Pads, t0, BarLength - 0.03, Above(45, chord[0]), 0.7f);
                Lead(upper, 52, 76, chord[1], chord[2], chord[3]);
                foreach (int pitch in upper)
                    Add(Part.Pads, t0, BarLength - 0.03, pitch, 0.6f);
            }

            void Keys(Section section, double t0, int[] chord, int[] voices)
            {
                Lead(voices, 57, 84, chord[1], chord[2], chord[0]);

                if (section.BrokenKeys)
                {
                    // A broken chord in eighths, each note held across the next, so two or three arms are up at once.
                    int[] order = { 0, 1, 2, 1, 0, 1, 2, 1 };
                    for (int i = 0; i < 8; i++)
                    {
                        int pitch = voices[order[i]] + (i >= 4 && order[i] == 2 ? 12 : 0);
                        Add(Part.Keys, t0 + i * 2 * Step, Step * 3.5, pitch, i % 2 == 0 ? 0.7f : 0.55f);
                    }

                    return;
                }

                // Off-beat stabs.
                foreach (int s in new[] { 2, 6, 10, 14 })
                    foreach (int pitch in voices)
                        Add(Part.Keys, t0 + s * Step, Step * 1.5, pitch, s == 2 ? 0.75f : 0.6f);
            }

            /// <summary>Sixteenths up and down the chord over two octaves — the starlings trace this.</summary>
            void Arp(Section section, double t0, int[] chord)
            {
                int bottom = Above(64, chord[0]);
                var tones = new int[8];
                for (int i = 0; i < 8; i++)
                    tones[i] = Above(bottom + (i >= 4 ? 12 : 0), chord[i % 4]);

                Array.Sort(tones);
                int[] shape = { 0, 1, 2, 3, 4, 5, 6, 7, 6, 5, 4, 3, 2, 1, 2, 3 };
                int stride = section.Energy >= 0.6f ? 1 : 2;
                for (int s = 0; s < 16; s += stride)
                    Add(Part.Arp, t0 + s * Step, Step * stride * 0.8, tones[shape[s]], s % 4 == 0 ? 0.9f : 0.6f);
            }

            /// <summary>
            /// Three voices, one wolf each: a chord a bar in the quiet sections, two in the loud ones, with
            /// the top voice stepping up on the second so the highest wolf's howl climbs.
            /// </summary>
            void Choir(Section section, double t0, int[] chord, int[] voices)
            {
                Lead(voices, 52, 76, chord[2], chord[0], chord[1]);

                if (section.Energy < 0.6f)
                {
                    foreach (int pitch in voices)
                        Add(Part.Choir, t0, BarLength - 0.08, pitch, 0.65f);
                    return;
                }

                double half = BarLength * 0.5;
                foreach (int pitch in voices)
                    Add(Part.Choir, t0, half - 0.06, pitch, 0.75f);

                int top = 0;
                for (int i = 1; i < voices.Length; i++)
                    if (voices[i] > voices[top])
                        top = i;

                for (int i = 0; i < voices.Length; i++)
                {
                    int pitch = i == top ? Above(voices[i] + 1, chord[3]) : voices[i];
                    Add(Part.Choir, t0 + half, half - 0.06, pitch, 0.8f);
                }
            }

            /// <summary>
            /// Moves each voice to the nearest pitch of its target class — smooth voice leading, no leaps —
            /// folding back by an octave any voice that has drifted out of <paramref name="low"/>–<paramref name="high"/>.
            /// </summary>
            static void Lead(int[] voices, int low, int high, params int[] classes)
            {
                for (int i = 0; i < voices.Length; i++)
                {
                    int pitch = Nearest(voices[i], classes[i % classes.Length]);
                    while (pitch < low) pitch += 12;
                    while (pitch > high) pitch -= 12;
                    voices[i] = pitch;
                }

                Array.Sort(voices);
            }

            // ---------------------------------------------------------------- melody

            /// <summary>Rhythms for a two-bar phrase, in sixteenths: (start, length). Each has a long note for the parrot's wings.</summary>
            static readonly (int start, int length)[][] k_Rhythms =
            {
                new[] { (0, 3), (3, 3), (6, 2), (8, 8), (18, 2), (20, 2), (22, 2), (24, 8) },
                new[] { (2, 2), (4, 2), (6, 4), (10, 2), (12, 4), (16, 3), (19, 3), (22, 10) },
                new[] { (0, 6), (6, 2), (8, 2), (10, 6), (16, 2), (18, 2), (20, 4), (24, 6) },
                new[] { (0, 8), (12, 2), (14, 2), (16, 12) },
            };

            /// <summary>
            /// A two-bar tune as (step, length, pitch) triples: a rhythm, and a walk through the scale that
            /// lands on a chord tone on every strong beat.
            /// </summary>
            int[][] Tune(Section section, int phrase, int bar, Random random)
            {
                var rhythm = section.Energy < 0.4f ? k_Rhythms[3] : k_Rhythms[(section.Melody + phrase) % 3];
                int tonic = Tonic < 60 ? Tonic + 12 : Tonic;
                var notes = new int[rhythm.Length][];

                int degree = 2 + random.Next(3);
                for (int i = 0; i < rhythm.Length; i++)
                {
                    var (start, length) = rhythm[i];
                    degree += random.Next(-2, 3);
                    degree = Math.Max(0, Math.Min(9, degree));

                    int pitch = Degree(tonic, degree);
                    if (start % 4 == 0)
                    {
                        // Strong beats sit on the chord sounding then.
                        var chord = Chord(bar + start / 16);
                        int best = pitch, distance = int.MaxValue;
                        foreach (int pitchClass in new[] { chord[0], chord[1], chord[2] })
                        {
                            int candidate = Nearest(pitch, pitchClass);
                            if (Math.Abs(candidate - pitch) < distance)
                            {
                                distance = Math.Abs(candidate - pitch);
                                best = candidate;
                            }
                        }

                        pitch = best;
                    }

                    notes[i] = new[] { start, length, pitch };
                }

                return notes;
            }

            void Sing(double t0, int[][] tune)
            {
                foreach (var note in tune)
                {
                    double length = note[1] * Step;
                    Add(Part.Vocal, t0 + note[0] * Step, length - Math.Min(0.06, length * 0.2), note[2],
                        note[1] >= 6 ? 0.85f : 0.7f);
                }
            }
        }
    }
}
