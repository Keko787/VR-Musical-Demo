using System;
using System.Collections.Generic;
using System.Text;

namespace VRShootingGallery.AnimalOrchestra
{
    /// <summary>
    /// A minimal Standard MIDI File reader: formats 0 and 1, the tempo map, track names and notes — all
    /// a song folder needs to drive the animals. Times come out in seconds, through the tempo map, so
    /// they sit on the same clock as the audio stems.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The plan left "a MIDI import package for Unity, or a minimal parser" open; this is the minimal
    /// parser. It reads note on/off (a note on at velocity 0 is a note off), running status, tempo meta
    /// events and track names, and skips everything else by its declared length. SMPTE time division is
    /// not supported — every DAW exports ticks per quarter note by default.
    /// </para>
    /// <para>
    /// Plain C#, no Unity, so it can be tested outside the Editor.
    /// </para>
    /// </remarks>
    public sealed class MidiFile
    {
        /// <summary>One track's notes. In a format 0 file, every channel's notes are in the one track.</summary>
        public sealed class Track
        {
            public string Name = "";
            public readonly List<(Note note, int channel)> Notes = new();
        }

        public readonly List<Track> Tracks = new();

        /// <summary>The first tempo in the file, in beats per minute; 120 if it sets none.</summary>
        public float Bpm { get; private set; } = 120f;

        /// <summary>Time of the first note in any track, seconds.</summary>
        public double FirstNoteTime { get; private set; }

        /// <summary>Parses a whole .mid file. Throws <see cref="FormatException"/> on a file it cannot read.</summary>
        public static MidiFile Parse(byte[] data)
        {
            var file = new MidiFile();
            var reader = new Reader(data);

            if (reader.Tag() != "MThd")
                throw new FormatException("Not a MIDI file (no MThd header).");

            int headerLength = reader.Int32();
            int format = reader.Int16();
            int trackCount = reader.Int16();
            int division = reader.Int16();
            reader.Skip(headerLength - 6);

            if ((division & 0x8000) != 0)
                throw new FormatException("SMPTE time division is not supported; export with ticks per quarter note.");
            if (format > 1)
                throw new FormatException($"MIDI format {format} is not supported (only 0 and 1).");

            // Read every track's raw events first: in format 1 the tempo map lives in the first track but
            // applies to all of them, so ticks can only become seconds once it is known.
            var tempos = new List<(long tick, int microsecondsPerQuarter)>();
            var raw = new List<(string name, List<(long tick, int channel, int pitch, int velocity)> events)>();

            for (int t = 0; t < trackCount && !reader.End; t++)
            {
                string tag = reader.Tag();
                int length = reader.Int32();
                int end = reader.Position + length;
                if (tag != "MTrk")
                {
                    reader.Seek(end);
                    t--;
                    continue;
                }

                var events = new List<(long, int, int, int)>();
                string name = "";
                long tick = 0;
                int status = 0;

                while (reader.Position < end)
                {
                    tick += reader.VariableLength();
                    int b = reader.Byte();
                    if (b < 0x80)
                    {
                        // Running status: the byte is the first data byte of a repeat of the last status.
                        reader.Seek(reader.Position - 1);
                        b = status;
                    }

                    if (b == 0xFF)
                    {
                        int type = reader.Byte();
                        int metaLength = (int)reader.VariableLength();
                        if (type == 0x51 && metaLength == 3)
                            tempos.Add((tick, (reader.Byte() << 16) | (reader.Byte() << 8) | reader.Byte()));
                        else if (type == 0x03)
                            name = reader.Text(metaLength);
                        else if (type == 0x2F)
                        {
                            reader.Skip(metaLength);
                            break;
                        }
                        else
                            reader.Skip(metaLength);
                        continue;
                    }

                    if (b == 0xF0 || b == 0xF7)
                    {
                        reader.Skip((int)reader.VariableLength());
                        continue;
                    }

                    status = b;
                    int kind = b & 0xF0;
                    int channel = b & 0x0F;
                    switch (kind)
                    {
                        case 0x80:
                        {
                            int pitch = reader.Byte();
                            reader.Byte();
                            events.Add((tick, channel, pitch, 0));
                            break;
                        }
                        case 0x90:
                        {
                            int pitch = reader.Byte();
                            int velocity = reader.Byte();
                            events.Add((tick, channel, pitch, velocity));
                            break;
                        }
                        case 0xA0:
                        case 0xB0:
                        case 0xE0:
                            reader.Skip(2);
                            break;
                        case 0xC0:
                        case 0xD0:
                            reader.Skip(1);
                            break;
                        default:
                            throw new FormatException($"Unexpected status byte 0x{b:X2} in track {t}.");
                    }
                }

                reader.Seek(end);
                raw.Add((name, events));
            }

            tempos.Sort((a, b) => a.tick.CompareTo(b.tick));
            if (tempos.Count > 0)
                file.Bpm = 60_000_000f / tempos[0].microsecondsPerQuarter;

            var clock = new TempoMap(tempos, division);
            double first = double.MaxValue;

            foreach (var (name, events) in raw)
            {
                var track = new Track { Name = name };
                var open = new Dictionary<int, Queue<(long tick, int velocity)>>();

                // Pair each note off with the earliest still-open note on of the same pitch and channel.
                foreach (var (tick, channel, pitch, velocity) in events)
                {
                    int key = channel * 128 + pitch;
                    if (velocity > 0)
                    {
                        if (!open.TryGetValue(key, out var queue))
                            open[key] = queue = new Queue<(long, int)>();
                        queue.Enqueue((tick, velocity));
                    }
                    else if (open.TryGetValue(key, out var queue) && queue.Count > 0)
                    {
                        var (startTick, startVelocity) = queue.Dequeue();
                        double start = clock.Seconds(startTick);
                        double duration = Math.Max(0.01, clock.Seconds(tick) - start);
                        track.Notes.Add((new Note(start, duration, pitch, startVelocity / 127f), channel));
                        first = Math.Min(first, start);
                    }
                }

                track.Notes.Sort((a, b) => a.note.CompareTo(b.note));
                file.Tracks.Add(track);
            }

            file.FirstNoteTime = first == double.MaxValue ? 0.0 : first;
            return file;
        }

        /// <summary>Finds a track by name, ignoring case, or by its index written as a number; null if neither matches.</summary>
        public Track Find(string nameOrIndex)
        {
            if (string.IsNullOrEmpty(nameOrIndex))
                return null;

            foreach (var track in Tracks)
                if (string.Equals(track.Name.Trim(), nameOrIndex.Trim(), StringComparison.OrdinalIgnoreCase))
                    return track;

            return int.TryParse(nameOrIndex, out int index) && index >= 0 && index < Tracks.Count ? Tracks[index] : null;
        }

        /// <summary>Ticks to seconds through the tempo changes.</summary>
        sealed class TempoMap
        {
            readonly List<(long tick, double seconds, double secondsPerTick)> m_Segments = new();

            public TempoMap(List<(long tick, int microsecondsPerQuarter)> tempos, int division)
            {
                double perTick = 0.5 / division;   // 120 BPM until told otherwise
                long lastTick = 0;
                double seconds = 0.0;
                m_Segments.Add((0, 0.0, perTick));

                foreach (var (tick, microseconds) in tempos)
                {
                    seconds += (tick - lastTick) * perTick;
                    perTick = microseconds / 1_000_000.0 / division;
                    lastTick = tick;
                    m_Segments.Add((tick, seconds, perTick));
                }
            }

            public double Seconds(long tick)
            {
                var segment = m_Segments[0];
                foreach (var s in m_Segments)
                {
                    if (s.tick > tick)
                        break;
                    segment = s;
                }

                return segment.seconds + (tick - segment.tick) * segment.secondsPerTick;
            }
        }

        /// <summary>Big-endian reads over the file's bytes.</summary>
        sealed class Reader
        {
            readonly byte[] m_Data;

            public Reader(byte[] data) => m_Data = data;

            public int Position { get; private set; }

            public bool End => Position >= m_Data.Length;

            public void Seek(int position) => Position = Math.Max(0, Math.Min(m_Data.Length, position));

            public void Skip(int count) => Seek(Position + count);

            public int Byte()
            {
                if (Position >= m_Data.Length)
                    throw new FormatException("Unexpected end of MIDI file.");
                return m_Data[Position++];
            }

            public int Int16() => (Byte() << 8) | Byte();

            public int Int32() => (Byte() << 24) | (Byte() << 16) | (Byte() << 8) | Byte();

            public long VariableLength()
            {
                long value = 0;
                for (int i = 0; i < 4; i++)
                {
                    int b = Byte();
                    value = (value << 7) | (uint)(b & 0x7F);
                    if ((b & 0x80) == 0)
                        break;
                }

                return value;
            }

            public string Tag()
            {
                var chars = new char[4];
                for (int i = 0; i < 4; i++)
                    chars[i] = (char)Byte();
                return new string(chars);
            }

            public string Text(int length)
            {
                int start = Position;
                Skip(length);
                return Encoding.UTF8.GetString(m_Data, start, Math.Min(length, m_Data.Length - start));
            }
        }
    }
}
