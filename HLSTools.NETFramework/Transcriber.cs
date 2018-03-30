using System;
using System.IO;
using System.Net;

namespace HLSTools.NETFramework
{
    /// <summary>
    /// Transcribes audio samples.
    /// </summary>
    public class Transcriber
    {
        private const string BingSpeechToTextApiFormat = "detailed"; // "simple", "detailed"
        private static readonly string BingSpeechToTextApiUri = string.Format(
            "https://speech.platform.bing.com/speech/recognition/conversation/cognitiveservices/v1?language=en-GB&format={0}",
            BingSpeechToTextApiFormat);

        private string _subscriptionKey;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="subscriptionKey">The Bing speech to text API subscription key.</param>
        public Transcriber(string subscriptionKey)
        {
            if (string.IsNullOrWhiteSpace(subscriptionKey))
            {
                throw new ArgumentNullException("Subscription key cannot be null");
            }

            _subscriptionKey = subscriptionKey;
        }

        /// <summary>
        /// Transcribes the given audio sample using Bing Speech to Text API.
        /// </summary>
        /// <param name="audioSample">The audio sample to transcribe.</param>
        /// <returns>The transcription result (JSON).</returns>
        public string Transcribe(AudioSample audioSample)
        {
            //System.Diagnostics.Debug.WriteLine($"Bing speech to text API URI: {BingSpeechToTextApiUri}");

            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(BingSpeechToTextApiUri);
            httpWebRequest.SendChunked = true;
            httpWebRequest.Accept = @"application/json;text/xml";
            httpWebRequest.Method = "POST";
            httpWebRequest.ProtocolVersion = HttpVersion.Version11;
            httpWebRequest.Host = @"speech.platform.bing.com";

            string contentType =
                $"audio/wav; codec=audio/{audioSample.WaveFormatEncoding.ToLower()}; samplerate={audioSample.BlockAlignReductionStream.WaveFormat.SampleRate}";
            //System.Diagnostics.Debug.WriteLine($"Content type: {contentType}");

            httpWebRequest.ContentType = @contentType;
            httpWebRequest.Headers["Ocp-Apim-Subscription-Key"] = _subscriptionKey;

            byte[] audioBytes = audioSample.GetBytes();

            using (Stream requestStream = httpWebRequest.GetRequestStream())
            {
                requestStream.Write(audioBytes, 0, audioBytes.Length);
                requestStream.Flush();
            }

            string responseString = null;

            using (WebResponse webResponse = httpWebRequest.GetResponse())
            {
                System.Diagnostics.Debug.WriteLine($"Bing Speech to Text API request returned status code: {((HttpWebResponse)webResponse).StatusCode}");

                using (StreamReader streamReader = new StreamReader(webResponse.GetResponseStream()))
                {
                    responseString = streamReader.ReadToEnd();
                }
            }

            return responseString;
        }
    }
}
