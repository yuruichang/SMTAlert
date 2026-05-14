using NAudio.Wave;
using System;
using System.IO;

namespace SMTAlert
{
    /// <summary>
    /// Plays the SMT alert sound (woop.mp3) via NAudio, same as the main SMT application.
    /// </summary>
    public static class AlertSound
    {
        private static WaveOutEvent _waveOut;
        private static AudioFileReader _audioReader;
        private static readonly object _lock = new();

        public static void Play()
        {
            lock (_lock)
            {
                try
                {
                    if (_waveOut == null)
                    {
                        string soundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounds", "woop.mp3");
                        if (!File.Exists(soundPath)) return;

                        _audioReader = new AudioFileReader(soundPath);
                        _waveOut = new WaveOutEvent();
                        _waveOut.Init(_audioReader);
                    }

                    _waveOut.Stop();
                    _audioReader.Position = 0;
                    _waveOut.Volume = App.Config.AlertVolume;
                    _waveOut.Play();
                }
                catch
                {
                    // If playback fails, reset so the next call reinitializes
                    _waveOut?.Dispose();
                    _waveOut = null;
                    _audioReader?.Dispose();
                    _audioReader = null;
                }
            }
        }

        public static void Dispose()
        {
            lock (_lock)
            {
                _waveOut?.Dispose();
                _waveOut = null;
                _audioReader?.Dispose();
                _audioReader = null;
            }
        }
    }
}
