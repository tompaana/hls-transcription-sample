using HLSTools.NETFramework;
using NAudio.Wave;
using System;
using System.Threading.Tasks;

namespace HLSConsoleTest.NETFramework
{
    class Program
    {
        // Insert .m3u8 URL here
        private const string PlaylistUrl = "";

        // Insert your Bing Speech to Text API subscription key here
        private const string BingSpeechToTextApiSubscriptionKey = "";

        private static HLSProcessor _hlsProcessor;

        static async Task MainAsync()
        {
            await _hlsProcessor.StartProcessingAsync(PlaylistUrl);
        }

        static void Main(string[] args)
        {
            _hlsProcessor = new HLSProcessor(BingSpeechToTextApiSubscriptionKey);
            _hlsProcessor.MediaSegmentProcessed += OnMediaSegmentProcessed;
            MainAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Plays the audio in the given media and logs the audio transcription if available.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="mediaSegmentContent"></param>
        static void OnMediaSegmentProcessed(object sender, MediaSegmentContent mediaSegmentContent)
        {
            if (mediaSegmentContent.Transcription.NBest != null
                && mediaSegmentContent.Transcription.NBest.Count > 0)
            {
                Log(mediaSegmentContent.Transcription.NBest[0].Display);
            }
            else
            {
                Log(mediaSegmentContent.TranscriptionResult);
            }

            using (WaveOut waveOut = new WaveOut(WaveCallbackInfo.FunctionCallback()))
            {
                waveOut.Init(mediaSegmentContent.Audio.BlockAlignReductionStream);
                waveOut.Play();

                while (waveOut.PlaybackState == PlaybackState.Playing)
                {
                    System.Threading.Thread.Sleep(100);
                }
            }

            mediaSegmentContent.Dispose();
        }

        static void Log(string message)
        {
            System.Diagnostics.Debug.WriteLine(message);
            Console.WriteLine(message);
        }
    }
}
