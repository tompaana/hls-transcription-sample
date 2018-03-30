using System;

namespace HLSTools.NETFramework
{
    public class MediaSegmentContent : IDisposable
    {
        /// <summary>
        /// Full TS file content.
        /// </summary>
        public byte[] FullContent
        {
            get;
            set;
        }

        /// <summary>
        /// Audio sample extracted from the full content.
        /// </summary>
        public AudioSample Audio
        {
            get;
            set;
        }

        /// <summary>
        /// Raw audio transcription result.
        /// </summary>
        public string TranscriptionResult
        {
            get;
            set;
        }

        /// <summary>
        /// Parsed audio transcrition.
        /// </summary>
        public DetailedSpeechRecognition Transcription
        {
            get;
            set;
        }

        public void Dispose()
        {
            if (Audio != null)
            {
                Audio.Dispose();
            }
        }
    }
}
