using ConvertHero.Core.Models;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;
using Melanchall.DryWetMidi.Standards;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ConvertHero.Core.Services
{
    public interface IChartService
    {
        Stream ConvertToChart(string SongName);
    }
    public class ChartService : IChartService
    {
        private int ChartResolution;
        private List<SyncEvent> SyncTrack;
        private IEnumerable<NoteTrack> LeadTracks;

        public ChartService(Stream midi)
        {
            LoadMidiFile(midi);
        }
        public Stream ConvertToChart(string SongName)
        {
            string tempFile = Path.GetTempFileName();
            Stream result = new MemoryStream();
            using (StreamWriter writer = new StreamWriter(result))
            {
                // Write SONG section
                writer.WriteLine(string.Format(Resources.SongSection, ChartResolution, SongName));

                // Write SYNC section
                writer.WriteLine(string.Format(Resources.SyncTrack, string.Join("\n", SyncTrack.Select(s => s.ToString()))));

                // Write EVENTS section
                List<string> eventStrings = SyncTrack.Where(s => s.BeatsPerMinute > 0).Select(s => s.ToEventString()).ToList();
                writer.WriteLine(string.Format(Resources.EventsTrack, string.Join("\n", eventStrings)));

                // LEAD TRACKS
                List<NoteTrack> leadTracks = new List<NoteTrack>();
                foreach (object obj in LeadTracks)
                {
                    leadTracks.Add(obj as NoteTrack);
                }

                if (leadTracks.Count > 0)
                {
                    NoteTrack lead = MergeManyTracks(leadTracks, CloneHeroInstrument.Single);
                    lead.CloneHeroInstrument = CloneHeroInstrument.Single.ToString();

                    // Write Note section
                    writer.WriteLine(string.Format(Resources.NoteTrack, $"{CloneHeroDifficulty.Expert}{CloneHeroInstrument.Single}", string.Join("\n", lead.Notes.Select(n => n.ToString()))));
                }
            }
            return result;
        }

        private void LoadMidiFile(Stream midiStream)
        {
            MidiFile midiFile = MidiFile.Read(midiStream);
            TicksPerQuarterNoteTimeDivision x = midiFile.TimeDivision as TicksPerQuarterNoteTimeDivision;
            ChartResolution = x.TicksPerQuarterNote;
            SyncTrack = LoadSyncEvents(midiFile);
            IEnumerable<NoteTrack> trackList = LoadNoteEvents(midiFile);
            // We only want to do the lead
            LeadTracks = trackList;
            return;
        }

        public IEnumerable<NoteTrack> LoadNoteEvents(MidiFile midiFile)
        {
            List<NoteTrack> tracks = new List<NoteTrack>();
            OutputDevice outputDevice = OutputDevice.GetAll().First();
            List<TrackChunk> trackChunks = midiFile.GetTrackChunks().ToList();
            foreach (TrackChunk channel in trackChunks)
            {
                string trackName = GetChannelTitle(channel);
                GeneralMidiProgram instrument = GetChannelInstrument(channel);
                List<ChartEvent> channelTrack = new List<ChartEvent>();
                int channel_index = -1;
                foreach (Note note in channel.GetNotes())
                {
                    channelTrack.Add(new ChartEvent(note.Time, note.NoteNumber, note.Length));
                    channel_index = note.Channel;
                }

                if (channelTrack.Count > 0)
                {
                    NoteTrack track = new NoteTrack(channelTrack, new List<SyncEvent>(SyncTrack), ChartResolution, instrument, channel.GetPlayback(midiFile.GetTempoMap(), outputDevice), trackName);
                    tracks.Add(track);
                }
            }

            return tracks;
        }

        private List<SyncEvent> LoadSyncEvents(MidiFile midiFile)
        {
            List<SyncEvent> syncTrack = new List<SyncEvent>();
            foreach (var tempoEvent in midiFile.GetTempoMap().GetTempoChanges())
            {
                double bpm = 60000000.0 / tempoEvent.Value.MicrosecondsPerQuarterNote;
                syncTrack.Add(new SyncEvent(tempoEvent.Time, bpm));
            }

            foreach (var timeSignatureEvent in midiFile.GetTempoMap().GetTimeSignatureChanges())
            {
                syncTrack.Add(new SyncEvent(timeSignatureEvent.Time, timeSignatureEvent.Value.Numerator, timeSignatureEvent.Value.Denominator));
            }

            return syncTrack.OrderBy(k => k.Tick).ToList();
        }

        private string GetChannelTitle(TrackChunk channel)
        {
            // Get Channel Names
            TimedEvent trackNameEvent = channel.GetTimedEvents().Where(e => e.Event is SequenceTrackNameEvent).FirstOrDefault();
            if (trackNameEvent == null)
            {
                return $"Untitled";
            }

            SequenceTrackNameEvent ev = trackNameEvent.Event as SequenceTrackNameEvent;
            return ev.Text;
        }

        /// <summary>
        /// Reads the instrument that is playing the tones in the specified channel.
        /// </summary>
        /// <param name="channel">
        /// The midi channel for a single instrument.
        /// </param>
        /// <returns>
        /// The Midi instrument code for the channel.
        /// </returns>
        private static GeneralMidiProgram GetChannelInstrument(TrackChunk channel)
        {
            TimedEvent programChangeEvent = channel.GetTimedEvents().Where(e => e.Event is ProgramChangeEvent).FirstOrDefault();
            if (programChangeEvent == null)
            {
                return GeneralMidiProgram.DistortionGuitar;
            }

            ProgramChangeEvent ev = programChangeEvent.Event as ProgramChangeEvent;
            return (GeneralMidiProgram)(int)ev.ProgramNumber;
        }
        private NoteTrack MergeManyTracks(List<NoteTrack> tracks, CloneHeroInstrument instrument)
        {
            if (tracks == null || tracks.Count == 0)
            {
                return null;
            }

            NoteTrack merged = null;
            if (tracks.Count == 1)
            {
                merged = tracks.First();
            }
            else
            {
                // Merge tracks
                merged = tracks.First();
                for (int i = 1; i < tracks.Count; i++)
                {
                    merged = MergeTwoTracks(merged, tracks[i]);
                }
            }

            // Map notes onto guitar hero highway system depending on the type of instrument it is (guitar/bass/drums).
            switch (instrument)
            {
                case CloneHeroInstrument.Single:
                    merged.GuitarReshape();
                    break;
                case CloneHeroInstrument.DoubleBass:
                    merged.BassGuitarReshape();
                    break;
                case CloneHeroInstrument.Drums:
                    merged.DrumReshape();
                    break;
                default:
                    merged.GuitarReshape();
                    break;
            }

            return merged;
        }

        private NoteTrack MergeTwoTracks(NoteTrack primary, NoteTrack secondary)
        {
            // If there are 4 beats of rest, the event is considered to be the end of the chunk
            int gapTicks = ChartResolution * 4;
            long chunkStartTick = -1;
            long chunkStopTick = -1;
            List<ChartEvent> chunk = new List<ChartEvent>();
            foreach (ChartEvent ev in primary.Notes)
            {
                if (chunkStartTick < 0)
                {
                    chunkStartTick = ev.Tick;
                    chunkStopTick = ev.Tick + ev.Sustain + gapTicks;
                }

                // If this event happens more than 4 beats after the previous event ended then we have reached the end of a chunk
                if (ev.Tick > chunkStopTick)
                {
                    OverwriteChunk(chunk, secondary);
                    chunk.Clear();
                    chunkStartTick = ev.Tick;
                    chunkStopTick = ev.Tick + ev.Sustain + gapTicks;
                }
                else
                {
                    // Push the stop tick value out if we can.
                    if (ev.Tick + ev.Sustain + gapTicks > chunkStopTick)
                    {
                        chunkStopTick = ev.Tick + ev.Sustain + gapTicks;
                    }

                    chunk.Add(ev);
                }
            }

            // Overwrite any notes that were saved in the chunk list.
            OverwriteChunk(chunk, secondary);

            return secondary;
        }

        private void OverwriteChunk(List<ChartEvent> chunk, NoteTrack track)
        {
            if (chunk == null || chunk.Count == 0)
            {
                return;
            }

            long startTick = chunk.First().Tick;
            long endTick = 0;
            foreach (ChartEvent ev in chunk)
            {
                if (ev.Tick + ev.Sustain > endTick)
                {
                    endTick = ev.Tick + ev.Sustain;
                }
            }

            // Remove all ChartEvents in track with tick >= startTick && tick <= endTick
            List<ChartEvent> merged = track.Notes.Where(n => n.Tick < startTick || n.Tick > endTick).ToList();
            merged.AddRange(chunk);
            track.Notes = merged.OrderBy(n => n.Tick).ToList();
        }
    }
}
