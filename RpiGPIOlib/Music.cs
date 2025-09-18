using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace RpiGPIO
{
    /// <summary>
    /// Class that contains music-related functionality for playing songs on buzzer
    /// </summary>
    public partial class Music
    {
        // Reference to the buzzer to play notes
        private readonly Buzzer _buzzer;
        private CancellationTokenSource? _musicTokenSource;
        
        /// <summary>
        /// Represents a musical note with frequency and duration
        /// </summary>
        public struct Note
        {
            public int Frequency { get; }
            public int Duration { get; }
            
            public Note(int frequency, int duration)
            {
                Frequency = frequency;
                Duration = duration;
            }
        }

        private static readonly Note[] MissionImpossibleTheme = new[]
        {
            // Main theme (repeats 3x with slight variations)
            // First time
            new Note(784, 150), new Note(0, 50), // G5
            new Note(784, 150), new Note(0, 50), 
            new Note(932, 150), new Note(0, 50), // A#5/Bb5
            new Note(1047, 150), new Note(0, 50), // C6
            new Note(784, 150), new Note(0, 50), // G5
            new Note(784, 150), new Note(0, 50),
            new Note(699, 150), new Note(0, 50), // F5
            new Note(740, 150), new Note(0, 50), // F#5/Gb5
            
            // Second time, higher pitch
            new Note(784, 150), new Note(0, 50), // G5
            new Note(784, 150), new Note(0, 50),
            new Note(932, 150), new Note(0, 50), // A#5/Bb5
            new Note(1047, 150), new Note(0, 50), // C6
            new Note(784, 150), new Note(0, 50), // G5
            new Note(784, 150), new Note(0, 50),
            new Note(699, 150), new Note(0, 50), // F5
            new Note(740, 150), new Note(0, 50), // F#5/Gb5
            
            // Third time, with longer ending note
            new Note(784, 150), new Note(0, 50), // G5
            new Note(784, 150), new Note(0, 50),
            new Note(932, 150), new Note(0, 50), // A#5/Bb5
            new Note(1047, 150), new Note(0, 50), // C6
            new Note(784, 150), new Note(0, 50), // G5
            new Note(784, 150), new Note(0, 50),
            new Note(699, 150), new Note(0, 50), // F5
            new Note(740, 150), new Note(0, 400), // F#5/Gb5
            
            // Transition section
            new Note(622, 300), new Note(0, 100), // D#5/Eb5
            new Note(587, 300), new Note(0, 100), // D5
            new Note(523, 300), new Note(0, 100), // C5
            
            // Secondary theme
            new Note(466, 200), new Note(0, 50), // A#4/Bb4
            new Note(523, 150), new Note(0, 50), // C5
            new Note(587, 200), new Note(0, 50), // D5
            new Note(622, 150), new Note(0, 50), // D#5/Eb5
            new Note(587, 150), new Note(0, 50), // D5
            new Note(523, 200), new Note(0, 100), // C5
            
            new Note(466, 200), new Note(0, 50), // A#4/Bb4
            new Note(523, 150), new Note(0, 50), // C5
            new Note(587, 200), new Note(0, 50), // D5
            new Note(622, 150), new Note(0, 50), // D#5/Eb5
            new Note(587, 150), new Note(0, 50), // D5
            new Note(523, 200), new Note(0, 400), // C5
            
            // Back to main theme
            new Note(784, 150), new Note(0, 50), // G5
            new Note(784, 150), new Note(0, 50), 
            new Note(932, 150), new Note(0, 50), // A#5/Bb5
            new Note(1047, 150), new Note(0, 50), // C6
            new Note(784, 150), new Note(0, 50), // G5
            new Note(784, 150), new Note(0, 50),
            new Note(699, 150), new Note(0, 50), // F5
            new Note(740, 150), new Note(0, 50), // F#5/Gb5
            
            // Final time with extended ending
            new Note(784, 150), new Note(0, 50), // G5
            new Note(784, 150), new Note(0, 50),
            new Note(932, 150), new Note(0, 50), // A#5/Bb5
            new Note(1047, 150), new Note(0, 50), // C6
            new Note(784, 150), new Note(0, 50), // G5
            new Note(784, 150), new Note(0, 50),
            new Note(699, 150), new Note(0, 50), // F5
            new Note(740, 400), new Note(0, 200)  // F#5/Gb5 (longer final note)
        };
        
        // Super Mario theme notes and durations
        private static readonly Note[] SuperMarioTheme = new[]
        {
            new Note(660, 100), new Note(0, 150), new Note(660, 100), new Note(0, 300), new Note(660, 100), new Note(0, 300), new Note(510, 100), new Note(0, 100),
            new Note(660, 100), new Note(0, 300), new Note(770, 100), new Note(0, 550), new Note(380, 100), new Note(0, 575),
            
            new Note(510, 100), new Note(0, 450), new Note(380, 100), new Note(0, 400), new Note(320, 100), new Note(0, 500), new Note(440, 100), new Note(0, 300),
            new Note(480, 80), new Note(0, 330), new Note(450, 100), new Note(0, 150), new Note(430, 100), new Note(0, 300), new Note(380, 100), new Note(0, 200),
            
            new Note(660, 80), new Note(0, 200), new Note(760, 50), new Note(0, 150), new Note(860, 100), new Note(0, 300), new Note(700, 80), new Note(0, 150),
            new Note(760, 50), new Note(0, 350), new Note(660, 80), new Note(0, 300), new Note(520, 80), new Note(0, 150), new Note(580, 80), new Note(0, 150),
            
            new Note(480, 80), new Note(0, 500)
        };

        // Jingle Bells theme
        private static readonly Note[] JingleBells = new[]
        {
            new Note(659, 300), new Note(0, 50), new Note(659, 300), new Note(0, 50), new Note(659, 600), new Note(0, 100),  
            new Note(659, 300), new Note(0, 50), new Note(659, 300), new Note(0, 50), new Note(659, 600), new Note(0, 100),
            new Note(659, 300), new Note(0, 50), new Note(783, 300), new Note(0, 50), new Note(523, 300), new Note(0, 50), new Note(587, 300), new Note(0, 50),
            new Note(659, 1200), new Note(0, 200),
            
            new Note(698, 300), new Note(0, 50), new Note(698, 300), new Note(0, 50), new Note(698, 300), new Note(0, 50), new Note(698, 150), new Note(0, 50), new Note(698, 150), new Note(0, 50),
            new Note(698, 300), new Note(0, 50), new Note(659, 300), new Note(0, 50), new Note(659, 300), new Note(0, 50), new Note(659, 150), new Note(0, 50), new Note(659, 150), new Note(0, 50),
            new Note(659, 300), new Note(0, 50), new Note(587, 300), new Note(0, 50), new Note(587, 300), new Note(0, 50), new Note(659, 300), new Note(0, 50),
            new Note(587, 600), new Note(0, 100), new Note(783, 600), new Note(0, 100)
        };
        
        // Imperial March from Star Wars
        private static readonly Note[] ImperialMarch = new[]
        {
            // First phrase
            new Note(440, 500), new Note(0, 50), new Note(440, 500), new Note(0, 50), new Note(440, 500), new Note(0, 50),
            new Note(349, 350), new Note(0, 50), new Note(523, 150), new Note(0, 50), new Note(440, 500), new Note(0, 50),
            new Note(349, 350), new Note(0, 50), new Note(523, 150), new Note(0, 50), new Note(440, 1000), new Note(0, 100),
            
            // Second phrase
            new Note(659, 500), new Note(0, 50), new Note(659, 500), new Note(0, 50), new Note(659, 500), new Note(0, 50),
            new Note(698, 350), new Note(0, 50), new Note(523, 150), new Note(0, 50), new Note(415, 500), new Note(0, 50),
            new Note(349, 350), new Note(0, 50), new Note(523, 150), new Note(0, 50), new Note(440, 1000), new Note(0, 100),
            
            // Third phrase
            new Note(880, 500), new Note(0, 50), new Note(440, 350), new Note(0, 50), new Note(440, 150), new Note(0, 50),
            new Note(880, 500), new Note(0, 50), new Note(830, 250), new Note(0, 50), new Note(784, 250), new Note(0, 50),
            
            // Fourth phrase
            new Note(740, 125), new Note(0, 50), new Note(698, 125), new Note(0, 50), new Note(740, 250), new Note(0, 50),
            new Note(0, 250), new Note(445, 250), new Note(0, 50), new Note(523, 500), new Note(0, 50), new Note(466, 250), 
            new Note(0, 50), new Note(440, 250), new Note(0, 50),
            
            // Fifth phrase
            new Note(415, 125), new Note(0, 50), new Note(392, 125), new Note(0, 50), new Note(415, 250), new Note(0, 50),
            new Note(0, 250), new Note(349, 250), new Note(0, 50), new Note(523, 500), new Note(0, 50), new Note(440, 500), 
            new Note(0, 50), new Note(523, 250), new Note(0, 50), new Note(659, 500), new Note(0, 50), 
            new Note(880, 500), new Note(0, 50), new Note(440, 350), new Note(0, 50), new Note(440, 150), new Note(0, 50),
            
            // Final phrase
            new Note(880, 500), new Note(0, 50), new Note(830, 250), new Note(0, 50), new Note(784, 250), new Note(0, 50),
            new Note(740, 125), new Note(0, 50), new Note(698, 125), new Note(0, 50), new Note(740, 250), new Note(0, 50),
            new Note(0, 250), new Note(445, 250), new Note(0, 50), new Note(523, 500), new Note(0, 50), new Note(466, 250), 
            new Note(0, 50), new Note(440, 250), new Note(0, 50), new Note(415, 125), new Note(0, 50), 
            new Note(392, 125), new Note(0, 50), new Note(415, 250), new Note(0, 50), new Note(0, 250), 
            new Note(349, 250), new Note(0, 50), new Note(523, 500), new Note(0, 50), new Note(440, 1000), new Note(0, 100)
        };

        // CS:GO bomb ticking sound sequence
        private static readonly Note[] BombTicking = new[]
        {
            // Regular ticking pattern that speeds up over time
            // First phase - normal speed
            new Note(1000, 50), new Note(0, 950),
            new Note(1000, 50), new Note(0, 950),
            new Note(1000, 50), new Note(0, 950),
            new Note(1000, 50), new Note(0, 950),
            
            // Second phase - faster
            new Note(1000, 50), new Note(0, 750),
            new Note(1000, 50), new Note(0, 750),
            new Note(1000, 50), new Note(0, 750),
            new Note(1000, 50), new Note(0, 750),
            
            // Third phase - even faster
            new Note(1000, 50), new Note(0, 450),
            new Note(1000, 50), new Note(0, 450),
            new Note(1000, 50), new Note(0, 450),
            new Note(1000, 50), new Note(0, 450),
            
            // Fourth phase - very fast
            new Note(1000, 50), new Note(0, 250),
            new Note(1000, 50), new Note(0, 250),
            new Note(1000, 50), new Note(0, 250),
            new Note(1000, 50), new Note(0, 250),
            
            // Final phase - rapid beeps
            new Note(1000, 50), new Note(0, 100),
            new Note(1000, 50), new Note(0, 100),
            new Note(1000, 50), new Note(0, 100),
            new Note(1000, 50), new Note(0, 100),
            
            // Explosion sound
            new Note(4000, 2000)
        };

        // CS:GO bomb ticking sound sequence
        private static readonly Note[] SuccessMelody = new[]
        {
            new Note(1319, 120), // E6
            new Note(1568, 120), // G6
            new Note(1760, 180), // A6
            new Note(0, 60),
            new Note(1760, 120), // A6
            new Note(2093, 250)  // C7 (longer, final note)
        };

        /// <summary>
        /// Available songs that can be played
        /// </summary>
        public enum Song
        {
            SuperMario,
            JingleBells,
            ImperialMarch,
            MissionImpossible,
            BombTicking,
            SuccessMelody
        }

        public Music(Buzzer buzzer)
        {
            _buzzer = buzzer ?? throw new ArgumentNullException(nameof(buzzer));
        }
        
        public void PlaySongOnce(Song song)
        {
            // Stop any currently playing music
            StopPlaying();

            // Select the notes for the requested song
            Note[] notes = song switch
            {
                Song.SuperMario => SuperMarioTheme,
                Song.JingleBells => JingleBells,
                Song.ImperialMarch => ImperialMarch,
                Song.MissionImpossible => MissionImpossibleTheme,
                Song.BombTicking => BombTicking,
                Song.SuccessMelody => SuccessMelody,
                _ => SuperMarioTheme  // Default to Mario theme
            };

            // Play through all the notes in the song
            foreach (var note in notes)
            {
                if (note.Frequency > 0)
                {
                    // Play this note
                    _buzzer.TurnOn(note.Frequency);
                    Thread.Sleep(note.Duration);
                }
                else
                {
                    // This is a rest (pause)
                    _buzzer.TurnOff();
                    Thread.Sleep(note.Duration);
                }
            }

            // Short pause before repeating
            _buzzer.TurnOff();
        }
        
        /// <summary>
        /// Plays a song on the buzzer
        /// </summary>
        /// <param name="song">The song to play</param>
        public void PlaySong(Song song)
        {
            // Stop any currently playing music
            StopPlaying();

            _musicTokenSource = new CancellationTokenSource();
            var token = _musicTokenSource.Token;

            // Select the notes for the requested song
            Note[] notes = song switch
            {
                Song.SuperMario => SuperMarioTheme,
                Song.JingleBells => JingleBells,
                Song.ImperialMarch => ImperialMarch,
                Song.MissionImpossible => MissionImpossibleTheme,
                Song.BombTicking => BombTicking,
                Song.SuccessMelody => SuccessMelody,
                _ => SuperMarioTheme  // Default to Mario theme
            };

            Task.Run(() =>
            {
                try
                {
                    Console.WriteLine($"Playing {song}");
                    while (!token.IsCancellationRequested)
                    {
                        // Play through all the notes in the song
                        foreach (var note in notes)
                        {
                            if (token.IsCancellationRequested) break;

                            if (note.Frequency > 0)
                            {
                                // Play this note
                                _buzzer.TurnOn(note.Frequency);
                                Thread.Sleep(note.Duration);
                            }
                            else
                            {
                                // This is a rest (pause)
                                _buzzer.TurnOff();
                                Thread.Sleep(note.Duration);
                            }
                        }

                        // Short pause before repeating
                        if (!token.IsCancellationRequested)
                        {
                            _buzzer.TurnOff();
                            Thread.Sleep(1000); // 1 second pause between repetitions
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Task canceled, ensure buzzer is off
                    _buzzer.TurnOff();
                }
            }, token);
        }

        /// <summary>
        /// Plays the Super Mario theme
        /// </summary>
        public void PlaySuperMarioTheme()
        {
            PlaySong(Song.SuperMario);
        }
        
        /// <summary>
        /// Plays Jingle Bells
        /// </summary>
        public void PlayJingleBells()
        {
            PlaySong(Song.JingleBells);
        }
        
        /// <summary>
        /// Plays the Imperial March (Darth Vader's theme)
        /// </summary>
        public void PlayImperialMarch()
        {
            PlaySong(Song.ImperialMarch);
        }
        
        /// <summary>
        /// Plays a CS:GO bomb ticking countdown sound
        /// </summary>
        public void PlayBombTicking()
        {
            PlaySong(Song.BombTicking);
        }
        
        public void PlaySuccessMelody()
        {
            PlaySong(Song.SuccessMelody);
        }

        public void PlayRTTTLMissionImpossible()
        {
            
            string rtttl = "Mission Impossible:d=16,o=6,b=95:32d,32d#,32d,32d#,32d,32d#,32d,32d#,32d,32d,32d#,32e,32f,32f#,32g,g,8p,g,8p,a#,p,c7,p,g,8p,g,8p,f,p,f#,p,g,8p,g,8p,a#,p,c7,p,g,8p,g,8p,f,p,f#,p,a#,g,2d,32p,a#,g,2c#,32p,a#,g,2c,a#5,8c,2p,32p,a#5,g5,2f#,32p,a#5,g5,2f,32p,a#5,g5,2e,d#,8d";
            PlayRtttlOnce(rtttl);
        }

        public void PlayRTTTLIndianaJones()
        {
            string rtttl = "Indiana Jones:d=4,o=5,b=250:e,8p,8f,8g,8p,1c6,8p.,d,8p,8e,1f,p.,g,8p,8a,8b,8p,1f6,p,a,8p,8b,2c6,2d6,2e6,e,8p,8f,8g,8p,1c6,p,d6,8p,8e6,1f.6,g,8p,8g,e.6,8p,d6,8p,8g,e.6,8p,d6,8p,8g,f.6,8p,e6,8p,8d6,2c6";
            PlayRtttlOnce(rtttl);
        }

        public void PlayRTTTLStarWars()
        {
            string rtttl = "Star Wars:d=4,o=5,b=45:32p,32f#,32f#,32f#,8b.,8f#.6,32e6,32d#6,32c#6,8b.6,16f#.6,32e6,32d#6,32c#6,8b.6,16f#.6,32e6,32d#6,32e6,8c#.6,32f#,32f#,32f#,8b.,8f#.6,32e6,32d#6,32c#6,8b.6,16f#.6,32e6,32d#6,32c#6,8b.6,16f#.6,32e6,32d#6,32e6,8c#6";
            PlayRtttlOnce(rtttl);
        }

        public void PlayRTTTL007()
        {
            string rtttl = "007:o=5,d=4,b=320,b=320:c,8d,8d,d,2d,c,c,c,c,8d#,8d#,2d#,d,d,d,c,8d,8d,d,2d,c,c,c,c,8d#,8d#,d#,2d#,d,c#,c,c6,1b.,g,f,1g.";
            PlayRtttlOnce(rtttl);
        }

        public void PlayRTTTLAxelF()
        {
            PlayRtttlOnce("Axel:o=5,d=8,b=125,b=125:16g,16g,a#.,16g,16p,16g,c6,g,f,4g,d6.,16g,16p,16g,d#6,d6,a#,g,d6,g6,16g,16f,16p,16f,d,a#,2g,4p,16f6,d6,c6,a#,4g,a#.,16g,16p,16g,c6,g,f,4g,d6.,16g,16p,16g,d#6,d6,a#,g,d6,g6,16g,16f,16p,16f,d,a#,2g");
        }

        public void PlayBarbie()
        {
            PlayRtttlOnce("Barbie Girl:o=5,d=8,b=125,b=125:g#,e,g#,c#6,4a,4p,f#,d#,f#,b,4g#,f#,e,4p,e,c#,4f#,4c#,4p,f#,e,4g#,4f#");
        }

        public void PlayBennyHill()
        {
            PlayRtttlOnce("Benny Hill:o=5,d=16,b=125,b=125:8d.,e,8g,8g,e,d,a4,b4,d,b4,8e,d,b4,a4,b4,8a4,a4,a#4,b4,d,e,d,4g,4p,d,e,d,8g,8g,e,d,a4,b4,d,b4,8e,d,b4,a4,b4,8d,d,d,f#,a,8f,4d,4p,d,e,d,8g,g,g,8g,g,g,8g,8g,e,8e.,8c,8c,8c,8c,e,g,a,g,a#,8g,a,b,a#,b,a,b,8d6,a,b,d6,8b,8g,8d,e6,b,b,d,8a,8g,4g");
        }

        public void PlayBird()
        {
            PlayRtttlOnce("Birdy Song:o=5,d=16,b=100,b=100:g,g,a,a,e,e,8g,g,g,a,a,e,e,8g,g,g,a,a,c6,c6,8b,8b,8a,8g,8f,f,f,g,g,d,d,8f,f,f,g,g,d,d,8f,f,f,g,g,a,b,8c6,8a,8g,8e,4c");
        }

        public void PlayCantina()
        {
            PlayRtttlOnce("Cantina:o=5,d=8,b=250,b=250:a,p,d6,p,a,p,d6,p,a,d6,p,a,p,g#,4a,a,g#,a,4g,f#,g,f#,4f.,d.,16p,4p.,a,p,d6,p,a,p,d6,p,a,d6,p,a,p,g#,a,p,g,p,4g.,f#,g,p,c6,4a#,4a,4g");
        }

        public void PlayDream()
        {
            PlayRtttlOnce("Dream:o=4,d=8,b=220,b=220:c3,4p.,c,4p,d#3,p,d#,d#3,p,d#,p,d#3,p,f3,4p.,f,4p,g3,p,g,g3,p,a#3,p,c,p,c3,p,f5,p,c,p,c5,d#3,f5,d#,d#3,p,d#,f5,d#3,g5,f3,p,f5,p,f,p,f5,g3,g5,g,g3,p,a#3,p,c,p,c3,g5,f5,p,c,g5,c5,d#3,f5,d#,d#3,p,d#,f5,d#3,g5,f3,g,f5,p,f,g5,f5,g3,g5,g,g3,p,a#3,d#5,c,p,c3,g5,f5,c5,c,g5,c5,d#3,f5,d#,d#3,g5,d#,f5,d#3,g5,f3,g,f5,d#5,f,g5,f5,g3,g5,g,g3,f5,a#3,d#5,c,c5,c3,g5,f5,p,c,g5,c5,d#3,f5,d#,d#3,p,d#,f5,d#3,g5,f3,g,f5,p,f,g5,f5,g3,g5,g,g3,p,a#3,d#5,c,p,c3,p,f5,p,c,p,c5,d#3,f5,d#,d#3,p,d#,f5,d#3,g5,f3,p,f5,p,f,p,f5,g3,g5,g,g3,p,a#3,p,c,p,c3,4p.,c,4p,d#3,p,d#,d#3,p,d#,p,d#3,p,f3,4p.,f,4p,g3,p,g,g3,p,a#3,p,c");
        }

        public void PlayEntertainer()
        {
            PlayRtttlOnce("Entertainer:o=5,d=8,b=140,b=140:d,d#,e,4c6,e,4c6,e,2c6.,c6,d6,d#6,e6,c6,d6,4e6,b,4d6,2c6,4p,d,d#,e,4c6,e,4c6,e,2c6.,p,a,g,f#,a,c6,4e6,d6,c6,a,2d6");
        }


        // BEI VORLETZER NODE
        public void PlayFinalCountdown()
        {
            PlayRtttlOnce("Final Countdown:o=5,d=16,b=125,b=125:b,a,4b,4e,4p,8p,c6,b,8c6,8b,4a,4p,8p,c6,b,4c6,4e,4p,8p,a,g,8a,8g,8f#,8a,4g.,f#,g,4a.,g,a,8b,8a,8g,8f#,4e,4c6,2b.,b,c6,b,a,1b");
        }

        public void PlayPopcorn()
        {
            PlayRtttlOnce("Popcorn:o=5,d=16,b=160,b=160:a,p,g,p,a,p,e,p,c,p,e,p,8a4,8p,a,p,g,p,a,p,e,p,c,p,e,p,8a4,8p,a,p,b,p,c6,p,b,p,c6,p,a,p,b,p,a,p,b,p,g,p,a,p,g,p,a,p,f,8a,8p,a,p,g,p,a,p,e,p,c,p,e,p,8a4,8p,a,p,g,p,a,p,e,p,c,p,e,p,8a4,8p,a,p,b,p,c6,p,b,p,c6,p,a,p,b,p,a,p,b,p,g,p,a,p,g,p,a,p,b,4c6");
        }

        public void PlayRichMan()
        {
            PlayRtttlOnce("Rich Man's World:o=6,d=8,b=112,b=112:e,e,e,e,e,e,16e5,16a5,16c,16e,d#,d#,d#,d#,d#,d#,16f5,16a5,16c,16d#,4d,c,a5,c,4c,2a5,32a5,32c,32e,a6");
        }

        public void PlayTakeOnMe()
        {
            PlayRtttlOnce("Take On Me:o=5,d=8,b=160,b=160:f#,f#,f#,d,p,b4,p,e,p,e,p,e,g#,g#,a,b,a,a,a,e,p,d,p,f#,p,f#,p,f#,e,e,f#,e,f#,f#,f#,d,p,b4,p,e,p,e,p,e,g#,g#,a,b,a,a,a,e,p,d,p,f#,p,f#,p,f#,e,e5");
        }

        public void PlayYMCA()
        {
            PlayRtttlOnce("YMCA:o=5,d=8,b=160,b=160:c#6,a#,2p,a#,g#,f#,g#,a#,4c#6,a#,4c#6,d#6,a#,2p,a#,g#,f#,g#,a#,4c#6,a#,4c#6,d#6,b,2p,b,a#,g#,a#,b,4d#6,f#6,4d#6,4f6.,4d#6.,4c#6.,4b.,4a#,4g#");
        }


        
        /// <summary>
        /// Stops any currently playing music
        /// </summary>
        public void StopPlaying()
        {
            if (_musicTokenSource != null)
            {
                _musicTokenSource.Cancel();
                _musicTokenSource.Dispose();
                _musicTokenSource = null;
            }

            _buzzer.TurnOff();
        }
    }
}
