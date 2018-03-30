using NAudio.Wave;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace HLSTools.NETFramework
{
    /// <summary>
    /// HTTP Live Streaming content processor.
    /// Extracts audio from the video segments and does audio transcription using Bing Speech to Text API.
    /// </summary>
    public class HLSProcessor
    {
        /// <summary>
        /// Fired when a new media segment is processed.
        /// </summary>
        public event EventHandler<MediaSegmentContent> MediaSegmentProcessed;

        private string _bingSpeechToTextApiSubscriptionKey;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="bingSpeechToTextApiSubscriptionKey">
        /// The Bing speech to text API subscription key for transcribing the audio.
        /// Not required: Null -> no transcription.</param>
        public HLSProcessor(string bingSpeechToTextApiSubscriptionKey)
        {
            _bingSpeechToTextApiSubscriptionKey = bingSpeechToTextApiSubscriptionKey;
        }

        /// <summary>
        /// Starts processing the given playlist.
        /// </summary>
        /// <param name="playlistUrl">The HTTP live streaming playlist URL (.m3u8).</param>
        /// <returns>True, if items to process were found. False otherwise.</returns>
        public async Task<bool> StartProcessingAsync(string playlistUrl)
        {
            if (string.IsNullOrWhiteSpace(playlistUrl))
            {
                throw new ArgumentNullException("Playlist URL missing");
            }

            var files = LoadPlaylist(new Uri(playlistUrl));

            if (files == null || files.Count() == 0)
            {
                System.Diagnostics.Debug.WriteLine("No files in playlist");
                return false;
            }

            foreach (var file in files)
            {
                await ProcessStreamAsync(file);
            }

            return true;
        }

        /// <summary>
        /// Loads a playlist from the given URI.
        /// </summary>
        /// <param name="playlistUri">The playlist URI (.m3u8).</param>
        /// <returns>The files in the playlist to process.</returns>
        public IEnumerable<Uri> LoadPlaylist(Uri playlistUri)
        {
            using (WebClient webClient = new WebClient())
            {
                var uriHashset = new HashSet<Uri>();
                var uriQueue = new Queue<Uri>();
                uriQueue.Enqueue(playlistUri);

                while (uriQueue.Count != 0)
                {
                    Uri uri = uriQueue.Dequeue();

                    if (!uriHashset.Add(uri))
                    {
                        continue;
                    }

                    string playlistItemUrls = webClient.DownloadString(uri);

                    string[] playlistItemUrlArray =
                        playlistItemUrls.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (string playlistItemUrl in playlistItemUrlArray)
                    {
                        if (string.IsNullOrWhiteSpace(playlistItemUrl)
                            || playlistItemUrl[0] == '#'
                            || !Uri.TryCreate(uri, playlistItemUrl, out Uri playlistItemUri))
                        {
                            //System.Diagnostics.Debug.WriteLine($"Invalid URI: '{playlistItemUrl}'");
                            continue;
                        }

                        System.Diagnostics.Debug.WriteLine($"Processing URI: {playlistItemUrl}");
                        string extension = Path.GetExtension(playlistItemUri.LocalPath);

                        if (extension.StartsWith(".m3u", StringComparison.OrdinalIgnoreCase))
                        {
                            uriQueue.Enqueue(playlistItemUri);
                        }
                        else
                        {
                            yield return playlistItemUri;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Processes the stream with the given URI:
        /// Extracts the audio and does the audio transcription.
        /// Once the processing is complete, MediaSegmentProcessed event,
        /// including the processed content, is fired.
        /// </summary>
        /// <param name="streamUri">The stream URI.</param>
        public async Task ProcessStreamAsync(Uri streamUri)
        {
            MediaSegmentContent mediaSegmentContent = null;

            try
            {
                mediaSegmentContent = await LoadTsFileAndExtractAudioAsync(streamUri);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to extract audio: {e.Message}");
            }

            if (!string.IsNullOrWhiteSpace(_bingSpeechToTextApiSubscriptionKey)
                && mediaSegmentContent.Audio != null)
            {
                string transcription = string.Empty;
                DetailedSpeechRecognition detailedSpeechRecognition = null;

                try
                {
                    transcription = new Transcriber(_bingSpeechToTextApiSubscriptionKey).Transcribe(mediaSegmentContent.Audio);
                    detailedSpeechRecognition = JsonConvert.DeserializeObject<DetailedSpeechRecognition>(transcription);
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to transcribe audio/deserialize transcription: {e.Message}");
                }

                mediaSegmentContent.TranscriptionResult = transcription;
                mediaSegmentContent.Transcription = detailedSpeechRecognition;
            }

            // Notify
            MediaSegmentProcessed?.Invoke(this, mediaSegmentContent);
        }

        /// <summary>
        /// Loads the .ts file with the given URI and extracts the audio.
        /// </summary>
        /// <param name="tsFileUri">The .ts file URI.</param>
        /// <returns>Loaded and processed media segment content.</returns>
        public async Task<MediaSegmentContent> LoadTsFileAndExtractAudioAsync(Uri tsFileUri)
        {
            if (string.IsNullOrWhiteSpace(tsFileUri.ToString()))
            {
                throw new ArgumentNullException("TS file URI missing");
            }

            byte[] contentAsByteArray = null;

            using (HttpClient httpClient = new HttpClient())
            {
                using (HttpResponseMessage httpResponseMessage = await httpClient.GetAsync(tsFileUri))
                {
                    using (HttpContent httpContent = httpResponseMessage.Content)
                    {
                        contentAsByteArray = await httpContent.ReadAsByteArrayAsync();
                    }
                }
            }

            MediaSegmentContent mediaSegmentContent = new MediaSegmentContent()
            {
                FullContent = contentAsByteArray
            };

            MemoryStream contentAsMemoryStream = new MemoryStream(contentAsByteArray);

            using (WaveStream pcmStream =
                WaveFormatConversionStream.CreatePcmStream(
                    new StreamMediaFoundationReader(contentAsMemoryStream)))
            {
                // TODO: Read in chunks if the length is long
                WaveStream blockAlignReductionStream = new BlockAlignReductionStream(pcmStream);
                mediaSegmentContent.Audio = new AudioSample(blockAlignReductionStream);
            }

            System.Diagnostics.Debug.WriteLine($"Extracted: {mediaSegmentContent.Audio}");
            return mediaSegmentContent;
        }

        /// <summary>
        /// For convenience.
        /// </summary>
        public async Task<MediaSegmentContent> LoadTsFileAndExtractAudioAsync(string tsFileUrl)
        {
            return await LoadTsFileAndExtractAudioAsync(new Uri(tsFileUrl));
        }
    }
}
