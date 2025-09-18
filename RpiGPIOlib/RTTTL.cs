using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

namespace RpiGPIO
{
    /// <summary>
    /// Class that contains music-related functionality for playing songs on buzzer
    /// </summary>
    public partial class Music
    {
        /// <summary>
        /// Default settings for RTTTL parsing
        /// </summary>
        private struct RtttlDefaults
        {
            public int Duration { get; set; }
            public int Octave { get; set; }
            public int Beat { get; set; }

            public RtttlDefaults(int duration = 4, int octave = 6, int beat = 63)
            {
                Duration = duration;
                Octave = octave;
                Beat = beat;
            }
        }

        /// <summary>
        /// Parses RTTTL defaults section
        /// </summary>
        private static RtttlDefaults ParseDefaults(string unparsedDefaults)
        {
            var defaults = new RtttlDefaults();

            foreach (var option in unparsedDefaults.Split(','))
            {
                var parts = option.Split('=');
                if (parts.Length != 2) continue;

                var key = parts[0].Trim();
                var value = parts[1].Trim();

                switch (key)
                {
                    case "d":
                        if (int.TryParse(value, out int duration))
                            defaults.Duration = duration;
                        break;
                    case "o":
                        if (int.TryParse(value, out int octave))
                            defaults.Octave = octave;
                        break;
                    case "b":
                        if (int.TryParse(value, out int beat))
                            defaults.Beat = beat;
                        break;
                }
            }

            return defaults;
        }

        /// <summary>
        /// Converts RTTTL melody string to Note array
        /// </summary>
        private static Note[] ParseMelody(string melody, RtttlDefaults defaults)
        {
            var notes = new[] { "c", "c#", "d", "d#", "e", "f", "f#", "g", "g#", "a", "a#", "b" };
            const double middleC = 261.63;

            var notePattern = @"(?<duration>1|2|4|8|16|32|64)?(?<note>(?:[a-g]|p)#?)(?<dot>\.?)(?<octave>4|5|6|7)?";
            var regex = new Regex(notePattern, RegexOptions.IgnoreCase);

            return melody.Split(',')
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(unparsedNote =>
                {
                    var match = regex.Match(unparsedNote.Trim());
                    if (!match.Success)
                        return new Note(0, 100); // Default rest note

                    // Parse duration
                    var duration = defaults.Duration;
                    if (match.Groups["duration"].Success && int.TryParse(match.Groups["duration"].Value, out int parsedDuration))
                        duration = parsedDuration;

                    // Parse note
                    var noteName = match.Groups["note"].Value.ToLower();
                    
                    // Parse dot (increases duration by 50%)
                    var hasDot = match.Groups["dot"].Success && match.Groups["dot"].Value == ".";
                    
                    // Parse octave
                    var octave = defaults.Octave;
                    if (match.Groups["octave"].Success && int.TryParse(match.Groups["octave"].Value, out int parsedOctave))
                        octave = parsedOctave;

                    // Calculate duration in milliseconds
                    var durationMs = (int)((240.0 / defaults.Beat / duration) * (hasDot ? 1.5 : 1) * 1000);

                    // Calculate frequency
                    int frequency = 0;
                    if (noteName != "p") // 'p' is pause/rest
                    {
                        var noteIndex = Array.IndexOf(notes, noteName);
                        if (noteIndex >= 0)
                        {
                            frequency = (int)(middleC * Math.Pow(2, octave - 4 + noteIndex / 12.0));
                        }
                    }

                    return new Note(frequency, durationMs);
                })
                .ToArray();
        }

        /// <summary>
        /// Parses a complete RTTTL string into a Note array
        /// </summary>
        /// <param name="rtttl">RTTTL format string (name:defaults:melody)</param>
        /// <returns>Array of Note objects</returns>
        public static Note[] ParseRtttl(string rtttl)
        {
            if (string.IsNullOrWhiteSpace(rtttl))
                throw new ArgumentException("RTTTL string cannot be null or empty", nameof(rtttl));

            var parts = rtttl.Split(':', 3);
            if (parts.Length < 3)
                throw new ArgumentException("Invalid RTTTL format. Expected format: name:defaults:melody", nameof(rtttl));

            var defaults = ParseDefaults(parts[1]);
            return ParseMelody(parts[2], defaults);
        }

        /// <summary>
        /// Plays an RTTTL melody once
        /// </summary>
        /// <param name="rtttl">RTTTL format string</param>
        public void PlayRtttlOnce(string rtttl)
        {
            var notes = ParseRtttl(rtttl);
            PlayNotesOnce(notes);
        }

        /// <summary>
        /// Plays an array of notes once
        /// </summary>
        private void PlayNotesOnce(Note[] notes)
        {
            StopPlaying();

            foreach (var note in notes)
            {
                if (note.Frequency > 0)
                {
                    _buzzer.TurnOn(note.Frequency);
                    System.Threading.Thread.Sleep(note.Duration);
                }
                else
                {
                    _buzzer.TurnOff();
                    System.Threading.Thread.Sleep(note.Duration);
                }
            }

            _buzzer.TurnOff();
        }
    }
}