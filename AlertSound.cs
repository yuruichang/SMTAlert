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
            try
            {
                lock (_lock)
                {
                    if (_waveOut == null)
                    {
                        string soundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounds", "woop.mp3");
                        if (!File.Exists(soundPath)) return;

                        _audioReader = new AudioFileReader(soundPath);
                        _waveOut = new WaveOutEvent { DeviceNumber = -1 };
                        try
                        {
                            _waveOut.Init(_audioReader);
                        }
                        catch
                        {
                            // wave output fails on some devices
                        }
                    }

                    _waveOut.Stop();
                    _audioReader.Position = 0;
                    _waveOut.Play();
                }
            }
            catch { }
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
