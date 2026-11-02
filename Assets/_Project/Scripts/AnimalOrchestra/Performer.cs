using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRShootingGallery.AnimalOrchestra
{
    /// <summary>
    /// One animal of the orchestra. It listens to exactly one <see cref="Part"/> and owns its animal's
    /// motion: the notes give it discrete triggers (a stomp, a peck), the stem analysis gives it
    /// continuous signals (a glow, a swim speed), and both are read at the conductor's song time — the
    /// audio clock, the only timeline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A performer builds its own body (<see cref="Build"/>), called once by the scene builder in the
    /// Editor: the parts are ordinary GameObjects saved in the scene, and the performer keeps references
    /// to the ones it moves. Its root's origin is where it stands and its local +Z the way it faces; the
    /// builder places and turns the root.
    /// </para>
    /// <para>
    /// Every frame <see cref="Perform"/> gets the song's state through the properties here. When the song
    /// has no such part, or between songs, <see cref="Live"/> is false and the animal runs its idle loop,
    /// so a silent stem still looks alive.
    /// </para>
    /// </remarks>
    public abstract class Performer : MonoBehaviour
    {
        [SerializeField, Tooltip("The transport this performer reads its song time from.")]
        OrchestraConductor m_Conductor;

        [SerializeField, Tooltip("The part this animal plays. Set by the builder; each animal listens to exactly one.")]
        Part m_Part;

        [SerializeField, Range(0f, 2f), Tooltip("Visual gain: how strongly this animal answers its stem — mix the " +
            "visuals like audio. Scales its glows and the size of its continuous motion.")]
        float m_Gain = 1f;

        int m_SongSerial = -1;

        public Part Part => m_Part;

        public OrchestraConductor Conductor => m_Conductor;

        /// <summary>Seconds into the playing song on the audio clock; 0 between songs.</summary>
        protected double SongTime { get; private set; }

        /// <summary>A song is playing.</summary>
        protected bool SongPlaying { get; private set; }

        /// <summary>A song is playing and it has this animal's part. False means: idle.</summary>
        protected bool Live { get; private set; }

        /// <summary>This animal's notes in the playing song. Never null; empty when not <see cref="Live"/>.</summary>
        protected NoteTrack Notes { get; private set; } = NoteTrack.Empty;

        /// <summary>Beats since the song's first beat, fractional — or a 60 BPM clock between songs, so bobbing never stops.</summary>
        protected double Beats { get; private set; }

        /// <summary>
        /// The visual gain times the room's intensity (lowered in low intensity mode). Multiply every
        /// flash and glow by this.
        /// </summary>
        protected float Intensity => m_Gain * (m_Conductor != null ? m_Conductor.Intensity : 1f);

        protected float Gain => m_Gain;

        /// <summary>The fastest anything may flash, Hz. Lower in low intensity mode; a photosensitivity cap.</summary>
        protected float MaxFlashHz => m_Conductor != null ? m_Conductor.MaxFlashHz : 3f;

        /// <summary>
        /// 0 while a song plays, rising to 1 between songs — the room's idle state, when the fireflies fill
        /// the room and everything else breathes.
        /// </summary>
        protected float Idleness => m_Conductor != null ? m_Conductor.Idleness : 1f;

        /// <summary>Seconds, for idle loops. Not the song clock.</summary>
        protected static float Clock => Time.time;

        /// <summary>The builder's hook: which conductor and which part.</summary>
        public void Configure(OrchestraConductor conductor, Part part)
        {
            m_Conductor = conductor;
            m_Part = part;
        }

        /// <summary>
        /// Builds the animal's body under this transform, with the kit's meshes and materials, and keeps
        /// references to the parts it will move. Called in the Editor by the scene builder, on an empty
        /// GameObject; the result is saved with the scene.
        /// </summary>
        public abstract void Build(CreatureKit kit);

        protected virtual void Update()
        {
            var conductor = m_Conductor;
            SongPlaying = conductor != null && conductor.IsPlaying;
            SongTime = SongPlaying ? conductor.SongTime : 0.0;
            Live = SongPlaying && conductor.Has(m_Part);
            Notes = Live ? conductor.Track(m_Part) : NoteTrack.Empty;
            Beats = SongPlaying ? conductor.Beats : Time.timeAsDouble;

            int serial = conductor != null ? conductor.SongSerial : 0;
            if (serial != m_SongSerial)
            {
                m_SongSerial = serial;
                OnSongChanged();
            }

            Perform(Time.deltaTime);
        }

        /// <summary>Called on the first frame of each new song (and of each gap), for animals that keep per-song state.</summary>
        protected virtual void OnSongChanged() { }

        /// <summary>Poses the animal for this frame.</summary>
        protected abstract void Perform(float deltaTime);

        /// <summary>The stem's smoothed signal now, about 0–1, times the visual gain. 0 when not <see cref="Live"/>.</summary>
        protected float Level(Band band = Band.Level) =>
            Live ? m_Conductor.Level(m_Part, band) * m_Gain : 0f;

        /// <summary>
        /// The note a triggered move should be shaped around now, and <paramref name="tau"/>, the time from
        /// it (negative before) — feed both to <see cref="Motion.Hit"/>. The coming note wins once it is
        /// within its lead, so the windup starts on time; otherwise the last note, while its recovery runs.
        /// The lead shrinks to three quarters of the gap between notes, so fast repeats still each get a
        /// windup and a strike. False when neither applies: the animal is at rest.
        /// </summary>
        protected bool TryHit(float lead, float recover, out double tau, out float usedLead, out Note note, out int index)
        {
            tau = 0.0;
            usedLead = lead;
            note = default;
            index = -1;
            if (!Live)
                return false;

            int last = Notes.LastAtOrBefore(SongTime);
            int next = last + 1;

            // Skip notes that start together (chords, flams): the move is shaped around the group.
            while (next < Notes.Count && last >= 0 && Notes[next].Time - Notes[last].Time < 1e-4)
                next++;

            if (next < Notes.Count)
            {
                double until = Notes[next].Time - SongTime;
                double gap = last >= 0 ? Notes[next].Time - Notes[last].Time : double.MaxValue;
                usedLead = (float)Math.Min(lead, gap * 0.75);
                if (until <= usedLead)
                {
                    tau = -until;
                    note = Notes[next];
                    index = next;
                    return true;
                }
            }

            if (last >= 0)
            {
                double since = SongTime - Notes[last].Time;
                if (since < recover)
                {
                    tau = since;
                    note = Notes[last];
                    index = last;
                    return true;
                }
            }

            return false;
        }

        /// <summary>The last note to have started, and its index. False before the first, or when not <see cref="Live"/>.</summary>
        protected bool TryLast(out Note note, out int index)
        {
            index = Live ? Notes.LastAtOrBefore(SongTime) : -1;
            note = index >= 0 ? Notes[index] : default;
            return index >= 0;
        }

        /// <summary>The next note to start, and its index. False after the last, or when not <see cref="Live"/>.</summary>
        protected bool TryNext(out Note note, out int index)
        {
            index = Live ? Notes.LastAtOrBefore(SongTime) + 1 : Notes.Count;
            bool found = index < Notes.Count;
            note = found ? Notes[index] : default;
            return found;
        }

        /// <summary>The notes sounding now. Empty when not <see cref="Live"/>.</summary>
        protected void ActiveNotes(List<Note> into)
        {
            if (Live)
                Notes.ActiveAt(SongTime, into);
            else
                into.Clear();
        }
    }
}
