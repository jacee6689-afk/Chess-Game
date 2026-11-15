using System;
using UnityEngine;

namespace ChessGame
{
    /// <summary>
    /// Procedural acoustic audio engine for authentic wooden chess sound effects.
    /// Synthesizes high-fidelity 44.1kHz audio clips on the fly with zero external dependencies.
    /// </summary>
    public class ChessAudio : MonoBehaviour
    {
        public static ChessAudio Instance { get; private set; }

        private AudioSource audioSource;
        private AudioSource tickSource;
        private AudioSource musicSource;

        private AudioClip moveClip;
        private AudioClip captureClip;
        private AudioClip checkClip;
        private AudioClip castleClip;
        private AudioClip promoteClip;
        private AudioClip victoryClip;
        private AudioClip defeatClip;
        private AudioClip drawClip;
        private AudioClip tickClip;
        private AudioClip illegalClip;
        private AudioClip musicClip;

        public bool SoundEnabled
        {
            get => ChessSettings.SoundEnabled;
            set => ChessSettings.SoundEnabled = value;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D clean stereo audio
            audioSource.volume = 0.85f;

            tickSource = gameObject.AddComponent<AudioSource>();
            tickSource.playOnAwake = false;
            tickSource.spatialBlend = 0f;
            tickSource.volume = 0.4f;

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;
            musicSource.loop = true;

            GenerateAllClips();
            musicClip = CreateMusicClip();
            musicSource.clip = musicClip;
            musicSource.Play();
            ChessSettings.OnSettingsChanged += ApplyMusicSettings;
            ApplyMusicSettings();
        }

        private void OnDestroy()
        {
            ChessSettings.OnSettingsChanged -= ApplyMusicSettings;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private AudioClip CreateMusicClip()
        {
            const int sampleRate = 22050;
            const int beatsPerLoop = 16;
            const float beatsPerMinute = 80f;
            float beatDuration = 60f / beatsPerMinute;
            int totalSamples = Mathf.RoundToInt(sampleRate * beatDuration * beatsPerLoop);
            float[] samples = new float[totalSamples];
            const int waveTableSize = 2048;
            float[] sineTable = new float[waveTableSize];
            for (int i = 0; i < sineTable.Length; i++)
            {
                sineTable[i] = Mathf.Sin(2f * Mathf.PI * i / waveTableSize);
            }

            int[] melodyNotes =
            {
                62, 69, 66, 69, // D major
                59, 66, 69, 66, // B minor
                55, 62, 71, 62, // G major
                57, 64, 67, 64  // A minor
            };
            int[] bassNotes = { 38, 35, 43, 45 };

            for (int bar = 0; bar < bassNotes.Length; bar++)
            {
                AddMusicNote(samples, sineTable, sampleRate, bassNotes[bar], bar * 4 * beatDuration, 2.4f, 0.11f, 1.5f);
                for (int beat = 0; beat < 4; beat++)
                {
                    int noteIndex = bar * 4 + beat;
                    AddMusicNote(samples, sineTable, sampleRate, melodyNotes[noteIndex], noteIndex * beatDuration, 1.25f, 0.15f, 2.4f);
                }
            }

            int edgeSamples = Mathf.RoundToInt(sampleRate * 0.005f);
            for (int i = 0; i < edgeSamples; i++)
            {
                float fadeIn = (float)i / edgeSamples;
                float fadeOut = (float)(totalSamples - 1 - i) / edgeSamples;
                samples[i] *= fadeIn;
                samples[totalSamples - 1 - i] *= fadeOut;
            }

            AudioClip clip = AudioClip.Create("ChessStudyMusic", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static void AddMusicNote(float[] samples, float[] sineTable, int sampleRate, int midiNote, float startTime, float duration, float volume, float decay)
        {
            int startSample = Mathf.RoundToInt(startTime * sampleRate);
            int sampleCount = Mathf.Min(Mathf.RoundToInt(duration * sampleRate), samples.Length - startSample);
            float frequency = 440f * Mathf.Pow(2f, (midiNote - 69) / 12f);
            float phaseStep = frequency * sineTable.Length / sampleRate;
            float phase = 0f;
            int tableMask = sineTable.Length - 1;

            for (int i = 0; i < sampleCount; i++)
            {
                float time = (float)i / sampleRate;
                float attack = Mathf.Min(1f, time / 0.018f);
                float envelope = attack * Mathf.Exp(-decay * time);
                float wave = SampleSine(sineTable, phase, 1f, tableMask)
                           + 0.25f * SampleSine(sineTable, phase, 2f, tableMask)
                           + 0.08f * SampleSine(sineTable, phase, 3f, tableMask);
                samples[startSample + i] += wave * envelope * volume;
                phase += phaseStep;
                if (phase >= sineTable.Length)
                {
                    phase -= sineTable.Length;
                }
            }
        }

        private static float SampleSine(float[] sineTable, float phase, float harmonic, int tableMask)
        {
            float tablePosition = phase * harmonic;
            int index = (int)tablePosition & tableMask;
            int nextIndex = (index + 1) & tableMask;
            float blend = tablePosition - (int)tablePosition;
            return Mathf.Lerp(sineTable[index], sineTable[nextIndex], blend);
        }

        private void ApplyMusicSettings()
        {
            if (musicSource == null)
            {
                return;
            }

            musicSource.volume = ChessSettings.MusicEnabled ? Mathf.Clamp01(ChessSettings.MusicVolume) : 0f;
            if (ChessSettings.MusicEnabled)
            {
                musicSource.UnPause();
            }
            else
            {
                musicSource.Pause();
            }
        }

        private void GenerateAllClips()
        {
            moveClip = CreateMoveClip();
            captureClip = CreateCaptureClip();
            checkClip = CreateCheckClip();
            castleClip = CreateCastleClip();
            promoteClip = CreatePromoteClip();
            victoryClip = CreateVictoryClip();
            defeatClip = CreateDefeatClip();
            drawClip = CreateDrawClip();
            tickClip = CreateTickClip();
            illegalClip = CreateIllegalClip();
        }

        private AudioClip CreateMoveClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.12f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;

                // Sharp initial impulse (contact click)
                float click = (t < 0.003f) ? (Mathf.Sin(2f * Mathf.PI * 2400f * t) * (1f - t / 0.003f)) : 0f;

                // Damped body resonance of wood (150 Hz & 310 Hz)
                float decay1 = Mathf.Exp(-t * 45f);
                float decay2 = Mathf.Exp(-t * 70f);
                float body = Mathf.Sin(2f * Mathf.PI * 155f * t) * decay1 * 0.7f
                           + Mathf.Sin(2f * Mathf.PI * 310f * t) * decay2 * 0.35f;

                // Soft table thump
                float thump = Mathf.Sin(2f * Mathf.PI * 85f * t) * Mathf.Exp(-t * 60f) * 0.4f;

                samples[i] = Mathf.Clamp(click * 0.6f + body + thump, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("ChessMove", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateCaptureClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.18f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;

                // Dual impact: Piece hits piece at t=0, then settles on board at t=0.016
                float t1 = t;
                float click1 = (t1 < 0.004f) ? Mathf.Sin(2f * Mathf.PI * 2800f * t1) * (1f - t1 / 0.004f) : 0f;
                float body1 = Mathf.Sin(2f * Mathf.PI * 190f * t1) * Mathf.Exp(-t1 * 40f) * 0.75f;

                float t2 = t - 0.016f;
                float click2 = (t2 > 0f && t2 < 0.004f) ? Mathf.Sin(2f * Mathf.PI * 2100f * t2) * (1f - t2 / 0.004f) : 0f;
                float body2 = (t2 > 0f) ? Mathf.Sin(2f * Mathf.PI * 125f * t2) * Mathf.Exp(-t2 * 35f) * 0.85f : 0f;

                // Heavy impact knock
                float subKnock = Mathf.Sin(2f * Mathf.PI * 72f * t) * Mathf.Exp(-t * 50f) * 0.5f;

                samples[i] = Mathf.Clamp(click1 * 0.5f + body1 + click2 * 0.6f + body2 + subKnock, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("ChessCapture", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateCheckClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.35f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;

                // Crisp wooden tap base
                float tap = Mathf.Sin(2f * Mathf.PI * 180f * t) * Mathf.Exp(-t * 50f) * 0.5f;

                // Bright alert harmonic chime (880 Hz + 1320 Hz)
                float chimeDecay = Mathf.Exp(-t * 9f);
                float chime = (Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.55f
                             + Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.35f
                             + Mathf.Sin(2f * Mathf.PI * 1760f * t) * 0.15f) * chimeDecay;

                samples[i] = Mathf.Clamp(tap + chime, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("ChessCheck", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateCastleClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.26f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;

                // First tap (King landing) at t=0
                float tap1 = Mathf.Sin(2f * Mathf.PI * 165f * t) * Mathf.Exp(-t * 45f) * 0.65f;

                // Slide friction noise between 0.02 and 0.09s
                float slide = 0f;
                if (t >= 0.02f && t < 0.09f)
                {
                    float noise = (Mathf.PingPong(t * 12345.67f, 1f) - 0.5f) * 2f;
                    slide = noise * Mathf.Sin((t - 0.02f) / 0.07f * Mathf.PI) * 0.18f;
                }

                // Second tap (Rook landing) at t=0.11s
                float t2 = t - 0.11f;
                float tap2 = (t2 > 0f) ? Mathf.Sin(2f * Mathf.PI * 210f * t2) * Mathf.Exp(-t2 * 45f) * 0.7f : 0f;

                samples[i] = Mathf.Clamp(tap1 + slide + tap2, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("ChessCastle", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreatePromoteClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.42f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            // 3-note ascending arpeggio: C5 (523Hz), E5 (659Hz), G5 (784Hz)
            float[] notes = { 523.25f, 659.25f, 783.99f };
            float[] noteTimes = { 0.0f, 0.09f, 0.18f };

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float sample = 0f;

                for (int n = 0; n < notes.Length; n++)
                {
                    float tn = t - noteTimes[n];
                    if (tn >= 0f)
                    {
                        float decay = Mathf.Exp(-tn * 10f);
                        sample += (Mathf.Sin(2f * Mathf.PI * notes[n] * tn) * 0.4f
                                 + Mathf.Sin(2f * Mathf.PI * (notes[n] * 2f) * tn) * 0.15f) * decay;
                    }
                }

                samples[i] = Mathf.Clamp(sample, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("ChessPromote", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateVictoryClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.65f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            // Triumphant chord: C5 (523Hz), G5 (784Hz), C6 (1046Hz)
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-t * 5f);
                float val = (Mathf.Sin(2f * Mathf.PI * 523.25f * t) * 0.35f
                           + Mathf.Sin(2f * Mathf.PI * 783.99f * t) * 0.35f
                           + Mathf.Sin(2f * Mathf.PI * 1046.50f * t) * 0.30f) * decay;
                samples[i] = Mathf.Clamp(val, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("ChessVictory", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateDefeatClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.65f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            // Descending minor interval: A4 (440Hz) down to F4 (349Hz)
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float freq = Mathf.Lerp(440f, 329.63f, Mathf.Clamp01(t / 0.5f));
                float decay = Mathf.Exp(-t * 4.5f);
                float val = Mathf.Sin(2f * Mathf.PI * freq * t) * decay * 0.6f;
                samples[i] = Mathf.Clamp(val, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("ChessDefeat", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateDrawClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.45f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            // Gentle neutral bell: D5 (587Hz) + A5 (880Hz)
            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-t * 6f);
                float val = (Mathf.Sin(2f * Mathf.PI * 587.33f * t) * 0.45f
                           + Mathf.Sin(2f * Mathf.PI * 880.00f * t) * 0.35f) * decay;
                samples[i] = Mathf.Clamp(val, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("ChessDraw", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateTickClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.04f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-t * 120f);
                float val = Mathf.Sin(2f * Mathf.PI * 1600f * t) * decay * 0.4f;
                samples[i] = Mathf.Clamp(val, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("ChessTick", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateIllegalClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.08f;
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-t * 70f);
                float val = Mathf.Sin(2f * Mathf.PI * 95f * t) * decay * 0.5f;
                samples[i] = Mathf.Clamp(val, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("ChessIllegal", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public void PlayMove() { if (SoundEnabled && moveClip != null) audioSource.PlayOneShot(moveClip, 0.85f * ChessSettings.SoundVolume); }
        public void PlayCapture() { if (SoundEnabled && captureClip != null) audioSource.PlayOneShot(captureClip, 0.95f * ChessSettings.SoundVolume); }
        public void PlayCheck() { if (SoundEnabled && checkClip != null) audioSource.PlayOneShot(checkClip, 0.90f * ChessSettings.SoundVolume); }
        public void PlayCastle() { if (SoundEnabled && castleClip != null) audioSource.PlayOneShot(castleClip, 0.90f * ChessSettings.SoundVolume); }
        public void PlayPromote() { if (SoundEnabled && promoteClip != null) audioSource.PlayOneShot(promoteClip, 0.90f * ChessSettings.SoundVolume); }
        public void PlayVictory() { if (SoundEnabled && victoryClip != null) audioSource.PlayOneShot(victoryClip, 1.0f * ChessSettings.SoundVolume); }
        public void PlayDefeat() { if (SoundEnabled && defeatClip != null) audioSource.PlayOneShot(defeatClip, 0.90f * ChessSettings.SoundVolume); }
        public void PlayDraw() { if (SoundEnabled && drawClip != null) audioSource.PlayOneShot(drawClip, 0.85f * ChessSettings.SoundVolume); }
        public void PlayTick() { if (SoundEnabled && tickClip != null) tickSource.PlayOneShot(tickClip, 0.5f * ChessSettings.SoundVolume); }
        public void PlayIllegal() { if (SoundEnabled && illegalClip != null) audioSource.PlayOneShot(illegalClip, 0.6f * ChessSettings.SoundVolume); }
    }
}
